using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Records a camera clip at a fixed interval without a viewer attached.
/// </summary>
public interface ICameraMonitor
{
    bool IsRunning { get; }

    Task StartAsync(
        string deviceId,
        CameraCaptureRequest request,
        int intervalSeconds,
        int[]? pauseStartHours = null,
        int[]? pauseEndHours = null);

    Task StopAsync();
}

internal sealed class CameraMonitor : ICameraMonitor, IDisposable
{
    private readonly IAppState _appState;
    private readonly ILogger<CameraMonitor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private volatile ScreenshotPauseWindow[] _pauseWindows = Array.Empty<ScreenshotPauseWindow>();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _captureTask;
    private CameraMonitorConfiguration? _configuration;

    public CameraMonitor(
        IAppState appState,
        IServiceProvider serviceProvider,
        ILogger<CameraMonitor> logger)
    {
        _appState = appState;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsRunning => _captureTask is { IsCompleted: false };

    public async Task StartAsync(
        string deviceId,
        CameraCaptureRequest request,
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

            var configuration = new CameraMonitorConfiguration(
                deviceId,
                request.CameraId,
                request.Quality,
                CameraQualityPresets.ClampDurationSeconds(request.DurationSeconds),
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

            var captureRequest = new CameraCaptureRequest(
                request.CameraId,
                request.Quality,
                configuration.DurationSeconds);
            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            _configuration = configuration;
            _appState.IsCameraMonitoring = true;
            _captureTask = Task.Run(() => CaptureLoopAsync(
                configuration.DeviceId,
                captureRequest,
                configuration.IntervalSeconds,
                cancellationTokenSource.Token));

            _logger.LogInformation(
                "Starting scheduled camera recording. Interval: {interval} seconds. Duration: {duration} seconds.",
                intervalSeconds,
                configuration.DurationSeconds);
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
        var wasRunning = cancellationTokenSource is not null || _appState.IsCameraMonitoring;

        _cancellationTokenSource = null;
        _captureTask = null;
        _configuration = null;
        _appState.IsCameraMonitoring = false;

        if (cancellationTokenSource is not null)
        {
            cancellationTokenSource.Cancel();
        }

        if (wasRunning)
        {
            try
            {
                _serviceProvider.GetRequiredService<ICameraCaptureService>().Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not cancel the current scheduled camera clip.");
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
                _logger.LogError(ex, "Error while stopping scheduled camera recording.");
            }
        }

        cancellationTokenSource?.Dispose();
    }

    private async Task CaptureLoopAsync(
        string deviceId,
        CameraCaptureRequest request,
        int intervalSeconds,
        CancellationToken cancellationToken)
    {
        var captureService = _serviceProvider.GetRequiredService<ICameraCaptureService>();

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
                    _logger.LogWarning("A scheduled camera clip was not recorded: {reason}", result.Reason);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while capturing a scheduled camera clip.");
            }
        }

        _logger.LogInformation("Scheduled camera recording stopped.");
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
        _appState.IsCameraMonitoring = false;
        GC.SuppressFinalize(this);
    }

    private sealed record CameraMonitorConfiguration(
        string DeviceId,
        string CameraId,
        CameraQuality Quality,
        int DurationSeconds,
        int IntervalSeconds,
        string PauseWindows);
}
