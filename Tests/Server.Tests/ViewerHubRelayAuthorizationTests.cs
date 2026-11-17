#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Server.Tests.Mocks;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models.Dtos;
using Remotely.Shared.Primitives;

namespace Remotely.Server.Tests;

/// <summary>
/// SPEC-001 / FR-010, FR-011, FR-015: control authority is a property of the
/// viewer, evaluated at every relayed message. A FullControl session must not
/// widen a viewer that attached in View Only, and a viewer whose authority was
/// never established must default to View Only (fail closed).
///
/// These tests exercise <see cref="ViewerHub.SendDtoToClient"/> itself, which
/// is the single relay point between a browser viewer and the device. A pure
/// helper test cannot catch a regression where the relay consults the wrong
/// flag, so the hub is constructed with mocks instead.
/// </summary>
[TestClass]
public class ViewerHubRelayAuthorizationTests
{
    // These keys are the private properties ViewerHub stores in
    // HubCallerContext.Items. They are duplicated here because the property
    // names are private; the "full control viewer relays" test below proves the
    // keys are live, so a rename cannot make these tests silently vacuous.
    private const string SessionInfoKey = "SessionInfo";
    private const string ViewerViewOnlyKey = "ViewerViewOnly";
    private const string ControlAuthorizationCheckedAtKey = "ControlAuthorizationCheckedAt";

    private Mock<IRemoteControlSessionCache> _sessionCache = null!;
    private Mock<IDesktopStreamCache> _streamCache = null!;
    private Mock<ISessionRecordingSink> _recordingSink = null!;
    private Mock<IUserService> _userService = null!;
    private Mock<ISettingsService> _settingsService = null!;
    private Mock<ILogger<ViewerHub>> _logger = null!;
    private HubContextFixture<AgentHub, IAgentHubClient> _agentHubFixture = null!;
    private HubContextFixture<DesktopHub, IDesktopHubClient> _desktopHubFixture = null!;
    private Mock<IHubCallerClients<IViewerHubClient>> _callerClients = null!;
    private Mock<IViewerHubClient> _caller = null!;
    private Mock<HubCallerContext> _hubCallerContext = null!;
    private Dictionary<object, object?> _items = null!;

    [TestInitialize]
    public void Init()
    {
        _sessionCache = new Mock<IRemoteControlSessionCache>();
        _streamCache = new Mock<IDesktopStreamCache>();
        _recordingSink = new Mock<ISessionRecordingSink>();
        _userService = new Mock<IUserService>();
        _settingsService = new Mock<ISettingsService>();
        _logger = new Mock<ILogger<ViewerHub>>();
        _agentHubFixture = new HubContextFixture<AgentHub, IAgentHubClient>();
        _desktopHubFixture = new HubContextFixture<DesktopHub, IDesktopHubClient>();

        _caller = new Mock<IViewerHubClient>();
        _caller
            .Setup(x => x.ShowMessage(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _callerClients = new Mock<IHubCallerClients<IViewerHubClient>>();
        _callerClients
            .Setup(x => x.Caller)
            .Returns(_caller.Object);

        _items = new Dictionary<object, object?>();
        _hubCallerContext = new Mock<HubCallerContext>();
        _hubCallerContext
            .SetupGet(x => x.ConnectionId)
            .Returns("viewer-connection");
        _hubCallerContext
            .SetupGet(x => x.Items)
            .Returns(_items);
    }

    [TestMethod]
    [Description(
        "FR-010/FR-011/FR-015: a viewer that attached View Only must not relay a " +
        "control DTO, even though the session itself is FullControl. This is the " +
        "release-blocking case: two viewers on one session with different modes.")]
    public async Task ViewOnlyViewer_CannotRelayControlDto_InFullControlSession()
    {
        var hub = CreateHub(
            session: NewSession(RemoteAccessMode.FullControl),
            viewerViewOnly: true);

        await hub.SendDtoToClient(Serialize(DtoType.MouseMove));

        AssertDesktopDidNotReceiveDto();
    }

    [TestMethod]
    [Description("FR-011: every control DTO type is refused for a View Only viewer, not just the first one.")]
    public async Task ViewOnlyViewer_CannotRelayAnyControlDtoType()
    {
        var controlTypes = new[]
        {
            DtoType.MouseMove, DtoType.MouseDown, DtoType.MouseUp, DtoType.Tap,
            DtoType.MouseWheel, DtoType.KeyDown, DtoType.KeyUp, DtoType.CtrlAltDel,
            DtoType.ToggleBlockInput, DtoType.TextTransfer, DtoType.KeyPress,
            DtoType.SetKeyStatesUp, DtoType.File
        };

        foreach (var dtoType in controlTypes)
        {
            var hub = CreateHub(
                session: NewSession(RemoteAccessMode.FullControl),
                viewerViewOnly: true);

            await hub.SendDtoToClient(Serialize(dtoType));

            AssertDesktopDidNotReceiveDto(dtoType.ToString());
        }
    }

    [TestMethod]
    [Description(
        "FR-009: an unreadable message cannot be proven to be view traffic, so a " +
        "View Only viewer must not have it relayed.")]
    public async Task ViewOnlyViewer_CannotRelayUnparsableDto()
    {
        var hub = CreateHub(
            session: NewSession(RemoteAccessMode.FullControl),
            viewerViewOnly: true);

        await hub.SendDtoToClient(new byte[] { 0xC1, 0xFF, 0x00 });

        AssertDesktopDidNotReceiveDto();
    }

    [TestMethod]
    [Description("FR-011: view traffic from a View Only viewer still reaches the device.")]
    public async Task ViewOnlyViewer_CanRelayViewTraffic()
    {
        var hub = CreateHub(
            session: NewSession(RemoteAccessMode.FullControl),
            viewerViewOnly: true);

        await hub.SendDtoToClient(Serialize(DtoType.ScreenData));

        AssertDesktopReceivedDto();
    }

    [TestMethod]
    [Description("Regression guard: a FullControl viewer with a valid basis keeps relaying control DTOs.")]
    public async Task FullControlViewer_CanRelayControlDto()
    {
        var session = NewSession(RemoteAccessMode.FullControl);
        AllowControlBasis(session);

        var hub = CreateHub(
            session: session,
            viewerViewOnly: false,
            authenticated: true);

        await hub.SendDtoToClient(Serialize(DtoType.MouseMove));

        AssertDesktopReceivedDto();
    }

    [TestMethod]
    [Description(
        "OD-13: the control path re-evaluation must not change the consent basis of " +
        "an attended session, where an unauthenticated viewer may hold FullControl.")]
    public async Task AttendedFullControlViewer_CanRelayControlDto()
    {
        var session = NewSession(RemoteAccessMode.FullControl);
        session.Mode = RemoteControlMode.Attended;

        var hub = CreateHub(session, viewerViewOnly: false);

        await hub.SendDtoToClient(Serialize(DtoType.MouseMove));

        AssertDesktopReceivedDto();
    }

    [TestMethod]
    [Description("FR-003: when the per-viewer mode was never recorded, the relay fails closed.")]
    public async Task ViewerWithoutRecordedMode_CannotRelayControlDto()
    {
        var hub = CreateHub(
            session: NewSession(RemoteAccessMode.FullControl),
            viewerViewOnly: null);

        await hub.SendDtoToClient(Serialize(DtoType.MouseMove));

        AssertDesktopDidNotReceiveDto();
    }

    [TestMethod]
    [Description("Regression guard: a ViewOnly session refuses control traffic regardless of the viewer mode.")]
    public async Task ViewOnlySession_CannotRelayControlDto()
    {
        var hub = CreateHub(
            session: NewSession(RemoteAccessMode.ViewOnly),
            viewerViewOnly: false);

        await hub.SendDtoToClient(Serialize(DtoType.KeyDown));

        AssertDesktopDidNotReceiveDto();
    }

    [TestMethod]
    [Description("A viewer with no attached session is never relayed to a device.")]
    public async Task UnattachedViewer_CannotRelayAnything()
    {
        var session = NewSession(RemoteAccessMode.FullControl);
        session.DesktopConnectionId = string.Empty;

        var hub = CreateHub(session, viewerViewOnly: false);

        await hub.SendDtoToClient(Serialize(DtoType.MouseMove));

        AssertDesktopDidNotReceiveDto();
    }

    [TestMethod]
    [Description(
        "FR-011: the refusal notice is sent once per connection. SignalR builds a " +
        "new hub instance per invocation, so the flag must live on the connection; " +
        "otherwise every mouse move produces another message.")]
    public async Task ViewOnlyViewer_IsWarnedOnlyOncePerConnection()
    {
        var session = NewSession(RemoteAccessMode.FullControl);

        // Two hub instances over the same connection state, exactly as SignalR
        // would do for two consecutive invocations.
        var firstInvocation = CreateHub(session, viewerViewOnly: true);
        var secondInvocation = CreateHub(session, viewerViewOnly: true);

        await firstInvocation.SendDtoToClient(Serialize(DtoType.MouseMove));
        await secondInvocation.SendDtoToClient(Serialize(DtoType.KeyDown));

        _caller.Verify(
            x => x.ShowMessage("Input blocked: session is View Only."),
            Times.Once,
            "The viewer must be told once, not once per input event.");
        AssertDesktopDidNotReceiveDto();
    }

    [TestMethod]
    [Description(
        "FR-010/FR-015: a viewer narrowed at attach cannot use the Ctrl+Alt+Del " +
        "action even when the session-wide basis would otherwise allow it.")]
    public async Task ViewOnlyViewer_CannotInvokeCtrlAltDel_WhenBasisIsOtherwiseValid()
    {
        var session = NewSession(RemoteAccessMode.FullControl);
        var hub = CreateHub(session, viewerViewOnly: true, authenticated: true);
        AllowControlBasis(session);

        await hub.InvokeCtrlAltDel();

        _agentHubFixture.SingleClientProxyMock.Verify(
            x => x.InvokeCtrlAltDel(),
            Times.Never,
            "The device must not be asked to send Ctrl+Alt+Del for a View Only viewer.");
    }

    [TestMethod]
    [Description("Regression guard: a FullControl viewer still reaches the Ctrl+Alt+Del action.")]
    public async Task FullControlViewer_CanInvokeCtrlAltDel_WhenBasisIsValid()
    {
        var session = NewSession(RemoteAccessMode.FullControl);
        var hub = CreateHub(session, viewerViewOnly: false, authenticated: true);
        AllowControlBasis(session);

        await hub.InvokeCtrlAltDel();

        _agentHubFixture.SingleClientProxyMock.Verify(
            x => x.InvokeCtrlAltDel(),
            Times.Once,
            "A control viewer with a valid basis should reach the device.");
    }

    [TestMethod]
    [Description(
        "FR-015: withdrawing the device-group membership of an attached viewer must " +
        "stop control. The relay re-evaluates the basis, so the next control message " +
        "is refused instead of waiting for a reconnect.")]
    public async Task ControlViewer_WithdrawnMembership_CannotRelayControlDto()
    {
        var session = NewSession(RemoteAccessMode.FullControl);

        AllowControlBasis(session);
        var firstInvocation = CreateHub(session, viewerViewOnly: false, authenticated: true);
        await firstInvocation.SendDtoToClient(Serialize(DtoType.MouseMove));
        AssertDesktopReceivedDto();

        // The user is removed from the device group while the session is open.
        _userService
            .Setup(x => x.DoesUserHaveAccessToDevice(session.DeviceId, It.IsAny<RemotelyUser>()))
            .Returns(false);

        // The per-connection recheck cache has a short window; expire it so the
        // next message performs the evaluation.
        _items[ControlAuthorizationCheckedAtKey] = DateTimeOffset.UtcNow.AddMinutes(-1);

        var nextInvocation = CreateHub(session, viewerViewOnly: false, authenticated: true);
        await nextInvocation.SendDtoToClient(Serialize(DtoType.KeyDown));

        AssertDesktopReceivedDtoTimes(1);
        _caller.Verify(
            x => x.ShowMessage("Input blocked: control authority was withdrawn."),
            Times.Once,
            "The viewer must be told that control authority was withdrawn.");
    }

    [TestMethod]
    [Description(
        "Withdrawing control authority stops input but keeps the visual stream, " +
        "so the viewer can still see the device it is watching.")]
    public async Task ControlViewer_WithdrawnMembership_CanStillRelayViewTraffic()
    {
        var session = NewSession(RemoteAccessMode.FullControl);

        AllowControlBasis(session);
        var firstInvocation = CreateHub(session, viewerViewOnly: false, authenticated: true);
        await firstInvocation.SendDtoToClient(Serialize(DtoType.MouseMove));

        _userService
            .Setup(x => x.DoesUserHaveAccessToDevice(session.DeviceId, It.IsAny<RemotelyUser>()))
            .Returns(false);
        _items[ControlAuthorizationCheckedAtKey] = DateTimeOffset.UtcNow.AddMinutes(-1);

        var nextInvocation = CreateHub(session, viewerViewOnly: false, authenticated: true);
        await nextInvocation.SendDtoToClient(Serialize(DtoType.KeyDown));
        await nextInvocation.SendDtoToClient(Serialize(DtoType.ScreenData));

        // One control message before the withdrawal, then only the view traffic.
        AssertDesktopReceivedDtoTimes(2);
    }

    [TestMethod]
    [Description(
        "The control basis is not queried for every input event: the short " +
        "per-connection cache keeps the relay off the database on the hot path.")]
    public async Task ControlBasis_IsNotQueriedForEveryMessage()
    {
        var session = NewSession(RemoteAccessMode.FullControl);

        AllowControlBasis(session);

        var firstInvocation = CreateHub(session, viewerViewOnly: false, authenticated: true);
        await firstInvocation.SendDtoToClient(Serialize(DtoType.MouseMove));

        var secondInvocation = CreateHub(session, viewerViewOnly: false, authenticated: true);
        await secondInvocation.SendDtoToClient(Serialize(DtoType.MouseMove));

        _userService.Verify(
            x => x.DoesUserHaveAccessToDevice(session.DeviceId, It.IsAny<RemotelyUser>()),
            Times.Once,
            "Two messages inside the cache window must share one evaluation.");
        AssertDesktopReceivedDtoTimes(2);
    }

    [TestMethod]
    [Description(
        "Once the control basis is withdrawn for a connection, restoring the " +
        "membership must not restore control: the narrowing is permanent for " +
        "the lifetime of that connection, and the notice is sent only once.")]
    public async Task ControlViewer_RevokedBasis_StaysRevokedWhenMembershipReturns()
    {
        var session = NewSession(RemoteAccessMode.FullControl);

        AllowControlBasis(session);
        var firstInvocation = CreateHub(session, viewerViewOnly: false, authenticated: true);
        await firstInvocation.SendDtoToClient(Serialize(DtoType.MouseMove));

        _userService
            .Setup(x => x.DoesUserHaveAccessToDevice(session.DeviceId, It.IsAny<RemotelyUser>()))
            .Returns(false);
        _items[ControlAuthorizationCheckedAtKey] = DateTimeOffset.UtcNow.AddMinutes(-1);

        var revocationInvocation = CreateHub(session, viewerViewOnly: null, authenticated: true);
        await revocationInvocation.SendDtoToClient(Serialize(DtoType.KeyDown));

        // The membership is granted again while the connection stays open.
        _userService
            .Setup(x => x.DoesUserHaveAccessToDevice(session.DeviceId, It.IsAny<RemotelyUser>()))
            .Returns(true);
        _items[ControlAuthorizationCheckedAtKey] = DateTimeOffset.UtcNow.AddMinutes(-1);

        var afterRestore = CreateHub(session, viewerViewOnly: null, authenticated: true);
        await afterRestore.SendDtoToClient(Serialize(DtoType.KeyDown));
        await afterRestore.SendDtoToClient(Serialize(DtoType.MouseMove));

        // Only the message before the withdrawal reached the device.
        AssertDesktopReceivedDtoTimes(1);
        _caller.Verify(
            x => x.ShowMessage("Input blocked: control authority was withdrawn."),
            Times.Once,
            "The withdrawal notice must be sent once, not for every blocked message.");
    }

    private void AllowControlBasis(RemoteControlSession session)
    {
        var user = new RemotelyUser { UserName = "admin@test.com" };

        _userService
            .Setup(x => x.GetUserByName("admin@test.com"))
            .ReturnsAsync(Result.Ok(user));
        _userService
            .Setup(x => x.DoesUserHaveAccessToDevice(session.DeviceId, user))
            .Returns(true);
    }

    private ViewerHub CreateHub(
        RemoteControlSession session,
        bool? viewerViewOnly,
        bool authenticated = false)
    {
        _items[SessionInfoKey] = session;
        if (viewerViewOnly.HasValue)
        {
            _items[ViewerViewOnlyKey] = viewerViewOnly.Value;
        }

        if (authenticated)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin@test.com")],
                "TestAuth");
            _hubCallerContext
                .SetupGet(x => x.User)
                .Returns(new ClaimsPrincipal(identity));
        }

        return new ViewerHub(
            _sessionCache.Object,
            _streamCache.Object,
            _agentHubFixture.HubContextMock.Object,
            _desktopHubFixture.HubContextMock.Object,
            _recordingSink.Object,
            _settingsService.Object,
            _userService.Object,
            _logger.Object)
        {
            Context = _hubCallerContext.Object,
            Clients = _callerClients.Object
        };
    }

    private static RemoteControlSession NewSession(RemoteAccessMode accessMode)
    {
        return new RemoteControlSession
        {
            Mode = RemoteControlMode.Unattended,
            AccessMode = accessMode,
            DesktopConnectionId = "desktop-connection",
            AgentConnectionId = "agent-connection"
        };
    }

    private static byte[] Serialize(DtoType dtoType)
    {
        return MessagePackSerializer.Serialize(new DtoWrapper { DtoType = dtoType });
    }

    private void AssertDesktopReceivedDto()
    {
        _desktopHubFixture.SingleClientProxyMock.Verify(
            x => x.SendDtoToClient(It.IsAny<byte[]>(), It.IsAny<string>()),
            Times.Once,
            "The device should have received the relayed DTO.");
    }

    private void AssertDesktopReceivedDtoTimes(int times)
    {
        _desktopHubFixture.SingleClientProxyMock.Verify(
            x => x.SendDtoToClient(It.IsAny<byte[]>(), It.IsAny<string>()),
            Times.Exactly(times),
            "The device should have received exactly the permitted DTOs.");
    }

    private void AssertDesktopDidNotReceiveDto(string? detail = null)
    {
        _desktopHubFixture.SingleClientProxyMock.Verify(
            x => x.SendDtoToClient(It.IsAny<byte[]>(), It.IsAny<string>()),
            Times.Never,
            $"The device must not receive this DTO.  {detail}".Trim());
    }
}
