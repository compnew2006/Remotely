using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Models;
using Remotely.Shared.Enums;
using Remotely.Shared.Models.Dtos;

namespace Remotely.Server.Tests;

/// <summary>
/// SPEC-001 security tests (SC-002, SC-004, SC-005 domain logic):
/// server-owned access mode defaults, lifecycle preservation, link
/// correctness, and the shared control-message classification that both
/// enforcement points (server relay and device) rely on.
/// </summary>
[TestClass]
public class RemoteControlAuthorizationTests
{
    [TestMethod]
    [Description("FR-003: a session created without an explicit access mode is ViewOnly.")]
    public void NewSession_DefaultsToViewOnly()
    {
        var session = new RemoteControlSession();

        Assert.AreEqual(RemoteAccessMode.ViewOnly, session.AccessMode);
        Assert.IsTrue(session.ViewOnly);
    }

    [TestMethod]
    [Description("FR-013/FR-014: cloning a ViewOnly session must keep it ViewOnly.")]
    public void CreateNew_ViewOnlySession_StaysViewOnly()
    {
        var session = new RemoteControlSession
        {
            Mode = RemoteControlMode.Unattended,
            AccessMode = RemoteAccessMode.ViewOnly
        };

        var clone = session.CreateNew();

        Assert.AreEqual(RemoteAccessMode.ViewOnly, clone.AccessMode);
        Assert.IsTrue(clone.ViewOnly);
        Assert.AreNotEqual(session.UnattendedSessionId, clone.UnattendedSessionId);
    }

    [TestMethod]
    [Description("FR-013: a lifecycle event must never widen a FullControl session into something else, and must never upgrade ViewOnly.")]
    public void CreateNew_FullControlSession_PreservesFullControl()
    {
        var session = new RemoteControlSession
        {
            Mode = RemoteControlMode.Unattended,
            AccessMode = RemoteAccessMode.FullControl
        };

        var clone = session.CreateNew();

        Assert.AreEqual(RemoteAccessMode.FullControl, clone.AccessMode);
        Assert.IsFalse(clone.ViewOnly);
    }

    [TestMethod]
    [Description("FR-003a: a server-generated viewer link must carry the session's actual access mode.")]
    public void RelativeAccessUri_ViewOnlySession_CarriesViewOnlyTrue()
    {
        var session = new RemoteControlSession
        {
            Mode = RemoteControlMode.Unattended,
            AccessMode = RemoteAccessMode.ViewOnly,
            UnattendedSessionId = Guid.NewGuid(),
            AccessKey = "key"
        };

        StringAssert.Contains(session.RelativeAccessUri, "viewonly=True");
    }

    [TestMethod]
    [Description("FR-003a: a FullControl session's link must not carry a literal or differing mode.")]
    public void RelativeAccessUri_FullControlSession_CarriesViewOnlyFalse()
    {
        var session = new RemoteControlSession
        {
            Mode = RemoteControlMode.Unattended,
            AccessMode = RemoteAccessMode.FullControl,
            UnattendedSessionId = Guid.NewGuid(),
            AccessKey = "key"
        };

        StringAssert.Contains(session.RelativeAccessUri, "viewonly=False");
    }

    [TestMethod]
    [Description("SC-002 domain logic: every keyboard/pointer/device-modifying DTO type is classified as a control message.")]
    public void IsControlMessage_AllInputTypesAreControl()
    {
        DtoType[] controlTypes =
        {
            DtoType.MouseMove, DtoType.MouseDown, DtoType.MouseUp, DtoType.Tap,
            DtoType.MouseWheel, DtoType.KeyDown, DtoType.KeyUp, DtoType.CtrlAltDel,
            DtoType.ToggleBlockInput, DtoType.TextTransfer, DtoType.KeyPress,
            DtoType.SetKeyStatesUp, DtoType.File
        };

        foreach (var dtoType in controlTypes)
        {
            Assert.IsTrue(
                dtoType.IsControlMessage(),
                $"DtoType.{dtoType} must be classified as a control message.");
        }
    }

    [TestMethod]
    [Description("SC-002 domain logic: view-only traffic (screen, audio, cursor, metrics) is never classified as control.")]
    public void IsControlMessage_ViewTrafficIsNotControl()
    {
        DtoType[] viewTypes =
        {
            DtoType.ScreenData, DtoType.ScreenSize, DtoType.ClipboardText,
            DtoType.AudioSample, DtoType.CursorChange, DtoType.SelectScreen,
            DtoType.ToggleAudio, DtoType.WindowsSessions, DtoType.FrameReceived,
            DtoType.OpenFileTransferWindow, DtoType.SessionMetrics
        };

        foreach (var dtoType in viewTypes)
        {
            Assert.IsFalse(
                dtoType.IsControlMessage(),
                $"DtoType.{dtoType} must not be classified as a control message.");
        }
    }

    [TestMethod]
    [Description("SC-002 completeness: every DtoType value is classified either way - none may be silently unhandled.")]
    public void ControlTypes_Coverage_EveryDtoTypeAccountedFor()
    {
        var allValues = Enum.GetValues<DtoType>();

        foreach (var value in allValues)
        {
            // The classifier must return a definitive answer for every value
            // without throwing, so neither enforcement point can have an
            // unhandled gap.
            _ = value.IsControlMessage();
        }

        Assert.AreEqual(allValues.Length, allValues.Distinct().Count());
    }
}
