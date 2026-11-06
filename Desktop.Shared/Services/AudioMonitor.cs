using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Records an audio clip at a fixed interval without a viewer attached.
/// </summary>
public interface IAudioMonitor
{
    bool IsRunning { get; }

    Task StartAsync(
        string deviceId,
        AudioCaptureRequest request,
        int intervalSeconds,
        int[]? pauseStartHours = null,
        int[]? pauseEndHours = null);

    Task StopAsync();
}

internal sealed class AudioMonitor : IAudioMonitor, IDisposable
{
    private readonly IAppState _appState;
    private readonly ILogger<AudioMonitor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private volatile ScreenshotPauseWindow[] _pauseWindows = Array.Empty<ScreenshotPauseWindow>();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _captureTask;
    private AudioMonitorConfiguration? _configuration;

    public AudioMonitor(
        IAppState appState,
        IServiceProvider serviceProvider,
        ILogger<AudioMonitor> logger)
    {
        _appState = appState;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsRunning => _captureTask is { IsCompleted: false };

    public async Task StartAsync(
        string deviceId,
        AudioCaptureRequest request,
        int intervalSeconds,
        int[]? pauseStartHours = null,
        int[]? pauseEndHours = null)
    {
        await _lifecycleLock.WaitAsync();

        try
        {
            intervalSeconds = Math.Clamp(intervalSeconds, 60, 30 * 60);
            pauseStartHours ??= Array.Empty<int>();
            pauseEndHours ??= Array.Empty<int>();

            var configuration = new AudioMonitorConfiguration(
                deviceId,
                request.Source,
                request.MicrophoneId,
                request.Quality,
                AudioQualityPresets.ClampDurationSeconds(request.DurationSeconds),
                intervalSeconds,
                string.Join(',', pauseStartHours) + "|" + string.Join(',', pauseEndHours));

            if (IsRunning && _configuration is { } currentConfiguration &&
                currentConfiguration with { PauseWindows = configuration.PauseWindows } == configuration)
            {
                _pauseWindows = BuildPauseWindows(pauseStartHours, pauseEndHours);
                _configuration = configuration;
                return;
            }

            await StopCoreAsync();

            _pauseWindows = BuildPauseWindows(pauseStartHours, pauseEndHours);

            var captureRequest = new AudioCaptureRequest(
                request.Source,
                request.Quality,
                configuration.DurationSeconds,
                request.MicrophoneId);
            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            _configuration = configuration;
            _appState.IsAudioMonitoring = true;
            _captureTask = Task.Run(() => CaptureLoopAsync(
                configuration.DeviceId,
                captureRequest,
                configuration.IntervalSeconds,
                cancellationTokenSource.Token));

            _logger.LogInformation(
                "Starting scheduled audio recording. Interval: {interval} seconds. Duration: {duration} seconds. Source: {source}.",
                intervalSeconds,
                configuration.DurationSeconds,
                configuration.Source);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync();

        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        var cancellationTokenSource = _cancellationTokenSource;
        var captureTask = _captureTask;
        var wasRunning = cancellationTokenSource is not null || _appState.IsAudioMonitoring;

        _cancellationTokenSource = null;
        _captureTask = null;
        _configuration = null;
        _appState.IsAudioMonitoring = false;

        if (cancellationTokenSource is not null)
        {
            cancellationTokenSource.Cancel();
        }

        if (wasRunning)
        {
            try
            {
                _serviceProvider.GetRequiredService<IAudioCaptureService>().Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not cancel the current scheduled audio clip.");
            }
        }

        if (captureTask is not null)
        {
            try
            {
                await captureTask;
            }
            catch (OperationCanceledException)
            {
                // The schedule was stopped while a clip was being captured.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping scheduled audio recording.");
            }
        }

        cancellationTokenSource?.Dispose();
    }

    private async Task CaptureLoopAsync(
        string deviceId,
        AudioCaptureRequest request,
        int intervalSeconds,
        CancellationToken cancellationToken)
    {
        var captureService = _serviceProvider.GetRequiredService<IAudioCaptureService>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (ScreenshotPause.IsPaused(_pauseWindows, DateTimeOffset.Now))
            {
                continue;
            }

            try
            {
                var result = await captureService.CaptureAsync(deviceId, request, cancellationToken);
                if (!result.IsSuccess && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("A scheduled audio clip was not recorded: {reason}", result.Reason);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while capturing a scheduled audio clip.");
            }
        }

        _logger.LogInformation("Scheduled audio recording stopped.");
    }

    private static ScreenshotPauseWindow[] BuildPauseWindows(int[] starts, int[] ends)
    {
        return starts
            .Zip(ends)
            .Select(x => new ScreenshotPauseWindow(x.First, x.Second))
            .ToArray();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _appState.IsAudioMonitoring = false;
        GC.SuppressFinalize(this);
    }

    private sealed record AudioMonitorConfiguration(
        string DeviceId,
        AudioSource Source,
        string MicrophoneId,
        AudioQuality Quality,
        int DurationSeconds,
        int IntervalSeconds,
        string PauseWindows);
}
