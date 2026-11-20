using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Agent.Services;
using Remotely.Shared.Utilities;
using Remotely.Shared.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using Remotely.Agent.Services.Linux;
using Remotely.Agent.Services.MacOS;
using Remotely.Agent.Services.Windows;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Microsoft.Win32;
using System.Reflection;

namespace Remotely.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var host = Host
                .CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSystemd()
                .ConfigureServices(RegisterServices)
                .Build();

            await host.StartAsync();

            await Init(host.Services);

            await host.WaitForShutdownAsync();
        }
        catch (Exception ex)
        {
            var version = AppVersionHelper.GetAppVersion();
            var componentName = Assembly.GetExecutingAssembly().GetName().Name;
            var logger = new FileLogger($"{componentName}", version, "Main");
            logger.LogError(ex, "Error during agent startup.");
            throw;
        }
    }

    private static async Task Init(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<IHost>>();

        AppDomain.CurrentDomain.UnhandledException += (sender, ex) =>
        {
            if (ex.ExceptionObject is Exception exception)
            {
                logger.LogError(exception, "Unhandled exception in AppDomain.");
            }
            else
            {
                logger.LogError("Unhandled exception in AppDomain.");
            }
        };

        SetWorkingDirectory();

        if (OperatingSystem.IsWindows())
        {
            SetSas(services, logger);
        }

        // Connect first so the device registers online and the trusted
        // update keys are fetched from the server before the first update
        // check verifies anything (an update check that runs before the
        // keys arrive always fails with UnknownKeyId and stalls updates).
        await services.GetRequiredService<IAgentHubConnection>().Connect();

        // TODO: Move this to a BackgroundService.
        await services.GetRequiredService<IUpdater>().BeginChecking();

        // The desktop client is not part of the agent package anymore.  It is
        // downloaded from the server in the background, so the first remote
        // control session does not have to wait for it.
        PrepareDesktopClient(services, logger);
    }

    private static void PrepareDesktopClient(IServiceProvider services, ILogger logger)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var installer = services.GetRequiredService<IDesktopPackageInstaller>();
                var result = await installer.EnsureInstalledAsync();

                if (result.Success)
                {
                    logger.LogInformation("The desktop client is available at {path}.", result.ExecutablePath);
                }
                else
                {
                    logger.LogWarning(
                        "The desktop client could not be prepared ({failure}): {detail}",
                        result.Failure,
                        result.Detail);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while preparing the desktop client.");
            }
        });
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddLogging(builder =>
        {
            builder.AddConsole().AddDebug();
            var version = AppVersionHelper.GetAppVersion();
            var componentName = Assembly.GetExecutingAssembly().GetName().Name;
            builder.AddProvider(new FileLoggerProvider($"{componentName}", version));
        });

        services.AddSingleton<IAgentHubConnection, AgentHubConnection>();
        services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSampler>();
        services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
        services.AddHostedService(services => services.GetRequiredService<ICpuUtilizationSampler>());
        services.AddSingleton<IChatClientService, ChatClientService>();
        services.AddTransient<IPsCoreShell, PsCoreShell>();
        services.AddTransient<IExternalScriptingShell, ExternalScriptingShell>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IUninstaller, Uninstaller>();
        services.AddSingleton<IScriptExecutor, ScriptExecutor>();
        services.AddSingleton<IProcessInvoker, ProcessInvoker>();
        services.AddSingleton<IUpdateDownloader, UpdateDownloader>();
        services.AddSingleton<IPackageContentFetcher, PackageContentFetcher>();
        services.AddSingleton<IDesktopPackageInstaller>(services =>
        {
            var configService = services.GetRequiredService<IConfigService>();
            var fetcher = services.GetRequiredService<IPackageContentFetcher>();
            var host = configService.GetConnectionInfo().Host;
            var options = string.IsNullOrWhiteSpace(host)
                ? null
                : DesktopPackageOptionsFactory.Create(host);

            return options is null
                ? new UnavailableDesktopPackageInstaller()
                : new DesktopPackageInstaller(fetcher, options);
        });
        services.AddSingleton<IFileLogsManager, FileLogsManager>();
        services.AddSingleton<IScriptingShellFactory, ScriptingShellFactory>();

        if (OperatingSystem.IsWindows())
        {
            RegisterWindowsServices(services);
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IAppLauncher, AppLauncherLinux>();
            services.AddSingleton<IUpdater, UpdaterLinux>();
            services.AddSingleton<IDeviceInformationService, DeviceInfoGeneratorLinux>();
            services.AddSingleton<IElevationDetector, ElevationDetectorLinux>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IAppLauncher, AppLauncherMac>();
            services.AddSingleton<IUpdater, UpdaterMac>();
            services.AddSingleton<IDeviceInformationService, DeviceInfoGeneratorMac>();
            services.AddSingleton<IElevationDetector, ElevationDetectorMac>();
        }
        else
        {
            throw new NotSupportedException("Operating system not supported.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsServices(IServiceCollection services)
    {
        services.AddSingleton<IAppLauncher, AppLauncherWin>();
        services.AddSingleton<DesktopProcessSupervisor>();
        services.AddSingleton<IDesktopProcessSupervisor>(services => services.GetRequiredService<DesktopProcessSupervisor>());
        services.AddHostedService(services => services.GetRequiredService<DesktopProcessSupervisor>());
        services.AddSingleton<IUpdater, UpdaterWin>();
        services.AddSingleton<IDeviceInformationService, DeviceInfoGeneratorWin>();
        services.AddSingleton<IElevationDetector, ElevationDetectorWin>();
    }

    [SupportedOSPlatform("windows")]
    private static void SetSas(IServiceProvider services, ILogger<IHost> logger)
    {
        try
        {
            var elevationDetector = services.GetRequiredService<IElevationDetector>();
            if (elevationDetector.IsElevated())
            {
                // Set Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
                var subkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                subkey?.SetValue("SoftwareSASGeneration", "3", RegistryValueKind.DWord);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while setting Secure Attention Sequence in the registry.");
        }
    }

    private static void SetWorkingDirectory()
    {
        var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs().First();
        var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        Directory.SetCurrentDirectory(exeDir);
    }
}
