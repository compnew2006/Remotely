using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Streams the audio of this device to the server while an administrator is
/// listening to it in real time.
/// </summary>
public interface IAudioLiveStreamer
{
    bool IsRunning { get; }

    Task StartAsync(AudioCaptureRequest request);

    Task StopAsync();
}

internal sealed class AudioLiveStreamer : IAudioLiveStreamer, IDisposable
{
    private readonly IAudioRecorder _audioRecorder;
    private readonly IDesktopHubConnection _desktopHubConnection;
    private readonly ILogger<AudioLiveStreamer> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private CancellationTokenSource? _cancellationTokenSource;

    public AudioLiveStreamer(
        IAudioRecorder audioRecorder,
        IDesktopHubConnection desktopHubConnection,
        ILogger<AudioLiveStreamer> logger)
    {
        _audioRecorder = audioRecorder;
        _desktopHubConnection = desktopHubConnection;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public Task StartAsync(AudioCaptureRequest request)
    {
        Stop();

        var cancellationTokenSource = new CancellationTokenSource();
        _cancellationTokenSource = cancellationTokenSource;

        _audioRecorder.StartLive(request, chunk => OnChunk(chunk, cancellationTokenSource.Token));

        IsRunning = true;

        _logger.LogInformation(
            "Starting a live audio stream.  Source: {source}.",
            request.Source);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stop();
        return Task.CompletedTask;
    }

    private void Stop()
    {
        var cancellationTokenSource = _cancellationTokenSource;
        _cancellationTokenSource = null;

        if (cancellationTokenSource is not null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        try
        {
            _audioRecorder.StopLive();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping the live audio stream.");
        }

        IsRunning = false;
    }

    private void OnChunk(byte[] chunk, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _sendLock.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await _desktopHubConnection.SendAudioLiveChunk(chunk);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while sending a live audio chunk.");
            }
            finally
            {
                _sendLock.Release();
            }
        });
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
