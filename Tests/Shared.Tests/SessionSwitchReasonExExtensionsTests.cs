using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Enums;
using Remotely.Shared.Extensions;

namespace Remotely.Shared.Tests;

/// <summary>
/// A Windows session switch that is not a logoff leaves the session and its
/// desktop alive.  Treating those switches as terminal tore down every active
/// remote control session as soon as the local session locked, the screen
/// saver kicked in, or the console was detached.  Only a logoff ends the
/// session for real.
/// </summary>
[TestClass]
public class SessionSwitchReasonExExtensionsTests
{
    [TestMethod]
    [DataRow(SessionSwitchReasonEx.SessionLogoff)]
    public void IsTerminalSessionSwitch_LogoffIsTerminal(SessionSwitchReasonEx reason)
    {
        Assert.IsTrue(reason.IsTerminalSessionSwitch());
    }

    [TestMethod]
    [DataRow(SessionSwitchReasonEx.SessionLock)]
    [DataRow(SessionSwitchReasonEx.SessionUnlock)]
    [DataRow(SessionSwitchReasonEx.SessionLogon)]
    [DataRow(SessionSwitchReasonEx.RemoteConnect)]
    [DataRow(SessionSwitchReasonEx.RemoteDisconnect)]
    [DataRow(SessionSwitchReasonEx.ConsoleConnect)]
    [DataRow(SessionSwitchReasonEx.ConsoleDisconnect)]
    [DataRow(SessionSwitchReasonEx.SessionRemoteControl)]
    public void IsTerminalSessionSwitch_ReversibleSwitchesAreNotTerminal(SessionSwitchReasonEx reason)
    {
        Assert.IsFalse(reason.IsTerminalSessionSwitch());
    }

    [TestMethod]
    public void EnumValues_MatchTheWindowsSessionSwitchReasons()
    {
        // The desktop client casts the .NET SessionSwitchReason value directly
        // to this enum, so the numbering has to stay identical to the one in
        // System.SessionSwitchReason.
        Assert.AreEqual(1, (int)SessionSwitchReasonEx.ConsoleConnect);
        Assert.AreEqual(2, (int)SessionSwitchReasonEx.ConsoleDisconnect);
        Assert.AreEqual(3, (int)SessionSwitchReasonEx.RemoteConnect);
        Assert.AreEqual(4, (int)SessionSwitchReasonEx.RemoteDisconnect);
        Assert.AreEqual(5, (int)SessionSwitchReasonEx.SessionLogon);
        Assert.AreEqual(6, (int)SessionSwitchReasonEx.SessionLogoff);
        Assert.AreEqual(7, (int)SessionSwitchReasonEx.SessionLock);
        Assert.AreEqual(8, (int)SessionSwitchReasonEx.SessionUnlock);
        Assert.AreEqual(9, (int)SessionSwitchReasonEx.SessionRemoteControl);
    }
}
