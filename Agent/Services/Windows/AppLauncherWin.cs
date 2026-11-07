using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Agent.Models;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using Remotely.Desktop.Native.Windows;
using System.Runtime.InteropServices;

namespace Remotely.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
public class AppLauncherWin : IAppLauncher
{
    private const string UnattendedRole = "Unattended";
    private const string ChatRole = "Chat";

    private readonly ConnectionInfo _connectionInfo;
    private readonly ILogger<AppLauncherWin> _logger;
    private readonly IDesktopProcessSupervisor _desktopProcessSupervisor;
    private readonly string _rcBinaryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Desktop", EnvironmentHelper.DesktopExecutableFileName);

    public AppLauncherWin(
        IConfigService configService,
        ILogger<AppLauncherWin> logger,
        IDesktopProcessSupervisor desktopProcessSupervisor)
    {
        _connectionInfo = configService.GetConnectionInfo();
        _logger = logger;
        _desktopProcessSupervisor = desktopProcessSupervisor;
    }

    public async Task<int> LaunchChatService(string pipeName, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection)
    {
        try
        {
            if (!File.Exists(_rcBinaryPath))
            {
                await hubConnection.SendAsync("DisplayMessage", "Chat executable not found on target device.", "Executable not found on device.", "bg-danger", userConnectionId);
            }


            // Start Desktop app.
            await hubConnection.SendAsync("DisplayMessage", $"Starting chat service.", "Starting chat service.", "bg-success", userConnectionId);

            var arguments =
                $" --mode Chat" +
                $" --host \"{_connectionInfo.Host}\"" +
                $" --pipe-name {pipeName}" +
                $" --requester-name \"{requesterName}\"" +
                $" --org-name \"{orgName}\"" +
                $" --org-id \"{orgId}\"";

            var processId = LaunchDesktopProcess(arguments, targetSessionId: -1, ChatRole);

            if (processId <= 0)
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Chat service failed to start on target device.",
                    "Failed to start chat service.",
                    "bg-danger",
                    userConnectionId);
            }

            return processId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching chat.");
            await hubConnection.SendAsync("DisplayMessage",
                "Chat service failed to start on target device.",
                "Failed to start chat service.",
                "bg-danger",
                userConnectionId);
        }
        return -1;
    }

    public async Task LaunchRemoteControl(int targetSessionId, string sessionId, string accessKey, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection)
    {
        try
        {
            if (!File.Exists(_rcBinaryPath))
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Remote control executable not found on target device.",
                    "Executable not found on device.",
                    "bg-danger",
                    userConnectionId);
                return;
            }


            // Start Desktop app.
            await hubConnection.SendAsync("DisplayMessage",
                "Starting remote control",
                "Starting remote control",
                "bg-success",
                userConnectionId);

            var arguments =
                $" --mode Unattended" +
                $" --host {_connectionInfo.Host}" +
                $" --requester-name \"{requesterName}\"" +
                $" --org-name \"{orgName}\"" +
                $" --org-id \"{orgId}\"" +
                $" --session-id \"{sessionId}\"" +
                $" --access-key \"{accessKey}\"";

            // The desktop client uses the keyboard layout of the thread that
            // starts it, and the agent is a service that runs as the system.
            // The layout of the window that has the focus is used, so the
            // characters that the client captures are the ones of the user.
            ApplyForegroundKeyboardLayout();

            var processId = LaunchDesktopProcess(arguments, targetSessionId, UnattendedRole);

            if (processId <= 0)
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Remote control failed to start on target device.",
                    "Failed to start remote control.",
                    "bg-danger",
                    userConnectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching remote control.");
            await hubConnection.SendAsync("DisplayMessage",
                "Remote control failed to start on target device.",
                "Failed to start remote control.",
                "bg-danger",
                userConnectionId);
        }
    }
    public async Task RestartScreenCaster(string[] viewerIds, string sessionId, string accessKey, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection, int targetSessionID = -1)
    {
        try
        {
            // Start Desktop app.
            _logger.LogInformation("Restarting screen caster.");
            if (WindowsIdentity.GetCurrent().IsSystem)
            {
                // Give a little time for session changing, etc.
                await Task.Delay(1000);
            }

            var arguments =
                $" --mode Unattended" +
                $" --relaunch true" +
                $" --host {_connectionInfo.Host}" +
                $" --requester-name \"{requesterName}\"" +
                $" --org-name \"{orgName}\"" +
                $" --org-id \"{orgId}\"" +
                $" --session-id \"{sessionId}\"" +
                $" --access-key \"{accessKey}\"";

            // A session that was started for a capture has no viewers, so
            // the option is left out.  An option without a value stops the
            // desktop client from starting at all.
            if (viewerIds.Length > 0)
            {
                arguments += $" --viewers {string.Join(",", viewerIds)}";
            }

            var processId = LaunchDesktopProcess(arguments, targetSessionID, UnattendedRole);

            if (processId <= 0)
            {
                _logger.LogWarning("Failed to relaunch screen caster.");
                await hubConnection.SendAsync("SendConnectionFailedToViewers", viewerIds);
                await hubConnection.SendAsync("DisplayMessage",
                    "Remote control failed to start on target device.",
                    "Failed to start remote control.",
                    "bg-danger",
                    userConnectionId);
            }
        }
        catch (Exception ex)
        {
            await hubConnection.SendAsync("SendConnectionFailedToViewers", viewerIds);
            _logger.LogError(ex, "Error while restarting screen caster.");
            throw;
        }
    }

    /// <summary>
    /// Uses the keyboard layout of the window that has the focus.  The
    /// desktop client inherits the keyboard layout of the thread that
    /// starts it, and the agent runs as the system, which uses a different
    /// layout than the user.
    /// </summary>
    private void ApplyForegroundKeyboardLayout()
    {
        try
        {
            var foregroundWindow = GetForegroundWindow();

            if (foregroundWindow == IntPtr.Zero)
            {
                return;
            }

            var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
            var layout = GetKeyboardLayout(threadId);

            if (layout == IntPtr.Zero || layout == GetKeyboardLayout(0))
            {
                return;
            }

            ActivateKeyboardLayout(layout, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The keyboard layout of the focused window could not be used.");
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint dwFlags);

    /// <summary>
    /// Starts the desktop client and hands the process over to the supervisor, so that it is
    /// started again if it is terminated while the session is still active.
    /// </summary>
    private int LaunchDesktopProcess(string arguments, int targetSessionId, string role)
    {
        int processId;

        if (WindowsIdentity.GetCurrent().IsSystem)
        {
            var commandLine = _rcBinaryPath + arguments;

            if (!Win32Interop.CreateInteractiveSystemProcess(
                    commandLine,
                    targetSessionId,
                    hiddenWindow: false,
                    out var procInfo))
            {
                return -1;
            }

            processId = (int)procInfo.dwProcessId;
        }
        else
        {
            var process = Process.Start(_rcBinaryPath, arguments);

            if (process is null)
            {
                return -1;
            }

            processId = process.Id;
        }

        TrackDesktopProcess(processId, targetSessionId, role, arguments);

        return processId;
    }

    private void TrackDesktopProcess(int processId, int targetSessionId, string role, string arguments)
    {
        var resolvedSessionId = (int)Win32Interop.ResolveWindowsSession(targetSessionId);

        _desktopProcessSupervisor.Track(new DesktopProcessLaunchInfo
        {
            ProcessId = processId,
            TargetSessionId = resolvedSessionId,
            Role = role,
            ExecutablePath = _rcBinaryPath,
            Arguments = arguments
        });
    }
}
