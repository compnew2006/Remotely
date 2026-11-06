namespace Remotely.Shared.Models;

/// <summary>
/// An audio source that is available on a remote device, so it can be put
/// into a recording next to the video.
/// </summary>
public class CameraAudioDeviceInfo
{
    public CameraAudioDeviceInfo()
    {
    }

    public CameraAudioDeviceInfo(
        string id,
        string name,
        CameraAudioSource source,
        bool isDefault = false)
    {
        Id = id;
        Name = name;
        Source = source;
        IsDefault = isDefault;
    }

    /// <summary>
    /// The device name that the recording engine uses to capture the audio.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the device that is shown to the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this device captures the system audio or a microphone.
    /// </summary>
    public CameraAudioSource Source { get; set; } = CameraAudioSource.Microphone;

    /// <summary>
    /// Whether this is the default device of its kind.
    /// </summary>
    public bool IsDefault { get; set; }
}
