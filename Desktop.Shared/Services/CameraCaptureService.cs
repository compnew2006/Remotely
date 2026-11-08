using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Records the video clips of this device's cameras and sends them to the
/// server.
/// </summary>
public interface ICameraCaptureService
{
    /// <summary>
    /// Stops the recording that is currently running, if any.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Records a clip and sends it to the server.
    /// </summary>
    Task<Result> CaptureAsync(
        string deviceId,
        CameraCaptureRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the cameras of this device to the server, so the administrator
    /// can pick one of them.
    /// </summary>
    Task SendCameraDevices();
}

/// <summary>
/// Records the video clips of this device's cameras and sends them to the
/// server.  The clip is uploaded with a single HTTP request when that is
/// possible, and it falls back to sending it in chunks over the hub when the
/// upload fails.
/// </summary>
internal class CameraCaptureService : ICameraCaptureService
{
    /// <summary>
    /// The size of the chunks that are sent over the hub.  It is smaller
    /// than the maximum message size that the server accepts.
    /// </summary>
    private const int ChunkSize = 80 * 1024;

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private readonly IAppState _appState;
    private readonly ICameraCapturer _cameraCapturer;
    private readonly IDesktopHubConnection _desktopHubConnection;
    private readonly ILogger<CameraCaptureService> _logger;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly object _captureSync = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private long _cancelGeneration;

    public CameraCaptureService(
        IAppState appState,
        ICameraCapturer cameraCapturer,
        IDesktopHubConnection desktopHubConnection,
        ILogger<CameraCaptureService> logger)
    {
        _appState = appState;
        _cameraCapturer = cameraCapturer;
        _desktopHubConnection = desktopHubConnection;
        _logger = logger;
    }

    public void Cancel()
    {
        lock (_captureSync)
        {
            _cancelGeneration++;

            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch
            {
                // The recording was already stopped.
            }
        }
    }

    public async Task SendCameraDevices()
    {
        try
        {
            var cameras = _cameraCapturer.GetCameras();

            // The cameras are sent first, so the administrator can pick a
            // screen even when the audio devices can't be listed (or the
            // listing of audio devices fails on this device).
            await _desktopHubConnection.SendCameraDevices(
                cameras.ToArray(),
                Array.Empty<CameraAudioDeviceInfo>());

            var audioDevices = _cameraCapturer.GetAudioDevices();
            await _desktopHubConnection.SendCameraDevices(
                cameras.ToArray(),
                audioDevices.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while listing the cameras of this device.");
        }
    }

    public async Task<Result> CaptureAsync(
        string deviceId,
        CameraCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Result.Fail("The device id is not known.");
        }

        long cancelGeneration;
        lock (_captureSync)
        {
            cancelGeneration = _cancelGeneration;
        }

        try
        {
            await _captureLock.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("The camera recording was canceled.");
        }

        CancellationTokenSource captureCancellation;
        lock (_captureSync)
        {
            if (cancelGeneration != _cancelGeneration)
            {
                _captureLock.Release();
                return Result.Fail("The camera recording was canceled.");
            }

            captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationTokenSource = captureCancellation;
        }

        var captureToken = captureCancellation.Token;

        try
        {
            var cameras = _cameraCapturer.GetCameras();
            if (cameras.Count == 0)
            {
                return Result.Fail("This device does not have a camera or a screen that can be recorded.");
            }

            var camera = SelectCamera(cameras, request);
            if (camera is null)
            {
                return Result.Fail("The selected source is not available on this device.");
            }

            var durationSeconds = CameraQualityPresets.ClampDurationSeconds(request.DurationSeconds);

            _logger.LogInformation(
                "Recording a video clip.  Source: {source}.  Audio: {audio}.  Quality: {quality}.  Duration: {duration} seconds.",
                camera.Name,
                request.AudioSource,
                request.Quality,
                durationSeconds);

            var recordResult = await _cameraCapturer.RecordAsync(
                new CameraCaptureRequest(
                    camera.Id,
                    request.Quality,
                    durationSeconds,
                    camera.Kind,
                    request.AudioSource,
                    request.AudioDeviceId),
                captureToken);

            if (!recordResult.IsSuccess)
            {
                return Result.Fail(recordResult.Reason);
            }

            var path = recordResult.Value;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return Result.Fail("The camera clip was not recorded.");
            }

            try
            {
                // The upload runs after the recording itself, so it must
                // not use the capture cancellation token: pressing Stop
                // while recording cancels that token, and it would cancel
                // the upload of the clip that was just recorded, discarding
                // it. On a stop the clip is saved (this is what stopping
                // the recording is supposed to do).
                var uploaded = await UploadAsync(
                    deviceId,
                    path,
                    request,
                    durationSeconds,
                    camera.Name,
                    CancellationToken.None);
                if (!uploaded.IsSuccess)
                {
                    return uploaded;
                }
            }
            finally
            {
                TryDeleteFile(path);
            }

            _logger.LogInformation("The camera clip was sent to the server.");

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("The camera recording was canceled.");
            return Result.Fail("The camera recording was canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while recording a camera clip.");
            return Result.Fail("The camera clip could not be recorded.");
        }
        finally
        {
            lock (_captureSync)
            {
                if (ReferenceEquals(_cancellationTokenSource, captureCancellation))
                {
                    _cancellationTokenSource = null;
                }
            }

            captureCancellation.Dispose();
            _captureLock.Release();
        }
    }

    /// <summary>
    /// Finds the video source that was picked.  The source kind decides
    /// whether a camera or a display is looked for, and the first source of
    /// that kind is used when nothing was picked.
    /// </summary>
    private static CameraDeviceInfo? SelectCamera(
        IReadOnlyList<CameraDeviceInfo> cameras,
        CameraCaptureRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CameraId))
        {
            var selected = cameras.FirstOrDefault(x =>
                string.Equals(x.Id, request.CameraId, StringComparison.OrdinalIgnoreCase) &&
                x.Kind == request.VideoSource);

            if (selected is not null)
            {
                return selected;
            }
        }

        return cameras.FirstOrDefault(x => x.Kind == request.VideoSource);
    }

    /// <summary>
    /// Sends the clip to the server.  A single HTTP request is used when it
    /// works, and the clip is sent in chunks over the hub when it doesn't.
    /// </summary>
    private async Task<Result> UploadAsync(
        string deviceId,
        string path,
        CameraCaptureRequest request,
        int durationSeconds,
        string cameraName,
        CancellationToken cancellationToken)
    {
        var clipId = Guid.NewGuid().ToString();

        try
        {
            var uploaded = await UploadWithHttpAsync(
                deviceId,
                clipId,
                path,
                request,
                durationSeconds,
                cameraName,
                cancellationToken);

            if (uploaded)
            {
                return Result.Ok();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The camera clip could not be uploaded with HTTP.  Falling back to the hub.");
        }

        try
        {
            await UploadWithHubAsync(
                clipId,
                path,
                request,
                durationSeconds,
                cameraName,
                cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The camera clip could not be uploaded.");
            return Result.Fail("The camera clip could not be sent to the server.");
        }
    }

    /// <summary>
    /// Uploads the clip with a single HTTP request.
    /// </summary>
    private async Task<bool> UploadWithHttpAsync(
        string deviceId,
        string clipId,
        string path,
        CameraCaptureRequest request,
        int durationSeconds,
        string cameraName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_appState.Host))
        {
            return false;
        }

        var url = $"{_appState.Host}/api/cameras/{Uri.EscapeDataString(deviceId)}/upload" +
            $"?clipId={Uri.EscapeDataString(clipId)}" +
            $"&quality={(int)request.Quality}" +
            $"&durationSeconds={durationSeconds}" +
            $"&cameraName={Uri.EscapeDataString(cameraName)}";

        await using var fileStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        message.Headers.TryAddWithoutValidation(
            AppConstants.SessionKeyHeaderName,
            _appState.AccessKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "The camera clip upload returned the status code {statusCode}.",
                (int)response.StatusCode);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Uploads the clip in chunks over the hub.
    /// </summary>
    private async Task UploadWithHubAsync(
        string clipId,
        string path,
        CameraCaptureRequest request,
        int durationSeconds,
        string cameraName,
        CancellationToken cancellationToken)
    {
        var chunkIndex = 0;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var buffer = new byte[ChunkSize];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            var chunk = new byte[read];
            Array.Copy(buffer, chunk, read);

            await _desktopHubConnection.SendCameraClipChunk(clipId, chunkIndex, chunk);
            chunkIndex++;
        }

        await _desktopHubConnection.SendCameraClipUploadCompleted(
            clipId,
            (int)request.Quality,
            durationSeconds,
            cameraName);
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting the camera clip {path}.", path);
        }
    }
}
