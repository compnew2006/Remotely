using Remotely.Server.Services;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Remotely.Server.Models.Messages;
using Remotely.Shared;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Helpers;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Hubs;

public class AgentHub : Hub<IAgentHubClient>
{
    private readonly IDeviceService _deviceService;
    private readonly IScriptResultService _scriptResultService;
    private readonly ISettingsService _settingsService;
    private readonly IUserService _userService;
    private readonly ICircuitManager _circuitManager;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<AgentHub> _logger;
    private readonly IMessenger _messenger;
    private readonly IRemoteControlSessionCache _remoteControlSessions;
    private readonly IAgentHubSessionCache _serviceSessionCache;
    private readonly IHubContext<ViewerHub> _viewerHubContext;
    private readonly IKeyboardCaptureManager _keyboardCaptureManager;
    private readonly IUpdateSigningService _updateSigningService;
    private readonly IApiScriptResultCache _apiScriptResultCache;

    public AgentHub(
        IDeviceService deviceService,
        IScriptResultService scriptResultService,
        ISettingsService settingsService,
        IUserService userService,
        IAgentHubSessionCache serviceSessionCache,
        IHubContext<ViewerHub> viewerHubContext,
        ICircuitManager circuitManager,
        IExpiringTokenService expiringTokenService,
        IRemoteControlSessionCache remoteControlSessionCache,
        IKeyboardCaptureManager keyboardCaptureManager,
        IUpdateSigningService updateSigningService,
        IApiScriptResultCache apiScriptResultCache,
        IMessenger messenger,
        ILogger<AgentHub> logger)
    {
        _deviceService = deviceService;
        _scriptResultService = scriptResultService;
        _settingsService = settingsService;
        _userService = userService;
        _serviceSessionCache = serviceSessionCache;
        _viewerHubContext = viewerHubContext;
        _circuitManager = circuitManager;
        _expiringTokenService = expiringTokenService;
        _remoteControlSessions = remoteControlSessionCache;
        _keyboardCaptureManager = keyboardCaptureManager;
        _updateSigningService = updateSigningService;
        _apiScriptResultCache = apiScriptResultCache;
        _messenger = messenger;
        _logger = logger;
    }

    private Device? Device
    {
        get
        {
            // TryGetValue, not the indexer: an unregistered connection has no
            // "Device" entry, and the indexer throws KeyNotFoundException.
            // The hub's own guard (and SetServerVerificationToken, which any
            // connection can call) expects a null here.
            if (Context.Items.TryGetValue("Device", out var stored) && stored is Device device)
            {
                return device;
            }
            _logger.LogWarning("Device has not been set in the context items.");
            return null;
        }
        set
        {
            Context.Items["Device"] = value;
        }
    }

    public async Task Chat(string messageText, bool disconnected, string browserConnectionId)
    {
        if (Device is null)
        {
            return;
        }

        if (_circuitManager.TryGetConnection(browserConnectionId, out _))
        {
            var message = new ChatReceivedMessage(Device.ID, $"{Device.DeviceName}", messageText, disconnected);
            await _messenger.Send(message, browserConnectionId);
        }
        else
        {
            await Clients.Caller.SendChatMessage(
                senderName: string.Empty,
                message: string.Empty,
                orgName: string.Empty,
                orgId: string.Empty,
                disconnected: true,
                senderConnectionId: browserConnectionId);
        }
    }

    public async Task CheckForPendingRemoteControlSessions()
    {
        try
        {
            if (Device is null)
            {
                return;
            }

            using var deviceLock = await _serviceSessionCache.AcquireDeviceLockAsync(Device.ID);
            if (!_serviceSessionCache.TryGetConnectionId(Device.ID, out var currentConnectionId) ||
                currentConnectionId != Context.ConnectionId)
            {
                return;
            }

            _logger.LogDebug(
                "Checking for pending remote control sessions for device {deviceId}.",
                Device.ID);

            var waitingSessions = _remoteControlSessions
                .Sessions
                .Where(x => x.DeviceId == Device.ID);

            foreach (var session in waitingSessions)
            {
                _logger.LogDebug(
                    "Restarting remote control session {sessionId}.",
                    session.UnattendedSessionId);

                session.AgentConnectionId = Context.ConnectionId;
                await Clients.Caller.RestartScreenCaster(
                    session.ViewerList.ToArray(),
                    $"{session.UnattendedSessionId}",
                    session.AccessKey,
                    session.UserConnectionId,
                    session.RequesterName,
                    session.OrganizationName,
                    session.OrganizationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for pending remote control sessions.");
        }
    }

    public async Task CheckForPendingScriptRuns()
    {
        if (Device is null)
        {
            return;
        }

        using var deviceLock = await _serviceSessionCache.AcquireDeviceLockAsync(Device.ID);
        if (!_serviceSessionCache.TryGetConnectionId(Device.ID, out var currentConnectionId) ||
            currentConnectionId != Context.ConnectionId)
        {
            return;
        }

        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(AppConstants.ScriptRunExpirationMinutes));
        var scriptRuns = await _scriptResultService.GetPendingScriptRuns(Device.ID);

        foreach (var run in scriptRuns)
        {
            if (run.SavedScriptId is null)
            {
                continue;
            }
            await Clients.Caller.RunScript(
                run.SavedScriptId.Value,
                run.Id,
                run.Initiator ?? "Unknown Initiator",
                run.InputType,
                authToken);
        }
    }

    public async Task<bool> DeviceCameOnline(DeviceClientDto device)
    {
        try
        {
            if (await CheckForDeviceBan(device.ID, device.DeviceName))
            {
                return false;
            }

            var ip = Context.GetHttpContext()?.Connection?.RemoteIpAddress;
            if (ip != null && ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }
            device.PublicIP = $"{ip}";

            if (await CheckForDeviceBan(device.PublicIP))
            {
                return false;
            }

            using var deviceLock = await _serviceSessionCache.AcquireDeviceLockAsync(device.ID);

            var presentedToken = GetPresentedDeviceToken();

            // G4 / device identity: a registration proves that it holds the
            // token bound to the device record.  A device ID and an
            // organization ID alone are not an identity; without this check
            // anyone who knows them can claim the device and receive its
            // session traffic.
            if (!await VerifyDeviceIdentity(device.ID, presentedToken))
            {
                return false;
            }

            var result = await _deviceService.AddOrUpdateDevice(device);
            if (!result.IsSuccess)
            {
                // Organization wasn't found.
                return false;
            }

            // A record that was just created has no token yet.  Bind the
            // token this registration presented right away, so the identity
            // is established on the first registration instead of staying
            // unbound (and claimable) until the device reconnects.
            if (!string.IsNullOrWhiteSpace(presentedToken) &&
                string.IsNullOrWhiteSpace(result.Value.ServerVerificationToken))
            {
                _deviceService.SetServerVerificationToken(result.Value.ID, presentedToken);
                result.Value.ServerVerificationToken = DeviceCredential.Fingerprint(presentedToken);
            }

            Device = result.Value;

            _serviceSessionCache.AddOrUpdateByConnectionId(Context.ConnectionId, Device);

            var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

            var filteredUserIDs = _userService.FilterUsersByDevicePermission(userIDs, Device.ID);

            var connections = _circuitManager.Connections
                .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                    filteredUserIDs.Contains(x.User.Id));

            foreach (var connection in connections)
            {
                var message = new DeviceStateChangedMessage(Device);
                await _messenger.Send(message, connection.ConnectionId);
            }

            if (Device.KeyboardEnabled)
            {
                // The text that is typed on this device is captured, so the
                // capture starts as soon as the device comes online.
                await _keyboardCaptureManager.StartAsync(
                    Device.ID,
                    string.Empty,
                    Device.OrganizationID,
                    "Remotely");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting device to online status.");
        }

        Context.Abort();
        return false;
    }

    public async Task DeviceHeartbeat(DeviceClientDto device)
    {
        if (await CheckForDeviceBan(device.ID, device.DeviceName))
        {
            return;
        }

        var ip = Context.GetHttpContext()?.Connection?.RemoteIpAddress;
        if (ip != null && ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }
        device.PublicIP = $"{ip}";

        if (await CheckForDeviceBan(device.PublicIP))
        {
            return;
        }

        using (await _serviceSessionCache.AcquireDeviceLockAsync(device.ID))
        {
            if (!_serviceSessionCache.TryGetConnectionId(device.ID, out var currentConnectionId) ||
                currentConnectionId != Context.ConnectionId)
            {
                return;
            }

            var result = await _deviceService.AddOrUpdateDevice(device);

            if (!result.IsSuccess)
            {
                return;
            }

            Device = result.Value;

            _serviceSessionCache.AddOrUpdateByConnectionId(Context.ConnectionId, Device);

            var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

            var filteredUserIDs = _userService.FilterUsersByDevicePermission(userIDs, Device.ID);

            var connections = _circuitManager.Connections
                .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                    filteredUserIDs.Contains(x.User.Id));

            foreach (var connection in connections)
            {
                var message = new DeviceStateChangedMessage(Device);
                await _messenger.Send(message, connection.ConnectionId);
            }
        }

        await CheckForPendingScriptRuns();
    }

    public Task DisplayMessage(string consoleMessage, string popupMessage, string className, string requesterId)
    {
        var message = new DisplayNotificationMessage(consoleMessage, popupMessage, className);
        return _messenger.Send(message, requesterId);
    }

    public Task DownloadFile(string fileID, string requesterId)
    {
        var message = new DownloadFileMessage(fileID);
        return _messenger.Send(message, requesterId);
    }

    public Task DownloadFileProgress(int progressPercent, string requesterId)
    {
        var message = new DownloadFileProgressMessage(progressPercent);
        return _messenger.Send(message, requesterId);
    }

    public async Task<string> GetServerUrl()
    {
        var settings = await _settingsService.GetSettings();
        return settings.ServerUrl;
    }

    public string GetServerVerificationToken()
    {
        // The fingerprint of the credential, never the credential itself.
        // The agent compares it with the fingerprint of the credential it
        // holds, which proves that both sides talk to the same device
        // without the server ever knowing the credential.
        return $"{Device?.ServerVerificationToken}";
    }

    /// <summary>
    /// The trusted keys of the client packages of this server.  The agent
    /// writes them next to its executable, so it can verify a package before
    /// it installs it (G2 / AD-13).
    /// </summary>
    public string GetUpdatePublicKey()
    {
        return _updateSigningService.GetTrustedKeysJson();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (Device != null)
            {
                using var deviceLock = await _serviceSessionCache.AcquireDeviceLockAsync(Device.ID);

                if (_serviceSessionCache.TryRemoveByConnectionId(Context.ConnectionId, out var disconnectedDevice))
                {
                    _deviceService.DeviceDisconnected(disconnectedDevice.ID);

                    disconnectedDevice.IsOnline = false;

                    var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

                    var filteredUserIDs = _userService.FilterUsersByDevicePermission(userIDs, disconnectedDevice.ID);

                    var connections = _circuitManager.Connections
                        .Where(x => x.User.OrganizationID == disconnectedDevice.OrganizationID &&
                            filteredUserIDs.Contains(x.User.Id));

                    foreach (var connection in connections)
                    {
                        var message = new DeviceStateChangedMessage(disconnectedDevice);
                        await _messenger.Send(message, connection.ConnectionId);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        finally
        {
            if (Device != null)
            {
                using var deviceLock = await _serviceSessionCache.AcquireDeviceLockAsync(Device.ID);
                _serviceSessionCache.TryRemoveByConnectionId(Context.ConnectionId, out _);
            }
        }
    }

    public Task ReturnPowerShellCompletions(PwshCommandCompletion completion, CompletionIntent intent, string senderConnectionId)
    {
        var message = new PowerShellCompletionsMessage(completion, intent);
        return _messenger.Send(message, senderConnectionId);
    }

    public async Task ScriptResult(string scriptResultId)
    {
        var result = await _scriptResultService.GetScriptResult(scriptResultId);
        if (!result.IsSuccess)
        {
            return;
        }

        var message = new ScriptResultMessage(result.Value);
        await _messenger.Send(message, $"{result.Value.SenderConnectionID}");
    }

    public void ScriptResultViaApi(string commandID, string requestID)
    {
        _apiScriptResultCache.Set(requestID, commandID);
    }

    public Task SendConnectionFailedToViewers(List<string> viewerIDs)
    {
        return _viewerHubContext.Clients.Clients(viewerIDs).SendAsync("ConnectionFailed");
    }

    public Task SendLogs(string logChunk, string requesterConnectionId)
    {
        var message = new ReceiveLogsMessage(logChunk);
        return _messenger.Send(message, requesterConnectionId);
    }

    public void SetServerVerificationToken(string verificationToken)
    {
        if (Device is null)
        {
            return;
        }

        // Only a connection that registered successfully reaches this method,
        // so the caller has already proved that it holds the credential.  It
        // may therefore rotate it.
        if (string.IsNullOrWhiteSpace(GetPresentedDeviceToken()))
        {
            // This agent predates the credential header, so it just generated
            // the GUID it will present on every later connection, and it
            // compares that GUID with whatever the server returns.  Keeping
            // the fingerprint would lock it out of this server.
            Device.ServerVerificationToken = verificationToken;
            _deviceService.SetLegacyServerVerificationToken(Device.ID, verificationToken);
            return;
        }

        var fingerprint = DeviceCredential.Fingerprint(verificationToken);
        Device.ServerVerificationToken = fingerprint;
        _deviceService.SetServerVerificationToken(Device.ID, verificationToken);
    }

    public Task TransferCompleted(string transferId, string requesterId)
    {
        var message = new TransferCompleteMessage(transferId);
        return _messenger.Send(message, requesterId);
    }

    /// <summary>
    /// The device verification token presented on this connection's HTTP
    /// request, or an empty string when the agent did not send one.
    /// </summary>
    private string GetPresentedDeviceToken() =>
        Context.GetHttpContext()?
            .Request
            .Headers[AppConstants.DeviceVerificationTokenHeaderName]
            .ToString() ?? string.Empty;

    /// <summary>
    /// Proves that this connection holds the verification token bound to the
    /// device record it is registering.  The token is sent as a connection
    /// header, so it is available before the first hub invocation.
    /// </summary>
    private async Task<bool> VerifyDeviceIdentity(string deviceId, string presentedToken)
    {
        if (await _deviceService.VerifyDeviceVerificationToken(deviceId, presentedToken))
        {
            return true;
        }

        _logger.LogWarning(
            "Device registration refused: the presented verification token does not match the token bound to the device.  " +
            "Device ID: {deviceId}.  Remote IP: {remoteIp}.",
            deviceId,
            Context.GetHttpContext()?.Connection.RemoteIpAddress);

        return false;
    }

    private async Task<bool> CheckForDeviceBan(params string[] deviceIdNameOrIPs)
    {
        var settings = await _settingsService.GetSettings();
        foreach (var device in deviceIdNameOrIPs)
        {
            if (string.IsNullOrWhiteSpace(device))
            {
                continue;
            }

            if (settings.BannedDevices.Any(x => !string.IsNullOrWhiteSpace(x) &&
                x.Equals(device, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Device ID/name/IP ({device}) is banned.  Sending uninstall command.", device);

                await Clients.Caller.UninstallAgent();
                return true;
            }
        }

        return false;
    }
}
