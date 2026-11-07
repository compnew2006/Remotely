using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Shared.Services;
using Remotely.Shared.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Remotely.Agent.Services.Windows;

public class UpdaterWin : IUpdater
{
    private readonly SemaphoreSlim _checkForUpdatesLock = new(1, 1);
    private readonly IConfigService _configService;
    private readonly IUpdateDownloader _updateDownloader;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdaterWin> _logger;
    private readonly SemaphoreSlim _installLatestVersionLock = new(1, 1);
    private readonly System.Timers.Timer _updateTimer = new(TimeSpan.FromHours(6).TotalMilliseconds);
    private DateTimeOffset _lastUpdateFailure;


    public UpdaterWin(
        IConfigService configService,
        IUpdateDownloader updateDownloader,
        IHttpClientFactory httpClientFactory,
        ILogger<UpdaterWin> logger)
    {
        _configService = configService;
        _updateDownloader = updateDownloader;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task BeginChecking()
    {
        if (EnvironmentHelper.IsDebug)
        {
            return;
        }

        await CheckForUpdates();
        _updateTimer.Elapsed += UpdateTimer_Elapsed;
        _updateTimer.Start();
    }

    public async Task CheckForUpdates()
    {
        if (!await _checkForUpdatesLock.WaitAsync(0))
        {
            return;
        }

        try
        {

            if (EnvironmentHelper.IsDebug)
            {
                return;
            }

            if (_lastUpdateFailure.AddDays(1) > DateTimeOffset.Now)
            {
                _logger.LogInformation("Skipping update check due to previous failure.  Updating will be tried again after 24 hours have passed.");
                return;
            }


            var connectionInfo = _configService.GetConnectionInfo();
            var serverUrl = _configService.GetConnectionInfo().Host;

            var platform = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            var fileUrl = serverUrl + $"/Content/Remotely-Win-{platform}.zip";

            using var httpClient = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, fileUrl);

            if (File.Exists("etag.txt"))
            {
                var lastEtag = await File.ReadAllTextAsync("etag.txt");
                if (!string.IsNullOrWhiteSpace(lastEtag) &&
                   EntityTagHeaderValue.TryParse(lastEtag.Trim(), out var etag))
                {
                    request.Headers.IfNoneMatch.Add(etag);
                }
            }

            using var response = await httpClient.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                _logger.LogInformation("Service Updater: Version is current.");
                return;
            }


            _logger.LogInformation("Service Updater: Update found.");

            await InstallLatestVersion();

        }
        catch (WebException ex) when (ex.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.NotModified)
        {
            _logger.LogInformation("Service Updater: Version is current.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for updates.");
        }
        finally
        {
            _checkForUpdatesLock.Release();
        }
    }

    public async Task InstallLatestVersion()
    {
        try
        {
            await _installLatestVersionLock.WaitAsync();

            var connectionInfo = _configService.GetConnectionInfo();
            var serverUrl = connectionInfo.Host;

            _logger.LogInformation("Service Updater: Downloading install package.");

            // G2 (OD-11 audit): use a fresh randomized working directory instead
            // of fixed %TEMP% paths, closing most of the verify-then-execute
            // TOCTOU window (replacing a fixed-path file between verification
            // and Process.Start). Residual local-replacement risk is documented
            // in the evidence map.
            var workDir = Path.Combine(
                Path.GetTempPath(),
                "RemotelyUpdate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);

            var zipPath = Path.Combine(workDir, "RemotelyUpdate.zip");

            var installerPath = Path.Combine(workDir, "Install-Remotely.ps1");
            var platform = Environment.Is64BitOperatingSystem ? "x64" : "x86";

            await _updateDownloader.DownloadFile(
                 $"{serverUrl}/Content/Install-Remotely.ps1",
                 installerPath);

            await _updateDownloader.DownloadFile(
               $"{serverUrl}/api/AgentUpdate/DownloadPackage/win-{platform}",
               zipPath);

            // G2 (AD-13): verify the signed update manifest, the package hash,
            // and the installer hash before anything is executed. Fail-closed:
            // any verification failure aborts the update; the running agent
            // keeps working and retries later.
            if (!await VerifyUpdatePackage(serverUrl!, platform, zipPath, installerPath))
            {
                _logger.LogError(
                    "Service Updater: Update verification failed.  " +
                    "The update will NOT be installed.  " +
                    "Ensure the server publishes a signed manifest for the package.");
                _lastUpdateFailure = DateTimeOffset.Now;
                return;
            }

            _logger.LogInformation("Launching installer to perform update.");

            // Same contract as the installer: an expected desktop shutdown
            // must look intentional to the supervisor. The installer removes
            // the flag once the new service is running; a stale flag is
            // ignored by the supervisor after ten minutes.
            SuppressDesktopRestart();

            Process.Start(
                "powershell.exe",
                $"-ExecutionPolicy Bypass -File \"{installerPath}\" -Path \"{zipPath}\" " +
                    $"-OrganizationId {connectionInfo.OrganizationID} -ServerUrl {connectionInfo.Host}");
        }
        catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout)
        {
            _logger.LogWarning("Timed out while waiting to download update.");
            _lastUpdateFailure = DateTimeOffset.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while installing latest version.");
            _lastUpdateFailure = DateTimeOffset.Now;
        }
        finally
        {
            _installLatestVersionLock.Release();
        }
    }

    /// <summary>
    /// G2 (AD-13): downloads the signed manifest and signature from the server,
    /// then verifies key trust, signature, package hash, installer hash, and
    /// version rollback protection. Any failure returns false (fail-closed).
    /// </summary>
    private async Task<bool> VerifyUpdatePackage(string serverUrl, string platform, string zipPath, string installerPath)
    {
        try
        {
            var manifestPath = Path.Combine(Path.GetDirectoryName(zipPath)!, "RemotelyUpdate.manifest.json");
            var signaturePath = Path.Combine(Path.GetDirectoryName(zipPath)!, "RemotelyUpdate.manifest.sig");

            await _updateDownloader.DownloadFile(
                $"{serverUrl}/Content/Remotely-Win-{platform}.zip.manifest.json",
                manifestPath);
            await _updateDownloader.DownloadFile(
                $"{serverUrl}/Content/Remotely-Win-{platform}.zip.manifest.sig",
                signaturePath);

            var trustedKeys = UpdatePackageVerifier.LoadTrustedKeysFile(
                Path.Combine(AppContext.BaseDirectory, "TrustedUpdateKeys.json"));

            var currentVersion = typeof(UpdaterWin).Assembly.GetName().Version ?? new Version(0, 0, 0);

            var result = UpdatePackageVerifier.Verify(
                await File.ReadAllBytesAsync(manifestPath),
                await File.ReadAllTextAsync(signaturePath),
                await File.ReadAllBytesAsync(zipPath),
                await File.ReadAllBytesAsync(installerPath),
                currentVersion,
                trustedKeys);

            if (!result.Success)
            {
                _logger.LogError(
                    "Service Updater: verification failed ({failure}).  Detail: {detail}",
                    result.Failure,
                    result.Detail);
                return false;
            }

            _logger.LogInformation("Service Updater: update manifest verified successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Updater: exception during update verification. Failing closed.");
            return false;
        }
    }

    private static void SuppressDesktopRestart()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "SuppressDesktopRestart.flag");
            File.WriteAllText(path, DateTimeOffset.Now.ToString("o"));
        }
        catch (Exception)
        {
            // The installer's hardened stop order covers the retry loop
            // on its own; the flag is defense in depth.
        }
    }

    private async void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        await CheckForUpdates();
    }
}
