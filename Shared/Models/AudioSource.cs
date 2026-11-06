namespace Remotely.Shared.Models;

/// <summary>
/// The place that the audio of a device is captured from.
/// </summary>
public enum AudioSource
{
    /// <summary>
    /// Everything the device plays through its speakers, captured with a
    /// loopback device.  This includes meetings, calls, and media.
    /// </summary>
    SystemAudio = 0,

    /// <summary>
    /// The device's microphone.
    /// </summary>
    Microphone = 1
}
