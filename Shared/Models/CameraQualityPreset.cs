namespace Remotely.Shared.Models;

/// <summary>
/// The encoder settings that are used for a camera recording at a given
/// quality level.
/// </summary>
/// <param name="Width">The width of the recorded video, in pixels.</param>
/// <param name="Height">The height of the recorded video, in pixels.</param>
/// <param name="Framerate">The number of frames per second.</param>
/// <param name="Bitrate">The target bitrate, in bits per second.</param>
public record CameraQualityPreset(
    int Width,
    int Height,
    int Framerate,
    int Bitrate);
