using Remotely.Shared.Models;
using Remotely.Shared.Primitives;

namespace Remotely.Desktop.Shared.Abstractions;

/// <summary>
/// Records video clips from the cameras of this device.
/// </summary>
public interface ICameraCapturer
{
    /// <summary>
    /// Returns the cameras that are available on this device.  An empty list
    /// is returned when the device has no camera, or when they can't be
    /// listed.
    /// </summary>
    IReadOnlyList<CameraDeviceInfo> GetCameras();

    /// <summary>
    /// Records a clip from a camera and returns the path of the file that was
    /// written.  The caller is responsible for deleting the file.
    /// </summary>
    /// <param name="request">The camera, the quality, and the duration of the recording.</param>
    /// <param name="cancellationToken">Stops the recording when it is cancelled.</param>
    Task<Result<string>> RecordAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken);
}
