using Remotely.Shared.Models;

namespace Remotely.Shared.Utilities;

/// <summary>
/// The sample formats that are available for audio recordings, and the limits
/// that are applied to the duration.
/// </summary>
public static class AudioQualityPresets
{
    public const int DefaultDurationSeconds = 30;
    public const int MinDurationSeconds = 5;
    public const int MaxDurationSeconds = 600;

    private static readonly AudioQualityPreset[] _presets =
    [
        new(AudioQuality.Low, 16000, 1, 16, "Low (16 kHz mono)"),
        new(AudioQuality.Medium, 22050, 1, 16, "Medium (22 kHz mono)"),
        new(AudioQuality.High, 44100, 1, 16, "High (44 kHz mono)")
    ];

    /// <summary>
    /// Returns the preset for a quality level.
    /// </summary>
    public static AudioQualityPreset GetPreset(AudioQuality quality)
    {
        return _presets.FirstOrDefault(x => x.Quality == quality) ?? _presets[1];
    }

    /// <summary>
    /// Returns true when the value matches one of the known quality levels.
    /// </summary>
    public static bool IsKnownQuality(string? value)
    {
        return Enum.TryParse<AudioQuality>(value, ignoreCase: true, out var quality) &&
            Enum.IsDefined(quality);
    }

    /// <summary>
    /// Reads a quality level from its name, and falls back to the medium
    /// preset for anything that is not recognized.
    /// </summary>
    public static AudioQuality ParseQuality(string? value)
    {
        if (Enum.TryParse<AudioQuality>(value, ignoreCase: true, out var quality) &&
            Enum.IsDefined(quality))
        {
            return quality;
        }

        return AudioQuality.Medium;
    }

    /// <summary>
    /// Brings a duration into the range that a device is allowed to record.
    /// </summary>
    public static int ClampDurationSeconds(int seconds)
    {
        return Math.Clamp(seconds, MinDurationSeconds, MaxDurationSeconds);
    }

    /// <summary>
    /// The number of bytes that one second of the preset occupies, before the
    /// WAV header.
    /// </summary>
    public static int GetBytesPerSecond(AudioQualityPreset preset)
    {
        return preset.SampleRate * preset.Channels * (preset.BitsPerSample / 8);
    }
}
