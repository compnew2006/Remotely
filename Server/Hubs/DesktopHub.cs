using Remotely.Server.Enums;
using Remotely.Server.Models;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services;
using Remotely.Shared.Enums;
using Remotely.Shared.Extensions;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR;

namespace Remotely.Server.Hubs;

public class DesktopHub : Hub<IDesktopHubClient>
{
    private readonly IAgentHubSessionCache _agentCache;
    private readonly ICameraService _cameraService;
    private readonly IAudioService _audioService;
    private readonly IDataService _dataService;
    private readonly ILogger<DesktopHub> _logger;
    private readonly IMessenger _messenger;
    private readonly IRemoteControlSessionCache _sessionCache;
    private readonly IDesktopStreamCache _streamCache;
    private readonly IScreenshotService _screenshotService;
    private readonly IHubContext<ViewerHub, IViewerHubClient> _viewerHub;
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub;

    public DesktopHub(
        IRemoteControlSessionCache sessionCache,
        IDesktopStreamCache streamCache,
        IAgentHubSessionCache agentCache,
        IScreenshotService screenshotService,
        ICameraService cameraService,
        IAudioService audioService,
        IDataService dataService,
        IHubContext<AgentHub, IAgentHubClient> agentHub,
        IHubContext<ViewerHub, IViewerHubClient> viewerHub,
        IMessenger messenger,
        ILogger<DesktopHub> logger)
    {
        _sessionCache = sessionCache;
        _agentCache = agentCache;
        _streamCache = streamCache;
        _screenshotService = screenshotService;
        _cameraService = cameraService;
        _audioService = audioService;
        _dataService = dataService;
        _viewerHub = viewerHub;
        _agentHub = agentHub;
        _messenger = messenger;
        _logger = logger;
    }

    private RemoteControlSession SessionInfo
    {
        get
        {
            if (Context.Items.TryGetValue(nameof(SessionInfo), out var result) &&
                result is RemoteControlSession session)
            {
                return session;
            }

            var newSession = new RemoteControlSession();
            Context.Items[nameof(SessionInfo)] = newSession;
            return newSession;
        }
        set
        {
            Context.Items[nameof(SessionInfo)] = value;
        }
    }

    private HashSet<string> ViewerList => SessionInfo.ViewerList;

    public async Task DisconnectViewer(string viewerID, bool notifyViewer)
    {
        ViewerList.Remove(viewerID);

        if (notifyViewer)
        {
            await _viewerHub.Clients.Client(viewerID).ViewerRemoved();
        }
    }

    public async Task<string> GetSessionID()
    {
        using var scope = _logger.BeginScope(nameof(GetSessionID));

        SessionInfo.Mode = RemoteControlMode.Attended;

        var random = new Random();
        var sessionId = string.Empty;

        while (true)
        {
            sessionId = "";
            for (var i = 0; i < 3; i++)
            {
                sessionId += random.Next(0, 999).ToString().PadLeft(3, '0');
            }

            SessionInfo.AttendedSessionId = sessionId;
            if (_sessionCache.TryAdd(sessionId, SessionInfo))
            {
                break;
            }
            await Task.Yield();
        }

        SessionInfo.SetSessionReadyState(true);
        return sessionId;
    }

    public Task NotifyRequesterUnattendedReady()
    {
        using var scope = _logger.BeginScope(nameof(NotifyRequesterUnattendedReady));

        if (!_sessionCache.TryGetValue($"{SessionInfo.UnattendedSessionId}", out var session))
        {
            _logger.LogError("Connection not found in cache.");
            return Task.CompletedTask;
        }

        session.SetSessionReadyState(true);
        return Task.CompletedTask;
    }

    public async Task NotifySessionChanged(SessionSwitchReasonEx reason, int currentSessionId)
    {
        if (!reason.IsTerminalSessionSwitch())
        {
            // A lock/unlock, an RDP connect/disconnect, or a console
            // connect/disconnect does not end the Windows session.  The device
            // keeps capturing the same desktop, so the viewer must not be
            // interrupted and the screen caster must not be restarted -
            // restarting it kills the running capture and drops the stream.
            _logger.LogInformation(
                "Windows session switch detected on the device.  Reason: {reason}.  " +
                "The screen cast is unaffected.",
                reason);
            return;
        }

        SessionInfo.StreamerState = StreamerState.ChangingSessions | StreamerState.DisconnectExpected;
        await _viewerHub.Clients.Clients(ViewerList).ShowMessage("Changing sessions");
        await NotifySessionChangedImpl(reason, currentSessionId);
    }

    public async Task NotifySessionEnding(SessionEndReasonsEx reason)
    {
        switch (reason)
        {
            case SessionEndReasonsEx.Logoff:
                SessionInfo.StreamerState = StreamerState.WindowsLoggingOff | StreamerState.DisconnectExpected;
                await _viewerHub.Clients.Clients(ViewerList).ShowMessage("Windows session ending");
                await NotifySessionChangedImpl(SessionSwitchReasonEx.SessionLogoff, -1);
                break;
            case SessionEndReasonsEx.SystemShutdown:
                SessionInfo.StreamerState = StreamerState.WindowsShuttingDown | StreamerState.DisconnectExpected;
                await _viewerHub.Clients.Clients(ViewerList).ShowMessage("Waiting for device to restart");
                break;
            default:
                break;
        }
    }

    public Task NotifyViewersRelaunchedScreenCasterReady(string[] viewerIDs)
    {
        SessionInfo.DesktopConnectionId = Context.ConnectionId;
        return _viewerHub.Clients
            .Clients(viewerIDs)
            .RelaunchedScreenCasterReady(
                SessionInfo.UnattendedSessionId,
                SessionInfo.AccessKey);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Desktop app disconnected. Streamer State: {state}.  Viewer Count: {count}",
            SessionInfo.StreamerState,
            ViewerList.Count);

        SessionInfo.SetSessionReadyState(false);

        if (SessionInfo.Mode == RemoteControlMode.Attended)
        {
            SessionInfo.StreamerState = StreamerState.Disconnected;
            _ = _sessionCache.TryRemove(SessionInfo.AttendedSessionId, out _);
            await _viewerHub.Clients.Clients(ViewerList).ScreenCasterDisconnected();
        }
        else if (
            SessionInfo.Mode == RemoteControlMode.Unattended &&
            SessionInfo.IsCameraMonitoring)
        {
            // Keep the schedule in the server cache while the desktop hub
            // reconnects. The client will re-register the same session and
            // receive the schedule again when it is back online.
            if (SessionInfo.DesktopConnectionId == Context.ConnectionId)
            {
                SessionInfo.DesktopConnectionId = string.Empty;
                SessionInfo.StreamerState = StreamerState.Disconnected;
            }
        }
        else if (
            SessionInfo.Mode == RemoteControlMode.Unattended &&
            !SessionInfo.StreamerState.HasFlag(StreamerState.DisconnectExpected))
        {
            // Don't restart if consent wasn't granted on the first request.
            if (ViewerList.Count > 0 && SessionInfo.ConsentGranted)
            {
                await RestartScreenCaster();
            }
            else
            {
                _ = _sessionCache.TryRemove($"{SessionInfo.UnattendedSessionId}", out _);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<Result<string>> PingViewer(string viewerConnectionId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _viewerHub.Clients.Client(viewerConnectionId).PingViewer(cts.Token);
            return Result.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ping viewer with connection ID {connectionId}.", viewerConnectionId);
            return Result.Fail<string>("Failed to ping viewer.");
        }
    }

    public Task ReceiveAttendedSessionInfo(string machineName)
    {
        SessionInfo.DesktopConnectionId = Context.ConnectionId;
        SessionInfo.StartTime = DateTimeOffset.Now;
        SessionInfo.MachineName = machineName;

        return Task.CompletedTask;
    }

    public async Task<Result> ReceiveUnattendedSessionInfo(Guid unattendedSessionId, string accessKey, string machineName, string requesterName, string organizationName)
    {
        if (_sessionCache.TryGetValue($"{unattendedSessionId}", out var existingSession) &&
            !string.IsNullOrWhiteSpace(existingSession.AccessKey) &&
            accessKey != existingSession.AccessKey)
        {
            _logger.LogWarning(
                "A desktop session tried to take over an existing session, " +
                "but the access key didn't match.");
            var result = Result.Fail("SessionId already exists on the server.");
            return result;
        }

        SessionInfo = _sessionCache.GetOrAdd($"{unattendedSessionId}", (key) => SessionInfo);

        SessionInfo.Mode = RemoteControlMode.Unattended;
        SessionInfo.DesktopConnectionId = Context.ConnectionId;
        SessionInfo.StartTime = DateTimeOffset.Now;
        SessionInfo.UnattendedSessionId = unattendedSessionId;
        SessionInfo.AccessKey = accessKey;
        SessionInfo.MachineName = machineName;
        SessionInfo.RequesterName = requesterName;
        SessionInfo.OrganizationName = organizationName;

        if (SessionInfo.IsMonitoring)
        {
            // This session was created for monitoring, so the device starts
            // capturing periodic screenshots with no viewer attached.
            _logger.LogInformation(
                "Starting monitoring on device {deviceId}.  Interval: {interval} seconds.",
                SessionInfo.DeviceId,
                SessionInfo.MonitoringIntervalSeconds);

            var pauseWindows = ScreenshotPause.Deserialize(SessionInfo.ScreenshotPauseWindows);

            await Clients.Caller.StartMonitoring(
                SessionInfo.MonitoringIntervalSeconds,
                pauseWindows.Select(x => x.StartHour).ToArray(),
                pauseWindows.Select(x => x.EndHour).ToArray());
        }

        if (SessionInfo.CaptureRequested)
        {
            // The device was asked for a single screenshot, so it is taken as
            // soon as the device connects.
            _logger.LogInformation(
                "Requesting a screenshot from device {deviceId}.",
                SessionInfo.DeviceId);

            SessionInfo.CaptureRequested = false;
            await Clients.Caller.CaptureScreenshotNow();
        }

        if (SessionInfo.IsCameraMonitoring)
        {
            await StartCameraMonitoringForCaller();
        }
        else if (SessionInfo.IsCameraCapture)
        {
            // The device was asked to record a video clip from one of its
            // cameras, so the recording starts as soon as it connects.
            var request = SessionInfo.CameraCapture ?? new CameraCaptureRequest();

            _logger.LogInformation(
                "Starting camera capture on device {deviceId}.  Quality: {quality}.  Duration: {duration} seconds.",
                SessionInfo.DeviceId,
                request.Quality,
                request.DurationSeconds);

            await Clients.Caller.StartCameraCapture(
                SessionInfo.DeviceId,
                request.CameraId,
                (int)request.Quality,
                request.DurationSeconds);
        }

        if (SessionInfo.IsAudioLive)
        {
            await StartAudioLiveForCaller();
        }
        else if (SessionInfo.IsAudioMonitoring)
        {
            await StartAudioMonitoringForCaller();
        }
        else if (SessionInfo.IsAudioCapture)
        {
            // The device was asked to record an audio clip, so the recording
            // starts as soon as it connects.
            var request = SessionInfo.AudioCapture ?? new AudioCaptureRequest();

            _logger.LogInformation(
                "Starting audio capture on device {deviceId}.  Source: {source}.  Quality: {quality}.  Duration: {duration} seconds.",
                SessionInfo.DeviceId,
                request.Source,
                request.Quality,
                request.DurationSeconds);

            await Clients.Caller.StartAudioCapture(
                SessionInfo.DeviceId,
                request.Source,
                request.MicrophoneId,
                (int)request.Quality,
                request.DurationSeconds);
        }

        return Result.Ok();
    }

    /// <summary>
    /// Re-applies the server-owned camera schedule after the desktop client
    /// reconnects. A client-side schedule without a matching server session is
    /// stopped rather than trusted or resumed.
    /// </summary>
    public async Task ResumeCameraMonitoring()
    {
        if (!SessionInfo.IsCameraMonitoring)
        {
            await Clients.Caller.StopCameraMonitoring();
            return;
        }

        await StartCameraMonitoringForCaller();
    }

    private async Task StartCameraMonitoringForCaller()
    {
        var request = SessionInfo.CameraCapture ?? new CameraCaptureRequest();
        var pauseWindows = ScreenshotPause.Deserialize(SessionInfo.CameraPauseWindows);

        _logger.LogInformation(
            "Starting scheduled camera recording on device {deviceId}. Interval: {interval} seconds.",
            SessionInfo.DeviceId,
            SessionInfo.CameraMonitoringIntervalSeconds);

        await Clients.Caller.StartCameraMonitoring(
            SessionInfo.DeviceId,
            request,
            SessionInfo.CameraMonitoringIntervalSeconds,
            pauseWindows.Select(x => x.StartHour).ToArray(),
            pauseWindows.Select(x => x.EndHour).ToArray());
    }

    /// <summary>
    /// Asks the device to take a screenshot right away.
    /// </summary>
    public async Task CaptureScreenshotNow()
    {
        await Clients.Caller.CaptureScreenshotNow();
    }

    /// <summary>
    /// Receives a screenshot captured by a device that is monitoring.
    ///  </summary>
    public async Task SendScreenshot(byte[] screenshotBytes)
    {
        if (screenshotBytes is null || screenshotBytes.Length == 0)
        {
            return;
        }

        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        SessionInfo.LastActivity = DateTimeOffset.Now;

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return;
        }

        var screenshot = await _screenshotService.SaveScreenshot(deviceResult.Value, screenshotBytes);
        if (screenshot is null)
        {
            return;
        }

        _logger.LogInformation(
            "Received a monitoring screenshot from device {deviceId}.",
            deviceId);

        if (!string.IsNullOrWhiteSpace(SessionInfo.UserConnectionId))
        {
            await _messenger.Send(
                new ScreenshotReceivedMessage(deviceId, screenshot.FileName, screenshot.TimeStamp),
                SessionInfo.UserConnectionId);
        }
    }

    /// <summary>
    /// Receives the list of cameras that a device has.  It is sent by the
    /// device when it connects for a camera recording.
    /// </summary>
    public async Task SendCameraDevices(CameraDeviceInfo[] cameras)
    {
        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        SessionInfo.CameraDevices = cameras?.ToList() ?? new List<CameraDeviceInfo>();

        _logger.LogInformation(
            "Device {deviceId} reported {count} cameras.",
            deviceId,
            SessionInfo.CameraDevices.Count);

        if (!string.IsNullOrWhiteSpace(SessionInfo.UserConnectionId))
        {
            await _messenger.Send(
                new CameraDevicesReceivedMessage(deviceId, SessionInfo.CameraDevices.ToArray()),
                SessionInfo.UserConnectionId);
        }
    }

    /// <summary>
    /// Receives a chunk of a camera clip that a device recorded.  It is used
    /// when the clip can't be uploaded with a single HTTP request.
    /// </summary>
    public async Task SendCameraClipChunk(string clipId, int chunkIndex, byte[] chunk)
    {
        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId) || !SessionInfo.IsCameraCapture)
        {
            return;
        }

        if (chunk is null || chunk.Length == 0)
        {
            return;
        }

        if (chunkIndex == 0)
        {
            // The first chunk starts a new upload, so anything that was left
            // over from an earlier attempt is discarded.
            var deviceResult = await _dataService.GetDevice(deviceId);
            if (!deviceResult.IsSuccess ||
                _cameraService.BeginUpload(deviceResult.Value, clipId) is null)
            {
                return;
            }
        }

        _cameraService.AppendChunk(clipId, chunk);
    }

    /// <summary>
    /// Completes a camera clip that was uploaded in chunks.
    /// </summary>
    public async Task CameraClipUploadCompleted(
        string clipId,
        int quality,
        int durationSeconds,
        string cameraName)
    {
        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId) || !SessionInfo.IsCameraCapture)
        {
            return;
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            _cameraService.AbortUpload(clipId);
            return;
        }

        var clip = await _cameraService.CompleteUpload(
            deviceResult.Value,
            clipId,
            quality.ToString(),
            durationSeconds,
            cameraName);

        if (clip is null)
        {
            return;
        }

        _logger.LogInformation(
            "Received a camera clip from device {deviceId}.  Size: {size} bytes.",
            deviceId,
            clip.SizeBytes);

        if (!string.IsNullOrWhiteSpace(SessionInfo.UserConnectionId))
        {
            await _messenger.Send(
                new CameraClipReceivedMessage(
                    deviceId,
                    clip.FileName,
                    clip.TimeStamp,
                    clip.Quality,
                    clip.DurationSeconds),
                SessionInfo.UserConnectionId);
        }
    }

    /// <summary>
    /// Re-applies the server-owned audio schedule after the desktop client
    /// reconnects.  A client-side schedule without a matching server session
    /// is stopped rather than trusted or resumed.
    /// </summary>
    public async Task ResumeAudioMonitoring()
    {
        if (!SessionInfo.IsAudioMonitoring)
        {
            await Clients.Caller.StopAudioMonitoring();
            return;
        }

        await StartAudioMonitoringForCaller();
    }

    private async Task StartAudioMonitoringForCaller()
    {
        var request = SessionInfo.AudioCapture ?? new AudioCaptureRequest();
        var pauseWindows = ScreenshotPause.Deserialize(SessionInfo.AudioPauseWindows);

        _logger.LogInformation(
            "Starting scheduled audio recording on device {deviceId}. Interval: {interval} seconds. Source: {source}.",
            SessionInfo.DeviceId,
            SessionInfo.AudioMonitoringIntervalSeconds,
            request.Source);

        await Clients.Caller.StartAudioMonitoring(
            SessionInfo.DeviceId,
            request,
            SessionInfo.AudioMonitoringIntervalSeconds,
            pauseWindows.Select(x => x.StartHour).ToArray(),
            pauseWindows.Select(x => x.EndHour).ToArray());
    }

    private async Task StartAudioLiveForCaller()
    {
        var source = SessionInfo.AudioLiveSource ?? AudioSource.SystemAudio;

        _logger.LogInformation(
            "Starting live audio stream on device {deviceId}. Source: {source}.",
            SessionInfo.DeviceId,
            source);

        await Clients.Caller.StartAudioLive(
            SessionInfo.DeviceId,
            source,
            string.Empty);
    }

    /// <summary>
    /// Receives the list of microphones that a device has.  It is sent by the
    /// device when it connects for an audio recording.
    /// </summary>
    public async Task SendAudioDevices(AudioDeviceInfo[] devices)
    {
        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        SessionInfo.AudioDevices = devices?.ToList() ?? new List<AudioDeviceInfo>();

        _logger.LogInformation(
            "Device {deviceId} reported {count} microphones.",
            deviceId,
            SessionInfo.AudioDevices.Count);

        if (!string.IsNullOrWhiteSpace(SessionInfo.UserConnectionId))
        {
            await _messenger.Send(
                new AudioDevicesReceivedMessage(deviceId, SessionInfo.AudioDevices.ToArray()),
                SessionInfo.UserConnectionId);
        }
    }

    /// <summary>
    /// Receives a chunk of an audio clip that a device recorded.  It is used
    /// when the clip can't be uploaded with a single HTTP request.
    /// </summary>
    public async Task SendAudioClipChunk(string clipId, int chunkIndex, byte[] chunk)
    {
        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId) || !SessionInfo.IsAudioCapture)
        {
            return;
        }

        if (chunk is null || chunk.Length == 0)
        {
            return;
        }

        if (chunkIndex == 0)
        {
            // The first chunk starts a new upload, so anything that was left
            // over from an earlier attempt is discarded.
            var deviceResult = await _dataService.GetDevice(deviceId);
            if (!deviceResult.IsSuccess ||
                _audioService.BeginUpload(deviceResult.Value, clipId) is null)
            {
                return;
            }
        }

        _audioService.AppendChunk(clipId, chunk);
    }

    /// <summary>
    /// Completes an audio clip that was uploaded in chunks.
    /// </summary>
    public async Task AudioClipUploadCompleted(
        string clipId,
        int quality,
        int durationSeconds,
        int source)
    {
        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId) || !SessionInfo.IsAudioCapture)
        {
            return;
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            _audioService.AbortUpload(clipId);
            return;
        }

        var clip = await _audioService.CompleteUpload(
            deviceResult.Value,
            clipId,
            (AudioQuality)quality,
            durationSeconds,
            (AudioSource)source);

        if (clip is null)
        {
            return;
        }

        _logger.LogInformation(
            "Received an audio clip from device {deviceId}.  Size: {size} bytes.",
            deviceId,
            clip.SizeBytes);

        if (!string.IsNullOrWhiteSpace(SessionInfo.UserConnectionId))
        {
            await _messenger.Send(
                new AudioClipReceivedMessage(
                    deviceId,
                    clip.FileName,
                    clip.TimeStamp,
                    clip.Quality,
                    clip.DurationSeconds,
                    clip.Source),
                SessionInfo.UserConnectionId);
        }
    }

    /// <summary>
    /// Receives one chunk of a live audio stream, and forwards it to the
    /// circuit that is listening to it.
    /// </summary>
    public async Task SendAudioLiveChunk(byte[] chunk)
    {
        var deviceId = SessionInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId) || !SessionInfo.IsAudioLive)
        {
            return;
        }

        if (chunk is null || chunk.Length == 0)
        {
            return;
        }

        SessionInfo.LastActivity = DateTimeOffset.Now;

        if (string.IsNullOrWhiteSpace(SessionInfo.UserConnectionId))
        {
            return;
        }

        await _messenger.Send(
            new AudioLiveChunkMessage(deviceId, chunk),
            SessionInfo.UserConnectionId);
    }

    public Task SendConnectionFailedToViewers(List<string> viewerIDs)
    {
        return _viewerHub.Clients.Clients(viewerIDs).ConnectionFailed();
    }

    public Task SendConnectionRequestDenied(string viewerID)
    {
        return _viewerHub.Clients.Client(viewerID).ConnectionRequestDenied();
    }

    public async Task SendDesktopStream(IAsyncEnumerable<byte[]> stream, Guid streamId)
    {
        using var signaler = _streamCache.GetOrAdd(streamId, key => new StreamSignaler(streamId));
        signaler.DesktopConnectionId = Context.ConnectionId;

        try
        {
            signaler.Stream = stream;
            signaler.ReadySignal.Release();

            // TODO: We can remove the timeout once we implement add a
            // timeout for viewer idle (i.e. no input).
            await signaler.EndSignal.WaitAsync(TimeSpan.FromHours(8));
        }
        finally
        {
            _ = _streamCache.TryRemove(signaler.StreamId, out _);
        }
    }

    public Task SendDtoToViewer(byte[] dto, string viewerId)
    {
        return _viewerHub.Clients.Client(viewerId).SendDtoToViewer(dto);
    }

    public Task SendMessageToViewer(string viewerId, string message)
    {
        return _viewerHub.Clients.Client(viewerId).ShowMessage(message);
    }

    private async Task RestartScreenCaster()
    {
        SessionInfo.StreamerState = StreamerState.Reconnecting;
        await _viewerHub.Clients.Clients(ViewerList).Reconnecting();

        if (!_agentCache.TryGetConnectionId(SessionInfo.DeviceId, out var agentConnectionId))
        {
            await _viewerHub.Clients
                .Clients(SessionInfo.ViewerList)
                .ShowMessage("Waiting for agent to come online");

            return;
        }

        SessionInfo.AgentConnectionId = agentConnectionId;

        await _agentHub.Clients
                 .Client(SessionInfo.AgentConnectionId)
                 .RestartScreenCaster(
                        [.. SessionInfo.ViewerList],
                        $"{SessionInfo.UnattendedSessionId}",
                        SessionInfo.AccessKey,
                        SessionInfo.UserConnectionId,
                        SessionInfo.RequesterName,
                        SessionInfo.OrganizationName,
                        SessionInfo.OrganizationId);
    }
    private async Task NotifySessionChangedImpl(SessionSwitchReasonEx reason, int currentSessionId)
    {
        if (SessionInfo.RequireConsent)
        {
            // Don't restart if consent wasn't granted on the first request.
            return;
        }

        _logger.LogDebug("Windows session changed during remote control.  " +
            "Reason: {reason}.  " +
            "Current Session ID: {sessionId}.  " +
            "Session Info: {@sessionInfo}",
            reason,
            currentSessionId,
            SessionInfo);

        await _agentHub.Clients
            .Client(SessionInfo.AgentConnectionId)
            .RestartScreenCaster(
                [.. SessionInfo.ViewerList],
                $"{SessionInfo.UnattendedSessionId}",
                SessionInfo.AccessKey,
                SessionInfo.UserConnectionId,
                SessionInfo.RequesterUserName,
                SessionInfo.OrganizationName,
                SessionInfo.OrganizationId);
    }
}
