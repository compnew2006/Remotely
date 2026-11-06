namespace Remotely.Shared.Models;

/// <summary>
/// The sample format that is used for a recorded audio clip.  All clips are
/// stored as uncompressed PCM WAV, so the browser can play them without an
/// extra codec.
/// </summary>
public record AudioQualityPreset(
    AudioQuality Quality,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    string Label);
