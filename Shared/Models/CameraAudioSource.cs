namespace Remotely.Shared.Models;

/// <summary>
/// The place that the audio of a recording is captured from.
/// </summary>
public enum CameraAudioSource
{
    /// <summary>
    /// The recording has no audio.
    /// </summary>
    None = 0,

    /// <summary>
    /// Everything the device plays, captured with a loopback device.
    /// </summary>
    SystemAudio = 1,

    /// <summary>
    /// One of the microphones of the device.
    /// </summary>
    Microphone = 2
}
