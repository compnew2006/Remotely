namespace Remotely.Shared.Models;

/// <summary>
/// The kind of a video source that a device reported.
/// </summary>
public enum CameraDeviceKind
{
    /// <summary>
    /// A camera of the device.
    /// </summary>
    Camera = 0,

    /// <summary>
    /// A display of the device, so the desktop itself can be recorded.
    /// </summary>
    Display = 1
}
