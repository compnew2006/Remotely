using Remotely.Server.Hubs;
using Remotely.Server.Services;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Shared.Extensions;
using Remotely.Shared.Interfaces;
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
