using Remotely.Shared.Enums;

namespace Remotely.Shared.Extensions;

public static class SessionSwitchReasonExExtensions
{
    /// <summary>
    /// Determines whether a Windows session switch means the session itself
    /// ended, as opposed to a reversible change such as a lock/unlock, an RDP
    /// connect/disconnect, or a console connect/disconnect.
    /// <para>
    /// Only a logoff destroys the session (and therefore the desktop that is
    /// being captured).  Every other switch leaves the session and its desktop
    /// alive, so an active screen cast must survive it.  Treating a lock or an
    /// RDP disconnect as terminal tore down working remote control sessions
    /// whenever the local RDP session locked or the screen saver kicked in.
    /// </para>
    /// </summary>
    public static bool IsTerminalSessionSwitch(this SessionSwitchReasonEx reason)
    {
        return reason == SessionSwitchReasonEx.SessionLogoff;
    }
}
