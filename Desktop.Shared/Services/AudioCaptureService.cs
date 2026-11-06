using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Records the audio clips of this device and sends them to the server.
/// </summary>
public interface IAudioCaptureService
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
        AudioCaptureRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the microphones of this device to the server, so the
    /// administrator can pick one of them.
    /// </summary>
    Task SendAudioDevices();
}

/// <summary>
/// Records the audio clips of this device and sends them to the server.  The
/// clip is uploaded with a single HTTP request when that is possible, and it
/// falls back to sending it in chunks over the hub when the upload fails.
/// </summary>
internal class AudioCaptureService : IAudioCaptureService
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
    private readonly IAudioRecorder _audioRecorder;
    private readonly IDesktopHubConnection _desktopHubConnection;
    private readonly ILogger<AudioCaptureService> _logger;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly object _captureSync = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private long _cancelGeneration;

    public AudioCaptureService(
        IAppState appState,
        IAudioRecorder audioRecorder,
        IDesktopHubConnection desktopHubConnection,
        ILogger<AudioCaptureService> logger)
    {
        _appState = appState;
        _audioRecorder = audioRecorder;
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

    public async Task SendAudioDevices()
    {
        try
        {
            var devices = _audioRecorder.GetMicrophones();
            await _desktopHubConnection.SendAudioDevices(devices.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while listing the microphones of this device.");
        }
    }

    public async Task<Result> CaptureAsync(
        string deviceId,
        AudioCaptureRequest request,
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
            return Result.Fail("The audio recording was canceled.");
        }

        CancellationTokenSource captureCancellation;
        lock (_captureSync)
        {
            if (cancelGeneration != _cancelGeneration)
            {
                _captureLock.Release();
                return Result.Fail("The audio recording was canceled.");
            }

            captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationTokenSource = captureCancellation;
        }

        var captureToken = captureCancellation.Token;

        try
        {
            var durationSeconds = AudioQualityPresets.ClampDurationSeconds(request.DurationSeconds);

            _logger.LogInformation(
                "Recording an audio clip.  Source: {source}.  Quality: {quality}.  Duration: {duration} seconds.",
                request.Source,
                request.Quality,
                durationSeconds);

            var recordResult = await _audioRecorder.RecordAsync(
                new AudioCaptureRequest(
                    request.Source,
                    request.Quality,
                    durationSeconds,
                    request.MicrophoneId),
                captureToken);

            if (!recordResult.IsSuccess)
            {
                return Result.Fail(recordResult.Reason);
            }

            var path = recordResult.Value;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return Result.Fail("The audio clip was not recorded.");
            }

            try
            {
                var uploaded = await UploadAsync(deviceId, path, request, durationSeconds, captureToken);
                if (!uploaded.IsSuccess)
                {
                    return uploaded;
                }
            }
            finally
            {
                TryDeleteFile(path);
            }

            _logger.LogInformation("The audio clip was sent to the server.");

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("The audio recording was canceled.");
            return Result.Fail("The audio recording was canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while recording an audio clip.");
            return Result.Fail("The audio clip could not be recorded.");
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
    /// Sends the clip to the server.  A single HTTP request is used when it
    /// works, and the clip is sent in chunks over the hub when it doesn't.
    /// </summary>
    private async Task<Result> UploadAsync(
        string deviceId,
        string path,
        AudioCaptureRequest request,
        int durationSeconds,
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
                cancellationToken);

            if (uploaded)
            {
                return Result.Ok();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The audio clip could not be uploaded with HTTP.  Falling back to the hub.");
        }

        try
        {
            await UploadWithHubAsync(
                clipId,
                path,
                request,
                durationSeconds,
                cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The audio clip could not be uploaded.");
            return Result.Fail("The audio clip could not be sent to the server.");
        }
    }

    /// <summary>
    /// Uploads the clip with a single HTTP request.
    /// </summary>
    private async Task<bool> UploadWithHttpAsync(
        string deviceId,
        string clipId,
        string path,
        AudioCaptureRequest request,
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_appState.Host))
        {
            return false;
        }

        var url = $"{_appState.Host}/api/audioClips/{Uri.EscapeDataString(deviceId)}/upload" +
            $"?clipId={Uri.EscapeDataString(clipId)}" +
            $"&quality={(int)request.Quality}" +
            $"&durationSeconds={durationSeconds}" +
            $"&source={(int)request.Source}";

        await using var fileStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

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
                "The audio clip upload returned the status code {statusCode}.",
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
        AudioCaptureRequest request,
        int durationSeconds,
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

            await _desktopHubConnection.SendAudioClipChunk(clipId, chunkIndex, chunk);
            chunkIndex++;
        }

        await _desktopHubConnection.SendAudioClipUploadCompleted(
            clipId,
            (int)request.Quality,
            durationSeconds,
            (int)request.Source);
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
            _logger.LogWarning(ex, "Error while deleting the audio clip {path}.", path);
        }
    }
}
