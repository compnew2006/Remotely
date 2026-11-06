using System;

namespace Remotely.Agent.Models;

/// <summary>
/// Describes how the desktop client was started, so that it can be started again with
/// the same arguments if it is terminated unexpectedly.
/// </summary>
public class DesktopProcessLaunchInfo
{
    /// <summary>
    /// The process id of the running desktop client.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// The Windows session that the desktop client was started in.
    /// </summary>
    public int TargetSessionId { get; set; }

    /// <summary>
    /// Identifies what the desktop client is doing (unattended session, chat, ...).
    /// Used to keep more than one desktop client per session apart.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The full path of the desktop client executable.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// The arguments that the desktop client was started with.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// The time that the desktop client process was started.  Used to make sure that a
    /// recycled process id is not mistaken for the desktop client.
    /// </summary>
    public DateTimeOffset? ProcessStartTime { get; set; }
}
