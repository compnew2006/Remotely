using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Agent.Models;
using Remotely.Desktop.Native.Windows;
using Remotely.Shared.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Remotely.Agent.Services.Windows;

/// <summary>
/// Watches the desktop client processes that the agent starts in the interactive sessions
/// of the target machine, and starts them again when they are terminated unexpectedly.
/// </summary>
[SupportedOSPlatform("windows")]
public class DesktopProcessSupervisor : BackgroundService, IDesktopProcessSupervisor
{
    private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(2);
    private const string UnattendedRole = "Unattended";
    private const string ChatRole = "Chat";

    private readonly ConcurrentDictionary<string, TrackedProcess> _trackedProcesses = new();
    private readonly DesktopProcessRecoveryPolicy _policy = new();
    private readonly ILogger<DesktopProcessSupervisor> _logger;
    private readonly string _stateFilePath;

    public DesktopProcessSupervisor(ILogger<DesktopProcessSupervisor> logger)
    {
        _logger = logger;
        _stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DesktopProcesses.json");
    }

    public void Track(DesktopProcessLaunchInfo launchInfo)
    {
        var process = AttachProcess(launchInfo.ProcessId);
        launchInfo.ProcessStartTime = GetStartTime(process);

        var key = BuildKey(launchInfo.TargetSessionId, launchInfo.Role);

        _trackedProcesses[key] = new TrackedProcess(launchInfo, process);

        _logger.LogInformation(
            "Watching desktop process {ProcessId} for session {SessionId} ({Role}).",
            launchInfo.ProcessId,
            launchInfo.TargetSessionId,
            launchInfo.Role);

        SaveState();
    }

    public void Untrack(int targetSessionId, string role)
    {
        var key = BuildKey(targetSessionId, role);

        if (_trackedProcesses.TryRemove(key, out var tracked))
        {
            _logger.LogInformation(
                "No longer watching desktop process {ProcessId} for session {SessionId} ({Role}).",
                tracked.Info.ProcessId,
                targetSessionId,
                role);

            SaveState();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RestoreState();

        using var timer = new PeriodicTimer(_checkInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                CheckTrackedProcesses();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking the desktop processes.");
            }
        }
    }

    private void CheckTrackedProcesses()
    {
        foreach (var entry in _trackedProcesses.ToArray())
        {
            var tracked = entry.Value;

            if (!HasExited(tracked, out var exitCode))
            {
                continue;
            }

            if (IsUpdateInProgress())
            {
                // The installer is replacing the desktop. Relaunching here
                // would resurrect the very process the installer is removing
                // and fail its install. Leave the session untracked: the
                // new agent starts a fresh desktop on demand.
                _logger.LogInformation(
                    "The desktop process {ProcessId} for session {SessionId} was terminated while an update is in progress.  It will not be started again.",
                    tracked.Info.ProcessId,
                    tracked.Info.TargetSessionId);

                RemoveTracked(entry.Key);
                continue;
            }

            if (_policy.IsCleanExit(exitCode))
            {
                _logger.LogInformation(
                    "The desktop process {ProcessId} for session {SessionId} exited normally.  It will not be started again.",
                    tracked.Info.ProcessId,
                    tracked.Info.TargetSessionId);

                RemoveTracked(entry.Key);
                continue;
            }

            var now = DateTimeOffset.Now;

            if (now < tracked.NextAttemptAt)
            {
                continue;
            }

            tracked.RecentAttempts.RemoveAll(x => now - x >= _policy.AttemptWindow);

            if (!_policy.ShouldRecover(exitCode, now, tracked.RecentAttempts))
            {
                _logger.LogError(
                    "The desktop process for session {SessionId} keeps exiting unexpectedly (exit code {ExitCode}).  " +
                    "Giving up after {Attempts} attempts.  Use \"Restart Screen Caster\" in the server to recover the session.",
                    tracked.Info.TargetSessionId,
                    exitCode,
                    tracked.RecentAttempts.Count);

                RemoveTracked(entry.Key);
                continue;
            }

            tracked.RecentAttempts.Add(now);

            var newProcessId = Relaunch(tracked.Info);

            tracked.NextAttemptAt = now.Add(_policy.GetDelay(tracked.RecentAttempts.Count));

            if (newProcessId <= 0)
            {
                _logger.LogError(
                    "Failed to start the desktop process for session {SessionId} again.",
                    tracked.Info.TargetSessionId);

                continue;
            }

            tracked.Info.ProcessId = newProcessId;
            tracked.Info.ProcessStartTime = null;
            tracked.Process = AttachProcess(newProcessId);
            tracked.Info.ProcessStartTime = GetStartTime(tracked.Process);

            _logger.LogInformation(
                "The desktop process for session {SessionId} was terminated (exit code {ExitCode}).  " +
                "Started it again as process {ProcessId}.",
                tracked.Info.TargetSessionId,
                exitCode,
                newProcessId);

            SaveState();
        }
    }

    private bool HasExited(TrackedProcess tracked, out int exitCode)
    {
        exitCode = 0;

        var process = tracked.Process;

        if (process is null)
        {
            return true;
        }

        try
        {
            if (!process.HasExited)
            {
                return false;
            }

            exitCode = process.ExitCode;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to read the exit code of desktop process {ProcessId}.  Treating it as an unexpected exit.",
                tracked.Info.ProcessId);

            exitCode = int.MinValue;

            return true;
        }
    }

    private int Relaunch(DesktopProcessLaunchInfo info)
    {
        try
        {
            if (!IsSessionActive(info.TargetSessionId))
            {
                _logger.LogWarning(
                    "Session {SessionId} is no longer active.  The desktop process will not be started again.",
                    info.TargetSessionId);

                return -1;
            }

            if (WindowsIdentity.GetCurrent().IsSystem)
            {
                var commandLine = info.ExecutablePath + info.Arguments;

                if (!Win32Interop.CreateInteractiveSystemProcess(
                        commandLine,
                        info.TargetSessionId,
                        hiddenWindow: false,
                        out var procInfo))
                {
                    return -1;
                }

                return (int)procInfo.dwProcessId;
            }

            var process = Process.Start(info.ExecutablePath, info.Arguments);

            return process?.Id ?? -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting the desktop process again.");

            return -1;
        }
    }

    private static bool IsSessionActive(int sessionId)
    {
        try
        {
            return Win32Interop.GetActiveSessions().Any(x => x.Id == (uint)sessionId);
        }
        catch
        {
            return true;
        }
    }

    private static Process? AttachProcess(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            var process = Process.GetProcessById(processId);

            // Opening the handle now keeps it usable after the process is gone, which is
            // what allows the exit code to be read later on.
            _ = process.Handle;

            return process;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static DateTimeOffset? GetStartTime(Process? process)
    {
        if (process is null)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void RemoveTracked(string key)
    {
        _trackedProcesses.TryRemove(key, out _);

        SaveState();
    }

    /// <summary>
    /// The installer writes this flag next to the agent before stopping it,
    /// so an expected shutdown looks intentional to the supervisor. A flag
    /// older than ten minutes is treated as stale (a crashed installer) and
    /// ignored, so a forgotten flag cannot disable recovery forever.
    /// </summary>
    private static bool IsUpdateInProgress()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SuppressDesktopRestart.flag");

            if (!File.Exists(path))
            {
                return false;
            }

            var created = File.GetLastWriteTime(path);

            if (DateTime.Now - created >= TimeSpan.FromMinutes(10))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildKey(int targetSessionId, string role) => $"{targetSessionId}:{role}";

    /// <summary>
    /// Reads the state left behind by a previous run of the agent, so that a desktop client
    /// that outlived the agent is still watched.  A desktop client that is no longer running
    /// is not started again here; the server decides when a session should exist.
    /// </summary>
    private void RestoreState()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return;
            }

            var state = JsonSerializer.Deserialize<List<DesktopProcessLaunchInfo>>(
                File.ReadAllText(_stateFilePath));

            if (state is null)
            {
                return;
            }

            foreach (var info in state)
            {
                var process = AttachProcess(info.ProcessId);

                if (process is null || HasExitedProcess(process))
                {
                    continue;
                }

                var startTime = GetStartTime(process);

                if (info.ProcessStartTime is not null &&
                    startTime is not null &&
                    Math.Abs((startTime.Value - info.ProcessStartTime.Value).TotalSeconds) > 2)
                {
                    // The process id was recycled by an unrelated process.
                    continue;
                }

                _trackedProcesses[BuildKey(info.TargetSessionId, info.Role)] =
                    new TrackedProcess(info, process);

                _logger.LogInformation(
                    "Resumed watching desktop process {ProcessId} for session {SessionId} ({Role}).",
                    info.ProcessId,
                    info.TargetSessionId,
                    info.Role);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to restore the watched desktop processes.");
        }
    }

    private void SaveState()
    {
        try
        {
            var state = _trackedProcesses.Values.Select(x => x.Info).ToList();

            File.WriteAllText(_stateFilePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to save the watched desktop processes.");
        }
    }

    private static bool HasExitedProcess(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private sealed class TrackedProcess
    {
        public TrackedProcess(DesktopProcessLaunchInfo info, Process? process)
        {
            Info = info;
            Process = process;
        }

        public DesktopProcessLaunchInfo Info { get; }

        public Process? Process { get; set; }

        public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.MinValue;

        public List<DateTimeOffset> RecentAttempts { get; } = new();
    }
}
