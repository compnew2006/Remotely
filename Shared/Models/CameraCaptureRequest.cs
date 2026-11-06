namespace Remotely.Shared.Models;

/// <summary>
/// A request to record a video clip from one of the cameras of a device.
/// </summary>
public class CameraCaptureRequest
{
    public CameraCaptureRequest()
    {
    }

    public CameraCaptureRequest(string cameraId, CameraQuality quality, int durationSeconds)
    {
        CameraId = cameraId;
        Quality = quality;
        DurationSeconds = durationSeconds;
    }

    /// <summary>
    /// The device path of the camera to record from.  When it is empty, the
    /// first camera of the device is used.
    /// </summary>
    public string CameraId { get; set; } = string.Empty;

    /// <summary>
    /// The quality preset of the recording.
    /// </summary>
    public CameraQuality Quality { get; set; }

    /// <summary>
    /// How long the recording should run, in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }
}
