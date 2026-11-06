using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;
using SkiaSharp;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Captures full screenshots of the desktop at a fixed interval and sends them
/// to the server.  This runs without any viewer attached, so it is used to
/// keep a device under observation.
/// </summary>
public interface IScreenMonitor
{
    bool IsRunning { get; }

    /// <summary>
    /// Captures a screenshot right away, without waiting for the monitoring
    /// interval.  When the periodic capture is running, it is woken up
    /// immediately.  Otherwise a single screenshot is taken and sent.
    /// </summary>
    void CaptureNow();

    void Start(int intervalSeconds, int[]? pauseStartHours = null, int[]? pauseEndHours = null);

    void Stop();
}

internal class ScreenMonitor : IScreenMonitor, IDisposable
{
    /// <summary>
    /// The largest screenshot that can be sent to the server.  SignalR closes
    /// the connection when a message exceeds the server's maximum receive
    /// message size, so the screenshot is compressed until it fits.
    /// </summary>
    private const int MaxScreenshotBytes = 80 * 1024;

    private readonly IAppState _appState;
    private readonly IImageHelper _imageHelper;
    private readonly ILogger<ScreenMonitor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _captureSignal = new(0, 1);

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _captureTask;
    private readonly List<ScreenshotPauseWindow> _pauseWindows = new();

    public ScreenMonitor(
        IAppState appState,
        IImageHelper imageHelper,
        IServiceProvider serviceProvider,
        ILogger<ScreenMonitor> logger)
    {
        _appState = appState;
        _imageHelper = imageHelper;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsRunning => _captureTask is not null && !_captureTask.IsCompleted;

    public void CaptureNow()
    {
        if (IsPaused())
        {
            _logger.LogInformation("A screenshot was requested, but it is currently paused.");
            return;
        }

        if (IsRunning)
        {
            // Wake the capture loop up so it takes the screenshot right away.
            try
            {
                _captureSignal.Release();
            }
            catch (SemaphoreFullException)
            {
                // A capture was already requested.
            }

            return;
        }

        // The periodic capture is not running, so a single screenshot is
        // taken and sent on its own.
        _ = Task.Run(CaptureOnceAsync);
    }

    public void Start(int intervalSeconds, int[]? pauseStartHours = null, int[]? pauseEndHours = null)
    {
        Stop();

        if (intervalSeconds < 10)
        {
            intervalSeconds = 10;
        }

        _pauseWindows.Clear();
        pauseStartHours ??= Array.Empty<int>();
        pauseEndHours ??= Array.Empty<int>();

        for (var i = 0; i < pauseStartHours.Length && i < pauseEndHours.Length; i++)
        {
            _pauseWindows.Add(new ScreenshotPauseWindow(pauseStartHours[i], pauseEndHours[i]));
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _captureTask = Task.Run(() => CaptureLoopAsync(intervalSeconds, _cancellationTokenSource.Token));
    }

    private bool IsPaused()
    {
        return ScreenshotPause.IsPaused(_pauseWindows, DateTimeOffset.Now);
    }

    public void Stop()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch { }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _captureTask = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private async Task CaptureLoopAsync(int intervalSeconds, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting screen monitoring.  Interval: {interval} seconds.", intervalSeconds);

        using var capturer = _serviceProvider.GetRequiredService<IScreenCapturer>();

        try
        {
            capturer.Init();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while initializing the screen capturer for monitoring.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for the interval to pass, or for a capture to be
                // requested right away.
                await _captureSignal.WaitAsync(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (IsPaused())
                {
                    // Screenshots are paused for this time of day.
                    continue;
                }

                await CaptureAndSendAsync(capturer);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while capturing a monitoring screenshot.");
            }
        }

        _logger.LogInformation("Screen monitoring stopped.");
    }

    /// <summary>
    /// Takes a single screenshot and sends it to the server.  Used when a
    /// screenshot is requested right away and the periodic capture is not
    /// running.
    /// </summary>
    private async Task CaptureOnceAsync()
    {
        using var capturer = _serviceProvider.GetRequiredService<IScreenCapturer>();

        try
        {
            capturer.Init();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while initializing the screen capturer.");
        }

        try
        {
            await CaptureAndSendAsync(capturer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while capturing a screenshot.");
        }
    }

    private async Task CaptureAndSendAsync(IScreenCapturer capturer)
    {
        // The capturer owns the frame it returns, so it must not be disposed
        // here.
        var frame = capturer.GetFullFrame();
        if (!frame.IsSuccess)
        {
            _logger.LogWarning("Failed to capture a screenshot: {reason}", frame.Reason);
            return;
        }

        var jpeg = EncodeWithinLimit(frame.Value);
        if (jpeg is null)
        {
            _logger.LogWarning("The screenshot is too large to send.");
            return;
        }

        // The hub connection is resolved lazily: taking it as a constructor
        // dependency would create a circular dependency with the desktop hub
        // connection that starts the monitoring.
        var desktopHubConnection = _serviceProvider.GetRequiredService<IDesktopHubConnection>();
        await desktopHubConnection.SendScreenshot(jpeg);
    }

    /// <summary>
    /// Encodes the frame as a JPEG that fits within the maximum message size
    /// that the server accepts.  The quality is lowered first, and the image
    /// is scaled down when the quality is not enough.
    /// </summary>
    private byte[]? EncodeWithinLimit(SKBitmap frame)
    {
        var jpeg = _imageHelper.EncodeBitmap(frame, SKEncodedImageFormat.Jpeg, 80);
        if (jpeg.Length <= MaxScreenshotBytes)
        {
            return jpeg;
        }

        foreach (var quality in new[] { 60, 45, 30 })
        {
            jpeg = _imageHelper.EncodeBitmap(frame, SKEncodedImageFormat.Jpeg, quality);
            if (jpeg.Length <= MaxScreenshotBytes)
            {
                return jpeg;
            }
        }

        for (var scale = 0.75; scale >= 0.25; scale -= 0.25)
        {
            using var scaled = new SKBitmap(
                Math.Max(1, (int)(frame.Width * scale)),
                Math.Max(1, (int)(frame.Height * scale)));

            using (var canvas = new SKCanvas(scaled))
            {
                canvas.DrawBitmap(
                    frame,
                    new SKRect(0, 0, frame.Width, frame.Height),
                    new SKRect(0, 0, scaled.Width, scaled.Height));
            }

            jpeg = _imageHelper.EncodeBitmap(scaled, SKEncodedImageFormat.Jpeg, 60);
            if (jpeg.Length <= MaxScreenshotBytes)
            {
                return jpeg;
            }
        }

        return jpeg.Length <= MaxScreenshotBytes ? jpeg : null;
    }
}
