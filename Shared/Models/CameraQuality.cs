namespace Remotely.Shared.Models;

/// <summary>
/// The quality presets that a camera recording can be made with.  Each
/// preset maps to a resolution, a frame rate, and a bitrate.
/// </summary>
public enum CameraQuality
{
    Low = 0,
    Medium = 1,
    High = 2
}
