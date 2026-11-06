using Remotely.Shared.Models;

namespace Remotely.Shared.Utilities;

/// <summary>
/// The quality presets that a camera recording can be made with, and the
/// limits that apply to the duration of a recording.
/// </summary>
public static class CameraQualityPresets
{
    /// <summary>
    /// The shortest recording that can be made, in seconds.
    /// </summary>
    public const int MinDurationSeconds = 5;

    /// <summary>
    /// The longest recording that can be made, in seconds.
    /// </summary>
    public const int MaxDurationSeconds = 300;

    /// <summary>
    /// The duration that is used when none is given, in seconds.
    /// </summary>
    public const int DefaultDurationSeconds = 30;

    /// <summary>
    /// Returns the encoder settings that belong to a quality level.  Any
    /// quality that is not known falls back to the medium preset.
    /// </summary>
    public static CameraQualityPreset GetPreset(CameraQuality quality)
    {
        return quality switch
        {
            CameraQuality.Low => new CameraQualityPreset(640, 480, 15, 1_000_000),
            CameraQuality.High => new CameraQualityPreset(1920, 1080, 30, 5_000_000),
            _ => new CameraQualityPreset(1280, 720, 30, 2_500_000)
        };
    }

    /// <summary>
    /// Brings a requested duration into the allowed range.
    /// </summary>
    public static int ClampDurationSeconds(int durationSeconds)
    {
        if (durationSeconds < MinDurationSeconds)
        {
            return MinDurationSeconds;
        }

        if (durationSeconds > MaxDurationSeconds)
        {
            return MaxDurationSeconds;
        }

        return durationSeconds;
    }

    /// <summary>
    /// Reads a quality level from a name.  Anything that cannot be read
    /// falls back to the medium quality, so a bad value never stops a
    /// recording.
    /// </summary>
    public static CameraQuality ParseQuality(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CameraQuality.Medium;
        }

        if (Enum.TryParse<CameraQuality>(value, true, out var quality) &&
            Enum.IsDefined(typeof(CameraQuality), quality))
        {
            return quality;
        }

        return CameraQuality.Medium;
    }
}
