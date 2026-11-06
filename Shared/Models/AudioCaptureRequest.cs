namespace Remotely.Shared.Models;

/// <summary>
/// A request to record audio from a device, or to stream it while it is
/// captured.
/// </summary>
public class AudioCaptureRequest
{
    public AudioCaptureRequest()
    {
    }

    public AudioCaptureRequest(
        AudioSource source,
        AudioQuality quality,
        int durationSeconds,
        string microphoneId = "")
    {
        Source = source;
        Quality = quality;
        DurationSeconds = durationSeconds;
        MicrophoneId = microphoneId;
    }

    /// <summary>
    /// Whether the system audio or the microphone is captured.
    /// </summary>
    public AudioSource Source { get; set; } = AudioSource.SystemAudio;

    /// <summary>
    /// The sample format of the recording.
    /// </summary>
    public AudioQuality Quality { get; set; } = AudioQuality.Medium;

    /// <summary>
    /// How long the recording should run, in seconds.
    /// </summary>
    public int DurationSeconds { get; set; } = 30;

    /// <summary>
    /// The device path of the microphone, when one is selected.  When it is
    /// empty, the default microphone of the device is used.
    /// </summary>
    public string MicrophoneId { get; set; } = string.Empty;
}
