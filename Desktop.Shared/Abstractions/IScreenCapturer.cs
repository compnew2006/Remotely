using Remotely.Shared.Primitives;
using SkiaSharp;
using System.Drawing;

namespace Remotely.Desktop.Shared.Abstractions;

public interface IScreenCapturer : IDisposable
{
    event EventHandler<Rectangle> ScreenChanged;

    bool CaptureFullscreen { get; set; }
    Rectangle CurrentScreenBounds { get; }
    bool IsGpuAccelerated { get; }
    string SelectedScreen { get; }
    IEnumerable<string> GetDisplayNames();
    SKRect GetFrameDiffArea();

    Result<SKBitmap> GetImageDiff();

    /// <summary>
    /// Captures a full frame of the screen, whether or not anything changed
    /// since the last capture.  This is what periodic screenshots use, since
    /// <see cref="GetNextFrame"/> fails when the screen is static.
    /// </summary>
    Result<SKBitmap> GetFullFrame();

    Result<SKBitmap> GetNextFrame();

    int GetScreenCount();

    Rectangle GetVirtualScreenBounds();

    void Init();

    void SetSelectedScreen(string displayName);
}
