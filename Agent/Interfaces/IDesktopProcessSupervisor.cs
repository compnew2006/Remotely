using Remotely.Agent.Models;

namespace Remotely.Agent.Interfaces;

/// <summary>
/// Keeps the desktop client alive.  The desktop client runs in the interactive session of
/// the target machine, so if it is terminated (for example with "End task" in Task
/// Manager) the remote control session dies with it.  The supervisor watches the process
/// and starts it again with the same arguments when it exits unexpectedly.
/// </summary>
public interface IDesktopProcessSupervisor
{
    /// <summary>
    /// Starts watching the given desktop client process.
    /// </summary>
    void Track(DesktopProcessLaunchInfo launchInfo);

    /// <summary>
    /// Stops watching the desktop client that was started for the given session and role.
    /// </summary>
    void Untrack(int targetSessionId, string role);
}
