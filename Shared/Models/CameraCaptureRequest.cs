namespace Remotely.Shared.Models;

/// <summary>
/// A request to record a video clip from one of the cameras or displays of a
/// device, optionally with the audio of the system or of a microphone.
/// </summary>
public class CameraCaptureRequest
{
    public CameraCaptureRequest()
    {
    }

    public CameraCaptureRequest(
        string cameraId,
        CameraQuality quality,
        int durationSeconds,
        CameraDeviceKind videoSource = CameraDeviceKind.Camera,
        CameraAudioSource audioSource = CameraAudioSource.None,
        string audioDeviceId = "")
    {
        CameraId = cameraId;
        Quality = quality;
        DurationSeconds = durationSeconds;
        VideoSource = videoSource;
        AudioSource = audioSource;
        AudioDeviceId = audioDeviceId;
    }

    /// <summary>
    /// The device path of the camera to record from, or the device name of
    /// the display.  When it is empty, the first source of the device is used.
    /// </summary>
    public string CameraId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the video comes from a camera or from a display.
    /// </summary>
    public CameraDeviceKind VideoSource { get; set; } = CameraDeviceKind.Camera;

    /// <summary>
    /// Where the audio of the recording comes from.
    /// </summary>
    public CameraAudioSource AudioSource { get; set; } = CameraAudioSource.None;

    /// <summary>
    /// The id of the audio device.  It is the microphone id when the source
    /// is the microphone, and the name of the loopback device when the source
    /// is the system audio.
    /// </summary>
    public string AudioDeviceId { get; set; } = string.Empty;

    /// <summary>
    /// The quality preset of the recording.
    /// </summary>
    public CameraQuality Quality { get; set; }

    /// <summary>
    /// How long the recording should run, in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }
}
