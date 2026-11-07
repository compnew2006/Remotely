using Remotely.Server.Services;
using Remotely.Shared.Helpers;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Models;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services.Stores;
using Remotely.Shared;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;
using System.Collections.Concurrent;

namespace Remotely.Server.Hubs;

public interface ICircuitConnection
{
    string ConnectionId { get; }

    RemotelyUser User { get; }

    Task DeleteRemoteLogs(string deviceId);

    Task ExecuteCommandOnAgent(ScriptingShell shell, string command, string[] deviceIDs);

    Task GetPowerShellCompletions(string inputText, int currentIndex, CompletionIntent intent, bool? forward);

    Task GetRemoteLogs(string deviceId);

    Task ReinstallAgents(string[] deviceIDs);

    Task<Result<RemoteControlSession>> RemoteControl(string deviceID, bool viewOnly);

    Task<Result> StartMonitoring(string deviceId, int intervalMinutes);

    Task<Result> CaptureScreenshotNow(string deviceId);

    Task<Result> StopMonitoring(string deviceId);

    Task<Result> UpdateScreenshotSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null);

    Task<Result> DeleteScreenshot(string deviceId, string fileName);

    Task<Result> ClearScreenshots(string deviceId);

    /// <summary>
    /// Asks a device to record a video clip from one of its cameras or
    /// displays.
    /// </summary>
    Task<Result> StartCameraCapture(
        string deviceId,
        CameraCaptureRequest request);

    /// <summary>
    /// Stops a camera recording that is running on a device.
    /// </summary>
    Task<Result> StopCameraCapture(string deviceId);

    Task<Result> StartCameraMonitoring(
        string deviceId,
        CameraCaptureRequest request,
        int intervalMinutes);

    Task<Result> StopCameraMonitoring(string deviceId);

    CameraMonitoringStatus? GetCameraMonitoringStatus(string deviceId);

    Task<Result> UpdateCameraSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null);

    /// <summary>
    /// Returns the cameras and the displays that the device reported the last
    /// time it was asked to record.
    /// </summary>
    CameraDeviceInfo[] GetCameraDevices(string deviceId);

    /// <summary>
    /// Returns the microphones and the loopback devices that the device
    /// reported the last time it was asked to record.
    /// </summary>
    CameraAudioDeviceInfo[] GetCameraAudioDevices(string deviceId);

    Task<Result> DeleteCameraClip(string deviceId, string fileName);

    Task<Result> ClearCameraClips(string deviceId);

    /// <summary>
    /// Asks a device to record an audio clip from its system audio or
    /// microphone.
    /// </summary>
    Task<Result> StartAudioCapture(
        string deviceId,
        AudioSource source,
        string microphoneId,
        int quality,
        int durationSeconds);

    /// <summary>
    /// Stops an audio recording that is running on a device.
    /// </summary>
    Task<Result> StopAudioCapture(string deviceId);

    Task<Result> StartAudioMonitoring(
        string deviceId,
        AudioSource source,
        string microphoneId,
        int quality,
        int durationSeconds,
        int intervalMinutes);

    Task<Result> StopAudioMonitoring(string deviceId);

    AudioMonitoringStatus? GetAudioMonitoringStatus(string deviceId);

    Task<Result> UpdateAudioSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null);

    /// <summary>
    /// Returns the microphones that the device reported the last time it was
    /// asked to record.
    /// </summary>
    AudioDeviceInfo[] GetAudioDevices(string deviceId);

    Task<Result> DeleteAudioClip(string deviceId, string fileName);

    Task<Result> ClearAudioClips(string deviceId);

    /// <summary>
    /// Starts a live audio stream from a device to the requesting browser.
    /// </summary>
    Task<Result> StartAudioLive(
        string deviceId,
        AudioSource source,
        string microphoneId);

    /// <summary>
    /// Stops the live audio stream of a device.
    /// </summary>
    Task<Result> StopAudioLive(string deviceId);

    /// <summary>
    /// Turns the keyboard capture of a device on or off.
    /// </summary>
    Task<Result> SetKeyboardEnabled(string deviceId, bool enabled);

    /// <summary>
    /// Returns the captured text of a device, newest first.
    /// </summary>
    KeyboardLog[] GetKeyboardLogs(string deviceId);

    Task<Result> DeleteKeyboardLog(string deviceId, string logId);

    Task<Result> ClearKeyboardLogs(string deviceId);

    Task<Result> UpdateKeyboardSettings(
        string deviceId,
        bool enabled,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null);

    Task RemoveDevices(string[] deviceIDs);

    Task RunScript(IEnumerable<string> deviceIds, Guid savedScriptId, int scriptRunId, ScriptInputType scriptInputType, bool runAsHostedService);

    Task SendChat(string message, string deviceId, bool isDisconnecting = false);
    Task<bool> TransferFileFromBrowserToAgent(string deviceId, string transferId, string[] fileIds);

    Task TriggerHeartbeat(string deviceId);

    Task UninstallAgents(string[] deviceIDs);
    Task UpdateTags(string deviceID, string tags);

    /// <summary>
    /// Sends a Wake-On-LAN request for the specified device to its peer devices.
    /// Peer devices are those in the same group or the same public IP.
    /// </summary>
    /// <param name="device"></param>
    Task<Result> WakeDevice(Device device);

    /// <summary>
    /// Sends a Wake-On-LAN request for the specified device to its peer devices.
    /// Peer devices are those in the same group or the same public IP.
    /// </summary>
    /// <param name="devices"></param>
    Task<Result> WakeDevices(Device[] devices);
}

public class CircuitConnection : CircuitHandler, ICircuitConnection
{
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHubContext;
    private readonly IHubContext<DesktopHub, IDesktopHubClient> _desktopHubContext;
    private readonly IDataService _dataService;
    private readonly ISelectedCardsStore _cardStore;
    private readonly IAuthService _authService;
    private readonly ICircuitManager _circuitManager;
    private readonly IRemoteControlSessionCache _remoteControlSessionCache;
    private readonly IScreenshotService _screenshotService;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<CircuitConnection> _logger;
    private readonly IAgentHubSessionCache _agentSessionCache;
    private readonly ICameraService _cameraService;
    private readonly IAudioService _audioService;
    private readonly IKeyboardLogService _keyboardLogService;
    private readonly IKeyboardCaptureManager _keyboardCaptureManager;
    private readonly IMessenger _messenger;
    private readonly IToastService _toastService;
    private RemotelyUser? _user;

    public CircuitConnection(
        IAuthService authService,
        IDataService dataService,
        ISelectedCardsStore cardStore,
        IHubContext<AgentHub, IAgentHubClient> agentHubContext,
        IHubContext<DesktopHub, IDesktopHubClient> desktopHubContext,
        ICircuitManager circuitManager,
        IToastService toastService,
        IExpiringTokenService expiringTokenService,
        IRemoteControlSessionCache remoteControlSessionCache,
        IScreenshotService screenshotService,
        ICameraService cameraService,
        IAudioService audioService,
        IKeyboardLogService keyboardLogService,
        IKeyboardCaptureManager keyboardCaptureManager,
        IAgentHubSessionCache agentSessionCache,
        IMessenger messenger,
        ILogger<CircuitConnection> logger)
    {
        _dataService = dataService;
        _agentHubContext = agentHubContext;
        _desktopHubContext = desktopHubContext;
        _cardStore = cardStore;
        _authService = authService;
        _circuitManager = circuitManager;
        _toastService = toastService;
        _expiringTokenService = expiringTokenService;
        _remoteControlSessionCache = remoteControlSessionCache;
        _screenshotService = screenshotService;
        _cameraService = cameraService;
        _audioService = audioService;
        _keyboardLogService = keyboardLogService;
        _keyboardCaptureManager = keyboardCaptureManager;
        _agentSessionCache = agentSessionCache;
        _messenger = messenger;
        _logger = logger;
    }


    public string ConnectionId { get; } = Guid.NewGuid().ToString();

    public RemotelyUser User
    {
        get => _user ?? throw new InvalidOperationException("User is not set.");
        internal set => _user = value;
    }


    public Task DeleteRemoteLogs(string deviceId)
    {
        var (canAccess, key) = CanAccessDevice(deviceId);
        if (!canAccess)
        {
            _toastService.ShowToast("Access denied.", classString: "bg-warning");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Delete logs command sent.  Device: {deviceId}.  User: {username}",
            deviceId,
            User?.UserName);

        return _agentHubContext.Clients.Client(key).DeleteLogs();
    }

    public async Task ExecuteCommandOnAgent(ScriptingShell shell, string command, string[] deviceIDs)
    {
        deviceIDs = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
        var connections = GetActiveConnectionsForUserOrg(deviceIDs);

        _logger.LogInformation("Command executed by {username}.  Shell: {shell}.  Command: {command}.  Devices: {deviceIds}",
              User.UserName,
              shell,
              command,
              string.Join(", ", deviceIDs));

        var authTokenForUploadingResults = _expiringTokenService.GetToken(Time.Now.AddMinutes(5));

        await _agentHubContext.Clients.Clients(connections).ExecuteCommand(
            shell,
            command,
            authTokenForUploadingResults,
            $"{User.UserName}",
            ConnectionId);
    }

    public Task GetPowerShellCompletions(string inputText, int currentIndex, CompletionIntent intent, bool? forward)
    {
        var device = _cardStore.SelectedDevices.FirstOrDefault();
        if (device is null)
        {
            return Task.CompletedTask;
        }

        var (canAccess, key) = CanAccessDevice(device);
        if (!canAccess)
        {
            return Task.CompletedTask;
        }

        return _agentHubContext.Clients.Client(key).GetPowerShellCompletions(
            inputText,
            currentIndex,
            intent,
            forward,
            ConnectionId);
    }

    public Task GetRemoteLogs(string deviceId)
    {
        var (canAccess, key) = CanAccessDevice(deviceId);
        if (!canAccess)
        {
            _toastService.ShowToast("Access denied.", classString: "bg-warning");
            return Task.CompletedTask;
        }

        return _agentHubContext.Clients.Client(key).GetLogs(ConnectionId);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ConnectionId))
        {
            _circuitManager.TryRemoveConnection(ConnectionId, out _);
        }
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (await _authService.IsAuthenticated())
        {
            var userResult = await _authService.GetUser();
            if (!userResult.IsSuccess)
            {
                _toastService.ShowToast2("Authorization failure.", Enums.ToastType.Error);
                return;
            }
            User = userResult.Value;
            _circuitManager.TryAddConnection(ConnectionId, this);
        }
        await base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public async Task ReinstallAgents(string[] deviceIDs)
    {
        deviceIDs = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
        var connections = GetActiveConnectionsForUserOrg(deviceIDs);
        await _agentHubContext.Clients.Clients(connections).ReinstallAgent();
        _dataService.RemoveDevices(deviceIDs);
    }

    public async Task<Result<RemoteControlSession>> RemoteControl(string deviceId, bool viewOnly)
    {
        var settings = await _dataService.GetSettings();

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            var message = new DisplayNotificationMessage(
                 "The selected device is not online.",
                 "Device is not online.",
                 "bg-warning");

            await _messenger.Send(message, ConnectionId);

            return Result.Fail<RemoteControlSession>("Device is not online.");
        }


        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            var device = _dataService.GetDevice(targetDevice.ID);
            _logger.LogWarning(
                "Remote control attempted by unauthorized user.  Device ID: {deviceId}.  User Name: {userName}.",
                deviceId,
                User.UserName);
            return Result.Fail<RemoteControlSession>("Unauthorized.");

        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            var message = new DisplayNotificationMessage(
                "Service connection not found.",
                "Service connection not found.",
                "bg-warning");
            
            await _messenger.Send(message, ConnectionId);
            return Result.Fail<RemoteControlSession>("Service connection not found.");
        }

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,

            // SPEC-001 / FR-005, FR-008: FullControl is granted only through this
            // explicit, authenticated UI request (device-group access is checked
            // above and the organization scope is the circuit user's). The
            // decision is recorded below; viewer links and URL parameters never
            // grant authority (FR-006).
            AccessMode = viewOnly
                ? Shared.Enums.RemoteAccessMode.ViewOnly
                : Shared.Enums.RemoteAccessMode.FullControl,
            OrganizationId = User.OrganizationID,
            RequireConsent = settings.EnforceAttendedAccess,
            NotifyUserOnStart = settings.RemoteControlNotifyUser
        };

        _logger.LogInformation(
            "Privilege decision: GRANTED.  " +
            "Action: StartRemoteControl.  " +
            "Requested Mode: {requestedMode}.  " +
            "Applied Mode: {accessMode}.  " +
            "Session ID: {sessionId}.  " +
            "Device ID: {deviceId}.  " +
            "Identity: {identity}.",
            viewOnly ? Shared.Enums.RemoteAccessMode.ViewOnly : Shared.Enums.RemoteAccessMode.FullControl,
            session.AccessMode,
            sessionId,
            deviceId,
            User.UserName);

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");

        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail<RemoteControlSession>(orgResult.Reason);
        }

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok(session);
    }

    /// <summary>
    /// Starts a monitoring session for a device.  The device captures periodic
    /// screenshots of its desktop with no viewer attached, and the screenshots
    /// are stored on the server and pushed to this circuit.
    /// </summary>
    public async Task<Result> StartMonitoring(string deviceId, int intervalMinutes)
    {
        if (intervalMinutes < 1 || intervalMinutes > 30)
        {
            return Result.Fail("The monitoring interval must be between 1 and 30 minutes.");
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            return Result.Fail("Device is not online.");
        }

        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            _logger.LogWarning(
                "Monitoring attempted by unauthorized user.  Device ID: {deviceId}.  User Name: {userName}.",
                deviceId,
                User.UserName);

            return Result.Fail("Unauthorized.");
        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            return Result.Fail("Service connection not found.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail(deviceResult.Reason);
        }

        // Stop any monitoring that is already running for this device.
        await StopExistingMonitoring(deviceId);

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = false,
            NotifyUserOnStart = false,
            IsMonitoring = true,
            MonitoringIntervalSeconds = intervalMinutes * 60,
            ScreenshotPauseWindows = deviceResult.Value.ScreenshotPauseWindows
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        _logger.LogInformation(
            "Monitoring started.  Device ID: {deviceId}.  Interval: {interval} minutes.  Identity: {identity}.",
            deviceId,
            intervalMinutes,
            User.UserName);

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail(orgResult.Reason);
        }

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok();
    }

    /// <summary>
    /// Asks a device to take a screenshot right away.  When the device is not
    /// already connected, a session is started so the device launches and the
    /// screenshot is taken as soon as it connects.
    /// </summary>
    public async Task<Result> CaptureScreenshotNow(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        if (!_agentSessionCache.TryGetConnectionId(deviceId, out var serviceConnectionId))
        {
            return Result.Fail("Device is not online.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail(deviceResult.Reason);
        }

        var pauseWindows = ScreenshotPause.Deserialize(deviceResult.Value.ScreenshotPauseWindows);
        if (ScreenshotPause.IsPaused(pauseWindows, DateTimeOffset.Now))
        {
            return Result.Fail(
                "Screenshots are paused for this device at this time of day.");
        }

        // The device may already be connected for a monitoring session or a
        // remote control session.
        var session = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && !string.IsNullOrWhiteSpace(x.DesktopConnectionId));

        if (session is not null)
        {
            _logger.LogInformation(
                "Requesting a screenshot from device {deviceId}.  Identity: {identity}.",
                deviceId,
                User.UserName);

            await _desktopHubContext.Clients.Client(session.DesktopConnectionId).CaptureScreenshotNow();

            return Result.Ok();
        }

        // There is no session on the device yet, so one is started.  The
        // screenshot is taken as soon as the device connects.
        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var newSession = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = false,
            NotifyUserOnStart = false,
            CaptureRequested = true,
            ScreenshotPauseWindows = deviceResult.Value.ScreenshotPauseWindows
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", newSession);

        _logger.LogInformation(
            "Requesting a screenshot from device {deviceId}.  Identity: {identity}.",
            deviceId,
            User.UserName);

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail(orgResult.Reason);
        }

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok();
    }

    /// <summary>
    /// Stops a monitoring session for a device.
    /// </summary>
    public Task<Result> StopMonitoring(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Task.FromResult(Result.Fail("Unauthorized."));
        }

        StopExistingMonitoring(deviceId);

        _logger.LogInformation(
            "Monitoring stopped.  Device ID: {deviceId}.  Identity: {identity}.",
            deviceId,
            User.UserName);

        return Task.FromResult(Result.Ok());
    }

    /// <summary>
    /// Changes where the screenshots of a device are stored, and how many of
    /// them are kept.
    /// </summary>
    public async Task<Result> UpdateScreenshotSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        return await _dataService.UpdateScreenshotSettings(
            deviceId,
            directory,
            retentionCount,
            retentionHours,
            pauseWindows);
    }

    /// <summary>
    /// Deletes a single screenshot of a device.
    /// </summary>
    public async Task<Result> DeleteScreenshot(string deviceId, string fileName)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _screenshotService.DeleteScreenshot(deviceResult.Value, fileName);

        return Result.Ok();
    }

    /// <summary>
    /// Deletes every screenshot that was captured from a device.
    /// </summary>
    public async Task<Result> ClearScreenshots(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _screenshotService.ClearScreenshots(deviceResult.Value);

        return Result.Ok();
    }

    /// <summary>
    /// Asks a device to record a video clip from one of its cameras or
    /// displays.  The device records the clip and sends it to the server.
    /// </summary>
    public async Task<Result> StartCameraCapture(
        string deviceId,
        CameraCaptureRequest request)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            _logger.LogWarning(
                "A camera recording was attempted by an unauthorized user.  Device ID: {deviceId}.  User Name: {userName}.",
                deviceId,
                User.UserName);

            return Result.Fail("Unauthorized.");
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            return Result.Fail("Device is not online.");
        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            return Result.Fail("Service connection not found.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail(deviceResult.Reason);
        }

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail(orgResult.Reason);
        }

        // A duration of zero means that only the list of sources is asked
        // for, so nothing is recorded.  Every other duration is brought into
        // the range that is allowed.
        var captureRequest = NormalizeCameraRequest(request);

        // A manual request can share the active scheduled session. The desktop
        // serializes captures, so it will never overlap a scheduled clip.
        var monitoringSession = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsCameraMonitoring);

        if (monitoringSession is not null)
        {
            if (string.IsNullOrWhiteSpace(monitoringSession.DesktopConnectionId))
            {
                return Result.Fail("Camera monitoring is waiting for the device to reconnect.");
            }

            await _desktopHubContext.Clients
                .Client(monitoringSession.DesktopConnectionId)
                .StartCameraCapture(deviceId, captureRequest);

            return Result.Ok();
        }

        // Replace any one-shot recording left over for this device.
        await StopExistingCameraCapture(deviceId);

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = false,
            NotifyUserOnStart = false,
            IsCameraCapture = true,
            CameraCapture = captureRequest
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        _logger.LogInformation(
            "Camera capture requested.  Device ID: {deviceId}.  Quality: {quality}.  Duration: {duration} seconds.  Identity: {identity}.",
            deviceId,
            session.CameraCapture.Quality,
            session.CameraCapture.DurationSeconds,
            User.UserName);

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok();
    }

    /// <summary>
    /// Stops a camera recording that is running on a device.
    /// </summary>
    public async Task<Result> StopCameraCapture(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var monitoringSession = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsCameraMonitoring);

        if (monitoringSession is not null)
        {
            if (!string.IsNullOrWhiteSpace(monitoringSession.DesktopConnectionId))
            {
                await _desktopHubContext.Clients
                    .Client(monitoringSession.DesktopConnectionId)
                    .StopCameraCapture();
            }
        }
        else
        {
            await StopExistingCameraCapture(deviceId);
        }

        _logger.LogInformation(
            "Camera capture stopped.  Device ID: {deviceId}.  Identity: {identity}.",
            deviceId,
            User.UserName);

        return Result.Ok();
    }

    /// <summary>
    /// Starts recurring camera recordings at the selected interval.
    /// </summary>
    public async Task<Result> StartCameraMonitoring(
        string deviceId,
        CameraCaptureRequest request,
        int intervalMinutes)
    {
        if (intervalMinutes < 1 || intervalMinutes > 30)
        {
            return Result.Fail("The camera recording interval must be between 1 and 30 minutes.");
        }

        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            return Result.Fail("Device is not online.");
        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            return Result.Fail("Service connection not found.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail(deviceResult.Reason);
        }

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail(orgResult.Reason);
        }

        await StopExistingCameraCapture(deviceId);

        var captureRequest = NormalizeCameraRequest(request);
        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = false,
            NotifyUserOnStart = false,
            IsCameraCapture = true,
            IsCameraMonitoring = true,
            CameraMonitoringIntervalSeconds = intervalMinutes * 60,
            CameraPauseWindows = deviceResult.Value.CameraPauseWindows,
            CameraCapture = captureRequest
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        _logger.LogInformation(
            "Scheduled camera recording started. Device ID: {deviceId}. Interval: {interval} seconds. Identity: {identity}.",
            deviceId,
            session.CameraMonitoringIntervalSeconds,
            User.UserName);

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok();
    }

    /// <summary>
    /// Stops recurring camera recordings for a device.
    /// </summary>
    public async Task<Result> StopCameraMonitoring(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        await StopExistingCameraMonitoring(deviceId);

        _logger.LogInformation(
            "Scheduled camera recording stopped. Device ID: {deviceId}. Identity: {identity}.",
            deviceId,
            User.UserName);

        return Result.Ok();
    }

    public CameraMonitoringStatus? GetCameraMonitoringStatus(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return null;
        }

        var session = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsCameraMonitoring);

        if (session?.CameraCapture is null)
        {
            return null;
        }

        return new CameraMonitoringStatus(
            session.CameraMonitoringIntervalSeconds / 60,
            new CameraCaptureRequest(
                session.CameraCapture.CameraId,
                session.CameraCapture.Quality,
                session.CameraCapture.DurationSeconds));
    }

    public async Task<Result> UpdateCameraSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var result = await _dataService.UpdateCameraSettings(
            deviceId,
            directory,
            retentionCount,
            retentionHours,
            pauseWindows);

        if (!result.IsSuccess)
        {
            return result;
        }

        var monitoringSession = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsCameraMonitoring);

        if (monitoringSession is null)
        {
            return result;
        }

        monitoringSession.CameraPauseWindows = pauseWindows;

        if (string.IsNullOrWhiteSpace(monitoringSession.DesktopConnectionId) ||
            monitoringSession.CameraCapture is null)
        {
            // The updated settings will be applied when the desktop client
            // reconnects and the server re-sends its schedule.
            return result;
        }

        var pauseWindowList = ScreenshotPause.Deserialize(pauseWindows);

        try
        {
            await _desktopHubContext.Clients
                .Client(monitoringSession.DesktopConnectionId)
                .StartCameraMonitoring(
                    deviceId,
                    monitoringSession.CameraCapture,
                    monitoringSession.CameraMonitoringIntervalSeconds,
                    pauseWindowList.Select(x => x.StartHour).ToArray(),
                    pauseWindowList.Select(x => x.EndHour).ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Camera settings were saved, but the active schedule could not be updated for device {deviceId}.",
                deviceId);
        }

        return result;
    }

    /// <summary>
    /// Brings a recording request into the shape that the device expects: the
    /// duration is clamped, the quality and the sources are validated, and a
    /// request that only asks for the list of sources keeps a duration of
    /// zero.
    /// </summary>
    private static CameraCaptureRequest NormalizeCameraRequest(CameraCaptureRequest request)
    {
        var duration = request.DurationSeconds <= 0 ?
            0 :
            CameraQualityPresets.ClampDurationSeconds(request.DurationSeconds);

        var videoSource = Enum.IsDefined(request.VideoSource) ?
            request.VideoSource :
            CameraDeviceKind.Camera;

        var audioSource = Enum.IsDefined(request.AudioSource) ?
            request.AudioSource :
            CameraAudioSource.None;

        return new CameraCaptureRequest(
            request.CameraId ?? string.Empty,
            CameraQualityPresets.ParseQuality(request.Quality.ToString()),
            duration,
            videoSource,
            audioSource,
            request.AudioDeviceId ?? string.Empty);
    }

    /// <summary>
    /// Returns the cameras that the device reported the last time it was
    /// asked to record.
    /// </summary>
    public CameraDeviceInfo[] GetCameraDevices(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Array.Empty<CameraDeviceInfo>();
        }

        var session = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsCameraCapture);

        return session?.CameraDevices.ToArray() ?? Array.Empty<CameraDeviceInfo>();
    }

    /// <summary>
    /// Returns the microphones and the loopback devices that the device
    /// reported the last time it was asked to record.
    /// </summary>
    public CameraAudioDeviceInfo[] GetCameraAudioDevices(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Array.Empty<CameraAudioDeviceInfo>();
        }

        var session = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsCameraCapture);

        return session?.CameraAudioDevices.ToArray() ?? Array.Empty<CameraAudioDeviceInfo>();
    }

    /// <summary>
    /// Deletes a single camera clip of a device.
    /// </summary>
    public async Task<Result> DeleteCameraClip(string deviceId, string fileName)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _cameraService.DeleteClip(deviceResult.Value, fileName);

        return Result.Ok();
    }

    /// <summary>
    /// Deletes every camera clip that was recorded from a device.
    /// </summary>
    public async Task<Result> ClearCameraClips(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _cameraService.ClearClips(deviceResult.Value);

        return Result.Ok();
    }

    /// <summary>
    /// Stops the camera recordings that are running for a device, and removes
    /// their sessions.
    /// </summary>
    private async Task StopExistingCameraCapture(string deviceId)
    {
        foreach (var session in _remoteControlSessionCache.Sessions.Where(x => x.DeviceId == deviceId && x.IsCameraCapture))
        {
            if (!string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                try
                {
                    // Tell the device to stop recording before the session is
                    // removed, so the desktop app doesn't keep recording with
                    // nowhere to send the result.
                    if (session.IsCameraMonitoring)
                    {
                        await _desktopHubContext.Clients
                            .Client(session.DesktopConnectionId)
                            .StopCameraMonitoring();
                    }
                    else
                    {
                        await _desktopHubContext.Clients
                            .Client(session.DesktopConnectionId)
                            .StopCameraCapture();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping a camera recording on device {deviceId}.", deviceId);
                }
            }

            _remoteControlSessionCache.TryRemove($"{session.UnattendedSessionId}", out _);
        }
    }

    private async Task StopExistingCameraMonitoring(string deviceId)
    {
        foreach (var session in _remoteControlSessionCache.Sessions
            .Where(x => x.DeviceId == deviceId && x.IsCameraMonitoring))
        {
            if (!string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                try
                {
                    await _desktopHubContext.Clients
                        .Client(session.DesktopConnectionId)
                        .StopCameraMonitoring();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping scheduled camera recording on device {deviceId}.", deviceId);
                }
            }

            _remoteControlSessionCache.TryRemove($"{session.UnattendedSessionId}", out _);
        }
    }

    /// <summary>
    /// Asks a device to record an audio clip from its system audio or
    /// microphone.  The device records the clip and sends it to the server.
    /// </summary>
    public async Task<Result> StartAudioCapture(
        string deviceId,
        AudioSource source,
        string microphoneId,
        int quality,
        int durationSeconds)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            _logger.LogWarning(
                "An audio recording was attempted by an unauthorized user.  Device ID: {deviceId}.  User Name: {userName}.",
                deviceId,
                User.UserName);

            return Result.Fail("Unauthorized.");
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            return Result.Fail("Device is not online.");
        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            return Result.Fail("Service connection not found.");
        }

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail(orgResult.Reason);
        }

        // A duration of zero means that only the list of microphones is asked
        // for, so nothing is recorded.
        var duration = durationSeconds <= 0 ?
            0 :
            AudioQualityPresets.ClampDurationSeconds(durationSeconds);
        var captureRequest = new AudioCaptureRequest(
            source,
            AudioQualityPresets.ParseQuality(((AudioQuality)quality).ToString()),
            duration,
            microphoneId ?? string.Empty);

        // A manual request can share the active scheduled session.
        var monitoringSession = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsAudioMonitoring);

        if (monitoringSession is not null)
        {
            if (string.IsNullOrWhiteSpace(monitoringSession.DesktopConnectionId))
            {
                return Result.Fail("Audio monitoring is waiting for the device to reconnect.");
            }

            await _desktopHubContext.Clients
                .Client(monitoringSession.DesktopConnectionId)
                .StartAudioCapture(
                    deviceId,
                    captureRequest.Source,
                    captureRequest.MicrophoneId,
                    (int)captureRequest.Quality,
                    duration);

            return Result.Ok();
        }

        // Replace any one-shot recording left over for this device.
        await StopExistingAudioCapture(deviceId);

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = false,
            NotifyUserOnStart = false,
            IsAudioCapture = true,
            AudioCapture = captureRequest
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        _logger.LogInformation(
            "Audio capture requested.  Device ID: {deviceId}.  Source: {source}.  Quality: {quality}.  Duration: {duration} seconds.  Identity: {identity}.",
            deviceId,
            captureRequest.Source,
            captureRequest.Quality,
            captureRequest.DurationSeconds,
            User.UserName);

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok();
    }

    /// <summary>
    /// Stops an audio recording that is running on a device.
    /// </summary>
    public async Task<Result> StopAudioCapture(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var monitoringSession = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsAudioMonitoring);

        if (monitoringSession is not null)
        {
            if (!string.IsNullOrWhiteSpace(monitoringSession.DesktopConnectionId))
            {
                await _desktopHubContext.Clients
                    .Client(monitoringSession.DesktopConnectionId)
                    .StopAudioCapture();
            }
        }
        else
        {
            await StopExistingAudioCapture(deviceId);
        }

        _logger.LogInformation(
            "Audio capture stopped.  Device ID: {deviceId}.  Identity: {identity}.",
            deviceId,
            User.UserName);

        return Result.Ok();
    }

    /// <summary>
    /// Starts recurring audio recordings at the selected interval.
    /// </summary>
    public async Task<Result> StartAudioMonitoring(
        string deviceId,
        AudioSource source,
        string microphoneId,
        int quality,
        int durationSeconds,
        int intervalMinutes)
    {
        if (intervalMinutes < 1 || intervalMinutes > 30)
        {
            return Result.Fail("The audio recording interval must be between 1 and 30 minutes.");
        }

        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            return Result.Fail("Device is not online.");
        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            return Result.Fail("Service connection not found.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail(deviceResult.Reason);
        }

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail(orgResult.Reason);
        }

        await StopExistingAudioMonitoring(deviceId);
        await StopExistingAudioCapture(deviceId);

        var captureRequest = new AudioCaptureRequest(
            source,
            AudioQualityPresets.ParseQuality(((AudioQuality)quality).ToString()),
            AudioQualityPresets.ClampDurationSeconds(durationSeconds),
            microphoneId ?? string.Empty);
        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = false,
            NotifyUserOnStart = false,
            IsAudioCapture = true,
            IsAudioMonitoring = true,
            AudioMonitoringIntervalSeconds = intervalMinutes * 60,
            AudioPauseWindows = deviceResult.Value.AudioPauseWindows,
            AudioCapture = captureRequest
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        _logger.LogInformation(
            "Scheduled audio recording started. Device ID: {deviceId}. Interval: {interval} seconds. Identity: {identity}.",
            deviceId,
            session.AudioMonitoringIntervalSeconds,
            User.UserName);

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok();
    }

    /// <summary>
    /// Stops recurring audio recordings for a device.
    /// </summary>
    public async Task<Result> StopAudioMonitoring(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        await StopExistingAudioMonitoring(deviceId);

        _logger.LogInformation(
            "Scheduled audio recording stopped. Device ID: {deviceId}. Identity: {identity}.",
            deviceId,
            User.UserName);

        return Result.Ok();
    }

    public AudioMonitoringStatus? GetAudioMonitoringStatus(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return null;
        }

        var session = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsAudioMonitoring);

        if (session?.AudioCapture is null)
        {
            return null;
        }

        return new AudioMonitoringStatus(
            session.AudioMonitoringIntervalSeconds / 60,
            new AudioCaptureRequest(
                session.AudioCapture.Source,
                session.AudioCapture.Quality,
                session.AudioCapture.DurationSeconds,
                session.AudioCapture.MicrophoneId));
    }

    public async Task<Result> UpdateAudioSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var result = await _dataService.UpdateAudioSettings(
            deviceId,
            directory,
            retentionCount,
            retentionHours,
            pauseWindows);

        if (!result.IsSuccess)
        {
            return result;
        }

        var monitoringSession = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsAudioMonitoring);

        if (monitoringSession is null)
        {
            return result;
        }

        monitoringSession.AudioPauseWindows = pauseWindows;

        if (string.IsNullOrWhiteSpace(monitoringSession.DesktopConnectionId) ||
            monitoringSession.AudioCapture is null)
        {
            // The updated settings will be applied when the desktop client
            // reconnects and the server re-sends its schedule.
            return result;
        }

        var pauseWindowList = ScreenshotPause.Deserialize(pauseWindows);

        try
        {
            await _desktopHubContext.Clients
                .Client(monitoringSession.DesktopConnectionId)
                .StartAudioMonitoring(
                    deviceId,
                    monitoringSession.AudioCapture,
                    monitoringSession.AudioMonitoringIntervalSeconds,
                    pauseWindowList.Select(x => x.StartHour).ToArray(),
                    pauseWindowList.Select(x => x.EndHour).ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Audio settings were saved, but the active schedule could not be updated for device {deviceId}.",
                deviceId);
        }

        return result;
    }

    /// <summary>
    /// Returns the microphones that the device reported the last time it was
    /// asked to record.
    /// </summary>
    public AudioDeviceInfo[] GetAudioDevices(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Array.Empty<AudioDeviceInfo>();
        }

        var session = _remoteControlSessionCache.Sessions
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsAudioCapture);

        return session?.AudioDevices.ToArray() ?? Array.Empty<AudioDeviceInfo>();
    }

    /// <summary>
    /// Deletes a single audio clip of a device.
    /// </summary>
    public async Task<Result> DeleteAudioClip(string deviceId, string fileName)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _audioService.DeleteClip(deviceResult.Value, fileName);

        return Result.Ok();
    }

    /// <summary>
    /// Deletes every audio clip that was recorded from a device.
    /// </summary>
    public async Task<Result> ClearAudioClips(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _audioService.ClearClips(deviceResult.Value);

        return Result.Ok();
    }

    /// <summary>
    /// Starts a live audio stream from a device to this browser.  The stream
    /// is delivered to the circuit as audio chunk messages.
    /// </summary>
    public async Task<Result> StartAudioLive(
        string deviceId,
        AudioSource source,
        string microphoneId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            _logger.LogWarning(
                "A live audio stream was attempted by an unauthorized user.  Device ID: {deviceId}.  User Name: {userName}.",
                deviceId,
                User.UserName);

            return Result.Fail("Unauthorized.");
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            return Result.Fail("Device is not online.");
        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            return Result.Fail("Service connection not found.");
        }

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail(orgResult.Reason);
        }

        await StopExistingAudioLive(deviceId);

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = false,
            NotifyUserOnStart = false,
            IsAudioCapture = true,
            IsAudioLive = true,
            AudioLiveSource = source
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        _logger.LogInformation(
            "Live audio stream requested.  Device ID: {deviceId}.  Source: {source}.  Identity: {identity}.",
            deviceId,
            source,
            User.UserName);

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok();
    }

    /// <summary>
    /// Stops the live audio stream of a device, and removes its session.
    /// </summary>
    public async Task<Result> StopAudioLive(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        await StopExistingAudioLive(deviceId);

        _logger.LogInformation(
            "Live audio stream stopped.  Device ID: {deviceId}.  Identity: {identity}.",
            deviceId,
            User.UserName);

        return Result.Ok();
    }

    /// <summary>
    /// Turns the keyboard capture of a device on or off.
    /// </summary>
    public async Task<Result> SetKeyboardEnabled(string deviceId, bool enabled)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        var device = deviceResult.Value;

        return await UpdateKeyboardSettings(
            deviceId,
            enabled,
            device.KeyboardRetentionCount,
            device.KeyboardRetentionHours,
            device.KeyboardPauseWindows);
    }

    /// <summary>
    /// Returns the captured text of a device, newest first.
    /// </summary>
    public KeyboardLog[] GetKeyboardLogs(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Array.Empty<KeyboardLog>();
        }

        return _keyboardLogService.GetLogs(deviceId, 200);
    }

    public async Task<Result> DeleteKeyboardLog(string deviceId, string logId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _keyboardLogService.DeleteLog(deviceResult.Value, logId);

        return Result.Ok();
    }

    public async Task<Result> ClearKeyboardLogs(string deviceId)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return Result.Fail("Device not found.");
        }

        _keyboardLogService.ClearLogs(deviceResult.Value);

        return Result.Ok();
    }

    public async Task<Result> UpdateKeyboardSettings(
        string deviceId,
        bool enabled,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return Result.Fail("Unauthorized.");
        }

        var result = await _dataService.UpdateKeyboardSettings(
            deviceId,
            enabled,
            retentionCount,
            retentionHours,
            pauseWindows);

        if (!result.IsSuccess)
        {
            return result;
        }

        if (enabled)
        {
            var started = await _keyboardCaptureManager.StartAsync(
                deviceId,
                ConnectionId,
                User.OrganizationID,
                $"{User.UserOptions?.DisplayName}");

            if (!started)
            {
                return Result.Fail(
                    "The settings were saved, but the device is not online, so the capture was not started. " +
                    "It starts as soon as the device comes online.");
            }
        }
        else
        {
            await _keyboardCaptureManager.StopAsync(deviceId);
        }

        _logger.LogInformation(
            "The keyboard capture of device {deviceId} was turned {state}.  Identity: {identity}.",
            deviceId,
            enabled ? "on" : "off",
            User.UserName);

        return result;
    }

    /// <summary>
    /// Stops the one-shot audio recordings that are running for a device, and
    /// removes their sessions.  A live stream and an active schedule are left
    /// alone, because they have their own sessions and their own stop
    /// actions.
    /// </summary>
    private async Task StopExistingAudioCapture(string deviceId)
    {
        foreach (var session in _remoteControlSessionCache.Sessions
            .Where(x => x.DeviceId == deviceId && x.IsAudioCapture && !x.IsAudioLive && !x.IsAudioMonitoring))
        {
            if (!string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                try
                {
                    // Tell the device to stop recording before the session is
                    // removed, so the desktop app doesn't keep recording with
                    // nowhere to send the result.
                    await _desktopHubContext.Clients
                        .Client(session.DesktopConnectionId)
                        .StopAudioCapture();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping an audio recording on device {deviceId}.", deviceId);
                }
            }

            _remoteControlSessionCache.TryRemove($"{session.UnattendedSessionId}", out _);
        }
    }

    private async Task StopExistingAudioMonitoring(string deviceId)
    {
        foreach (var session in _remoteControlSessionCache.Sessions
            .Where(x => x.DeviceId == deviceId && x.IsAudioMonitoring))
        {
            if (!string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                try
                {
                    await _desktopHubContext.Clients
                        .Client(session.DesktopConnectionId)
                        .StopAudioMonitoring();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping scheduled audio recording on device {deviceId}.", deviceId);
                }
            }

            _remoteControlSessionCache.TryRemove($"{session.UnattendedSessionId}", out _);
        }
    }

    private async Task StopExistingAudioLive(string deviceId)
    {
        foreach (var session in _remoteControlSessionCache.Sessions
            .Where(x => x.DeviceId == deviceId && x.IsAudioLive))
        {
            if (!string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                try
                {
                    await _desktopHubContext.Clients
                        .Client(session.DesktopConnectionId)
                        .StopAudioLive();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping a live audio stream on device {deviceId}.", deviceId);
                }
            }

            _remoteControlSessionCache.TryRemove($"{session.UnattendedSessionId}", out _);
        }
    }

    private async Task StopExistingMonitoring(string deviceId)    {        foreach (var session in _remoteControlSessionCache.Sessions.Where(x => x.DeviceId == deviceId && x.IsMonitoring))
        {
            if (!string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                try
                {
                    // Tell the device to stop capturing before the session is
                    // removed, so the desktop app doesn't keep taking
                    // screenshots with nowhere to send them.
                    await _desktopHubContext.Clients
                        .Client(session.DesktopConnectionId)
                        .StopMonitoring();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping monitoring on device {deviceId}.", deviceId);
                }
            }

            _remoteControlSessionCache.TryRemove($"{session.UnattendedSessionId}", out _);
        }
    }

    public Task RemoveDevices(string[] deviceIDs)
    {
        if (User is not null)
        {
            var filterDevices = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
            _dataService.RemoveDevices(filterDevices);
        }

        return Task.CompletedTask;
    }

    public async Task RunScript(
        IEnumerable<string> deviceIds,
        Guid savedScriptId,
        int scriptRunId,
        ScriptInputType scriptInputType,
        bool runAsHostedService)
    {
        var username = string.Empty;
        if (runAsHostedService)
        {
            username = "Remotely Server";
        }
        else if (User is not null)
        {
            username = User.UserName;
            deviceIds = _dataService.FilterDeviceIdsByUserPermission(deviceIds.ToArray(), User);
        }

        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(AppConstants.ScriptRunExpirationMinutes));

        var connectionIds = _agentSessionCache.GetConnectionIdsByDeviceIds(deviceIds).ToArray();

        if (connectionIds.Any())
        {
            await _agentHubContext.Clients.Clients(connectionIds).RunScript(
                savedScriptId,
                scriptRunId,
                $"{username}",
                scriptInputType,
                authToken);
        }

    }

    public async Task SendChat(string message, string deviceId, bool isDisconnecting = false)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return;
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var device) ||
            !_agentSessionCache.TryGetConnectionId(deviceId, out var connectionId))
        {
            _toastService.ShowToast("Device not found.");
            return;
        }

        if (device.OrganizationID != User.OrganizationID)
        {
            _toastService.ShowToast("Unauthorized.");
            return;
        }

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2("Organization not found.", Enums.ToastType.Warning);
            return;
        }

        await _agentHubContext.Clients.Client(connectionId).SendChatMessage(
            User.UserOptions?.DisplayName ?? $"{User.UserName}",
            message,
            orgResult.Value,
            User.OrganizationID,
            isDisconnecting,
            ConnectionId);
    }

    public async Task<bool> TransferFileFromBrowserToAgent(string deviceId, string transferId, string[] fileIds)
    {
        if (!_agentSessionCache.TryGetConnectionId(deviceId, out var connectionId))
        {
            return false;
        }

        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            _logger.LogWarning("User {username} does not have access to device ID {deviceId} and attempted file upload.",
                User.UserName,
                deviceId);

            return false;
        }

        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(5));

        await _agentHubContext.Clients
            .Client(connectionId)
            .TransferFileFromBrowserToAgent(
                transferId,
                fileIds,
                ConnectionId,
                authToken);

        return true;
    }

    public async Task TriggerHeartbeat(string deviceId)
    {
        var (canAccess, connectionId) = CanAccessDevice(deviceId);

        if (!canAccess)
        {
            return;
        }

        await _agentHubContext.Clients.Client(connectionId).TriggerHeartbeat();
    }

    public async Task UninstallAgents(string[] deviceIDs)
    {
        deviceIDs = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
        var connections = GetActiveConnectionsForUserOrg(deviceIDs);
        await _agentHubContext.Clients.Clients(connections).UninstallAgent();
        _dataService.RemoveDevices(deviceIDs);
    }

    public async Task UpdateTags(string deviceID, string tags)
    {
        if (_dataService.DoesUserHaveAccessToDevice(deviceID, User))
        {
            if (tags.Length > 200)
            {
                var message = new DisplayNotificationMessage(
                     $"Tag must be 200 characters or less. Supplied length is {tags.Length}.",
                    "Tag must be under 200 characters.",
                    "bg-warning");

                await _messenger.Send(message, ConnectionId);
                return;
            }

            await _dataService.UpdateTags(deviceID, tags);

            var successMessage = new DisplayNotificationMessage(
                "Device updated successfully.",
                "Device updated.",
                "bg-success");

            await _messenger.Send(successMessage, ConnectionId);
        }
    }

    public async Task<Result> WakeDevice(Device device)
    {
        try
        {
            if (!_dataService.DoesUserHaveAccessToDevice(device.ID, User.Id))
            {
                return Result.Fail("Unauthorized.");
            }

            var availableDevices = _agentSessionCache
                .GetAllDevices()
                .Where(x =>
                     x.OrganizationID == User.OrganizationID &&
                    (x.DeviceGroupID == device.DeviceGroupID || x.PublicIP == device.PublicIP))
                .ToArray();

            await SendWakeCommand(device, availableDevices);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waking device {deviceId}.", device.ID);
            return Result.Fail(ex);
        }
    }

    public async Task<Result> WakeDevices(Device[] devices)
    {
        try
        {
            var deviceIds = devices.Select(x => x.ID).ToArray();
            var filteredIds = _dataService.FilterDeviceIdsByUserPermission(deviceIds, User);
            var filteredDevices = devices.Where(x => filteredIds.Contains(x.ID)).ToArray();

            var availableDevices = _agentSessionCache
                .GetAllDevices()
                .Where(x => x.OrganizationID == User.OrganizationID);

            var devicesByGroupId = new ConcurrentDictionary<string, List<Device>>();
            var devicesByPublicIp = new ConcurrentDictionary<string, List<Device>>();

            foreach (var device in availableDevices)
            {
                if (!string.IsNullOrWhiteSpace(device.DeviceGroupID))
                {
                    var group = devicesByGroupId.GetOrAdd(device.DeviceGroupID, key => new());
                    group.Add(device);
                    // We only need the device in one group.
                    break;
                }

                if (!string.IsNullOrWhiteSpace(device.PublicIP))
                {
                    var group = devicesByPublicIp.GetOrAdd(device.PublicIP, key => new());
                    group.Add(device);
                }
            }

            foreach (var deviceToWake in filteredDevices)
            {
                if (!string.IsNullOrWhiteSpace(deviceToWake.DeviceGroupID) &&
                    devicesByGroupId.TryGetValue(deviceToWake.DeviceGroupID, out var groupList))
                {
                    await SendWakeCommand(deviceToWake, groupList);
                }

                if (!string.IsNullOrWhiteSpace(deviceToWake.PublicIP) &&
                    devicesByPublicIp.TryGetValue(deviceToWake.PublicIP, out var ipList))
                {
                    await SendWakeCommand(deviceToWake, ipList);
                }

            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while waking devices.");
            return Result.Fail(ex);
        }
    }

    private (bool canAccess, string connectionId) CanAccessDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return (false, string.Empty);
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var device) ||
            !_dataService.DoesUserHaveAccessToDevice(device.ID, User) ||
            !_agentSessionCache.TryGetConnectionId(device.ID, out var connectionId))
        {
            return (false, string.Empty);
        }


        return (true, connectionId);
    }

    private IEnumerable<string> GetActiveConnectionsForUserOrg(IEnumerable<string> deviceIds)
    {

        foreach (var deviceId in deviceIds)
        {
            if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var device))
            {
                continue;
            }

            if (device.OrganizationID != User.OrganizationID)
            {
                continue;
            }

            if (_agentSessionCache.TryGetConnectionId(device.ID, out var connectionId))
            {
                yield return connectionId;
            }
        }
    }


    private async Task SendWakeCommand(Device deviceToWake, IEnumerable<Device> peerDevices)
    {
        foreach (var peerDevice in peerDevices)
        {
            foreach (var mac in deviceToWake.MacAddresses ?? Array.Empty<string>())
            {
                if (_agentSessionCache.TryGetConnectionId(peerDevice.ID, out var connectionId))
                {
                    _logger.LogInformation(
                        "Sending wake command for device {deviceName} ({deviceId}) to " +
                        "peer device {peerDeviceName} ({peerDeviceId}).  " +
                        "Sender: {username}.",
                        deviceToWake.DeviceName,
                        deviceToWake.ID,
                        peerDevice.DeviceName,
                        peerDevice.ID,
                        User.UserName);
                    await _agentHubContext.Clients.Client(connectionId).WakeDevice(mac);
                }
            }
        }
    }
}
