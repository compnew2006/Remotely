using Remotely.Server.Hubs;
using Remotely.Server.Services;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Shared;
using Remotely.Shared.Extensions;
using Remotely.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class AgentHubTests
{
    private TestData _testData = null!;
    private ISettingsService _settingsService = null!;
    private IDeviceService _deviceService = null!;
    private IScriptResultService _scriptResultService = null!;
    private IUserService _userService = null!;

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_BannedByName()
    {
        var circuitManager = new Mock<ICircuitManager>();
        var circuitConnection = new Mock<ICircuitConnection>();
        circuitManager.Setup(x => x.Connections).Returns(new[] { circuitConnection.Object });
        circuitConnection.Setup(x => x.User).Returns(_testData.Org1Admin1);
        var viewerHub = new Mock<IHubContext<ViewerHub>>();
        var expiringTokenService = new Mock<IExpiringTokenService>();
        var serviceSessionCache = new Mock<IAgentHubSessionCache>();
        var remoteControlSessions = new Mock<IRemoteControlSessionCache>();
        var keyboardCaptureManager = new Mock<IKeyboardCaptureManager>();
        var updateSigningService = new Mock<IUpdateSigningService>();
        var apiScriptResultCache = new Mock<IApiScriptResultCache>();
        var messenger = new Mock<IMessenger>();
        var logger = new Mock<ILogger<AgentHub>>();

        var settings = await _settingsService.GetSettings();
        settings.BannedDevices = [_testData.Org1Device1.DeviceName!];
        await _settingsService.SaveSettings(settings);

        var hub = new AgentHub(
            _deviceService,
            _scriptResultService,
            _settingsService,
            _userService,
            serviceSessionCache.Object,
            viewerHub.Object,
            circuitManager.Object,
            expiringTokenService.Object,
            remoteControlSessions.Object,
            keyboardCaptureManager.Object,
            updateSigningService.Object,
            apiScriptResultCache.Object,
            messenger.Object,
            logger.Object);

        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        var caller = new Mock<IAgentHubClient>();
        hubClients.Setup(x => x.Caller).Returns(caller.Object);
        hub.Clients = hubClients.Object;

        var result = await hub.DeviceCameOnline(_testData.Org1Device1.ToDto());
        Assert.IsFalse(result);
        hubClients.Verify(x => x.Caller, Times.Once);
        caller.Verify(x => x.UninstallAgent(), Times.Once);
    }

    // TODO: Checking of device ban should be pulled out into
    // a separate service that's better testable.
    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_BannedById()
    {
        var circuitManager = new Mock<ICircuitManager>();
        var circuitConnection = new Mock<ICircuitConnection>();
        circuitManager.Setup(x => x.Connections).Returns(new[] { circuitConnection.Object });
        circuitConnection.Setup(x => x.User).Returns(_testData.Org1Admin1);
        var viewerHub = new Mock<IHubContext<ViewerHub>>();
        var expiringTokenService = new Mock<IExpiringTokenService>();
        var serviceSessionCache = new Mock<IAgentHubSessionCache>();
        var remoteControlSessions = new Mock<IRemoteControlSessionCache>();
        var keyboardCaptureManager = new Mock<IKeyboardCaptureManager>();
        var updateSigningService = new Mock<IUpdateSigningService>();
        var apiScriptResultCache = new Mock<IApiScriptResultCache>();
        var messenger = new Mock<IMessenger>();
        var logger = new Mock<ILogger<AgentHub>>();


        var settings = await _settingsService.GetSettings();
        settings.BannedDevices = [$"{_testData.Org1Device1.ID}"];
        await _settingsService.SaveSettings(settings);

        var hub = new AgentHub(
            _deviceService,
            _scriptResultService,
            _settingsService,
            _userService,
            serviceSessionCache.Object,
            viewerHub.Object,
            circuitManager.Object,
            expiringTokenService.Object,
            remoteControlSessions.Object,
            keyboardCaptureManager.Object,
            updateSigningService.Object,
            apiScriptResultCache.Object,
            messenger.Object,
            logger.Object);

        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        var caller = new Mock<IAgentHubClient>();
        hubClients.Setup(x => x.Caller).Returns(caller.Object);
        hub.Clients = hubClients.Object;

        var result = await hub.DeviceCameOnline(_testData.Org1Device1.ToDto());
        Assert.IsFalse(result);
        hubClients.Verify(x => x.Caller, Times.Once);
        caller.Verify(x => x.UninstallAgent(), Times.Once);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_RefusesMismatchedVerificationToken()
    {
        var hub = CreateHub(out var sessionCache);

        // The record already holds a token, so an agent that presents a
        // different one is not this device.
        _deviceService.SetServerVerificationToken(_testData.Org1Device1.ID, "bound-token");

        SetConnectionHeaders(hub, "wrong-token");

        var result = await hub.DeviceCameOnline(_testData.Org1Device1.ToDto());

        Assert.IsFalse(result, "A mismatched device token must not register the device.");
        sessionCache.Verify(
            x => x.AddOrUpdateByConnectionId(It.IsAny<string>(), It.IsAny<Remotely.Shared.Entities.Device>()),
            Times.Never,
            "A refused registration must not publish the device as online.");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_AcceptsMatchingVerificationToken()
    {
        var hub = CreateHub(out _);

        _deviceService.SetServerVerificationToken(_testData.Org1Device1.ID, "bound-token");

        SetConnectionHeaders(hub, "bound-token");

        var result = await hub.DeviceCameOnline(_testData.Org1Device1.ToDto());

        Assert.IsTrue(result, "The agent that holds the bound token must register.");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_BindsPresentedTokenOnFirstRegistration()
    {
        var hub = CreateHub(out _);

        var deviceDto = _testData.Org1Device1.ToDto();
        deviceDto.ID = Guid.NewGuid().ToString();

        SetConnectionHeaders(hub, "first-token");

        var result = await hub.DeviceCameOnline(deviceDto);

        Assert.IsTrue(result, "A device with no record yet must register on the first attempt.");

        // The token is bound during that first registration, so a different
        // token is refused from then on.  (A missing record would answer true
        // to any token, so this also proves the record now exists.)
        Assert.IsTrue(
            await _deviceService.VerifyDeviceVerificationToken(deviceDto.ID, "first-token"),
            "The presented token must be bound to the new device record.");
        Assert.IsFalse(
            await _deviceService.VerifyDeviceVerificationToken(deviceDto.ID, "other-token"),
            "A different token must not be accepted after the first registration.");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_RefusesRegistrationWithoutAnyToken()
    {
        var hub = CreateHub(out var sessionCache);

        _deviceService.SetServerVerificationToken(_testData.Org1Device1.ID, "bound-token");

        // No header at all.  This is the bypass a caller would try after
        // learning a device ID and an organization ID, so the refusal must
        // not depend on the header merely being wrong.
        SetConnectionHeaders(hub, deviceToken: null);

        var result = await hub.DeviceCameOnline(_testData.Org1Device1.ToDto());

        Assert.IsFalse(result, "A bound device must not register without presenting its token.");
        sessionCache.Verify(
            x => x.AddOrUpdateByConnectionId(It.IsAny<string>(), It.IsAny<Remotely.Shared.Entities.Device>()),
            Times.Never,
            "A refused registration must not publish the device as online.");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task SetServerVerificationToken_BeforeRegistration_DoesNotRotateTheBoundToken()
    {
        var hub = CreateHub(out _);

        _deviceService.SetServerVerificationToken(_testData.Org1Device1.ID, "bound-token");
        SetConnectionHeaders(hub, "attacker-token");

        // The endpoint is reachable on the unauthenticated agent hub, so it
        // must do nothing until this connection has proven who it is.
        hub.SetServerVerificationToken("attacker-token");

        Assert.AreEqual(
            string.Empty,
            hub.GetServerVerificationToken(),
            "An unregistered connection has no device and therefore no token.");

        Assert.IsTrue(
            await _deviceService.VerifyDeviceVerificationToken(_testData.Org1Device1.ID, "bound-token"),
            "The bound token must be unchanged.");
        Assert.IsFalse(
            await _deviceService.VerifyDeviceVerificationToken(_testData.Org1Device1.ID, "attacker-token"),
            "An unregistered connection must not rotate the device token.");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceHeartbeat_FromAnotherConnection_DoesNotTakeOverTheDevice()
    {
        var cache = new AgentHubSessionCache();

        var owner = CreateHub(cache);
        SetConnectionHeaders(owner, "bound-token", "connection-owner");
        _deviceService.SetServerVerificationToken(_testData.Org1Device1.ID, "bound-token");
        Assert.IsTrue(await owner.DeviceCameOnline(_testData.Org1Device1.ToDto()));

        var other = CreateHub(cache);
        SetConnectionHeaders(other, "bound-token", "connection-other");
        await other.DeviceHeartbeat(_testData.Org1Device1.ToDto());

        Assert.IsTrue(cache.TryGetConnectionId(_testData.Org1Device1.ID, out var current));
        Assert.AreEqual(
            "connection-owner",
            current,
            "A heartbeat from a connection that does not own the device must not take it over.");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task OnDisconnectedAsync_CurrentConnection_MarksTheDeviceOffline()
    {
        var cache = new AgentHubSessionCache();

        var hub = CreateHub(cache);
        SetConnectionHeaders(hub, "bound-token", "connection-current");
        _deviceService.SetServerVerificationToken(_testData.Org1Device1.ID, "bound-token");
        Assert.IsTrue(await hub.DeviceCameOnline(_testData.Org1Device1.ToDto()));

        await hub.OnDisconnectedAsync(null);

        var device = (await _deviceService.GetDevice(_testData.Org1Device1.ID)).Value!;
        Assert.IsFalse(device.IsOnline, "The owner's disconnect must mark the device offline.");
        Assert.IsFalse(cache.TryGetConnectionId(_testData.Org1Device1.ID, out _));
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task OnDisconnectedAsync_ReplacedConnection_LeavesTheNewOwnerOnline()
    {
        var cache = new AgentHubSessionCache();

        var first = CreateHub(cache);
        SetConnectionHeaders(first, "bound-token", "connection-first");
        _deviceService.SetServerVerificationToken(_testData.Org1Device1.ID, "bound-token");
        Assert.IsTrue(await first.DeviceCameOnline(_testData.Org1Device1.ToDto()));

        // A newer connection registers the same device and takes ownership.
        var second = CreateHub(cache);
        SetConnectionHeaders(second, "bound-token", "connection-second");
        Assert.IsTrue(await second.DeviceCameOnline(_testData.Org1Device1.ToDto()));

        // The replaced connection then drops.  Its disconnect must not remove
        // the newer mapping or report the device as offline.
        await first.OnDisconnectedAsync(null);

        Assert.IsTrue(cache.TryGetConnectionId(_testData.Org1Device1.ID, out var current));
        Assert.AreEqual(
            "connection-second",
            current,
            "The replacement connection must remain the device owner.");

        var device = (await _deviceService.GetDevice(_testData.Org1Device1.ID)).Value!;
        Assert.IsTrue(
            device.IsOnline,
            "A replaced connection's disconnect must not mark the device offline.");
    }

    private AgentHub CreateHub(out Mock<IAgentHubSessionCache> sessionCache)
    {
        sessionCache = new Mock<IAgentHubSessionCache>();
        return CreateHub(sessionCache.Object);
    }

    private AgentHub CreateHub(IAgentHubSessionCache serviceSessionCache)
    {
        var circuitManager = new Mock<ICircuitManager>();
        circuitManager.Setup(x => x.Connections).Returns(Array.Empty<ICircuitConnection>());
        var viewerHub = new Mock<IHubContext<ViewerHub>>();
        var expiringTokenService = new Mock<IExpiringTokenService>();
        var remoteControlSessions = new Mock<IRemoteControlSessionCache>();
        var keyboardCaptureManager = new Mock<IKeyboardCaptureManager>();
        var updateSigningService = new Mock<IUpdateSigningService>();
        var apiScriptResultCache = new Mock<IApiScriptResultCache>();
        var messenger = new Mock<IMessenger>();
        var logger = new Mock<ILogger<AgentHub>>();

        var hub = new AgentHub(
            _deviceService,
            _scriptResultService,
            _settingsService,
            _userService,
            serviceSessionCache,
            viewerHub.Object,
            circuitManager.Object,
            expiringTokenService.Object,
            remoteControlSessions.Object,
            keyboardCaptureManager.Object,
            updateSigningService.Object,
            apiScriptResultCache.Object,
            messenger.Object,
            logger.Object);

        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        hubClients.Setup(x => x.Caller).Returns(new Mock<IAgentHubClient>().Object);
        hub.Clients = hubClients.Object;

        return hub;
    }

    /// <summary>
    /// Gives the hub connection the HTTP request that carries the agent's
    /// device verification header.  A SignalR connection exposes it through
    /// <see cref="IHttpContextFeature"/>.  A null token leaves the header off
    /// the request entirely, which is what an agent that never received a
    /// token sends.
    /// </summary>
    private static void SetConnectionHeaders(
        AgentHub hub,
        string? deviceToken,
        string connectionId = "test-connection")
    {
        var httpContext = new DefaultHttpContext();
        if (deviceToken is not null)
        {
            httpContext.Request.Headers[AppConstants.DeviceVerificationTokenHeaderName] = deviceToken;
        }

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new TestHttpContextFeature
        {
            HttpContext = httpContext
        });

        var callerContext = new Mock<HubCallerContext>();
        callerContext.SetupGet(x => x.Features).Returns(features);
        callerContext.SetupGet(x => x.Items).Returns(new Dictionary<object, object?>());
        callerContext.SetupGet(x => x.ConnectionId).Returns(connectionId);

        hub.Context = callerContext.Object;
    }

    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _testData.ClearData();
    }

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _settingsService = IoCActivator.ServiceProvider.GetRequiredService<ISettingsService>();
        _deviceService = IoCActivator.ServiceProvider.GetRequiredService<IDeviceService>();
        _scriptResultService = IoCActivator.ServiceProvider.GetRequiredService<IScriptResultService>();
        _userService = IoCActivator.ServiceProvider.GetRequiredService<IUserService>();
    }
}
