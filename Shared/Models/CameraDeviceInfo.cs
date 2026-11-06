namespace Remotely.Shared.Models;

/// <summary>
/// A camera that is available on a remote device.
/// </summary>
public class CameraDeviceInfo
{
    public CameraDeviceInfo()
    {
    }

    public CameraDeviceInfo(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// The device path of the camera.  It is used to select the camera when
    /// the recording is started.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the camera that is shown to the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
