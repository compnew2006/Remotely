namespace Remotely.Shared.Models;

/// <summary>
/// A video source that is available on a remote device.  It is either one of
/// the cameras of the device, or one of its displays.
/// </summary>
public class CameraDeviceInfo
{
    public CameraDeviceInfo()
    {
    }

    public CameraDeviceInfo(string id, string name, CameraDeviceKind kind = CameraDeviceKind.Camera)
    {
        Id = id;
        Name = name;
        Kind = kind;
    }

    /// <summary>
    /// The device path of the camera, or the device name of the display.  It
    /// is used to select the source when the recording is started.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the source that is shown to the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a camera or a display.
    /// </summary>
    public CameraDeviceKind Kind { get; set; } = CameraDeviceKind.Camera;
}
