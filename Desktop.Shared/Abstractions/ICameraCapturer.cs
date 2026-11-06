using Remotely.Shared.Models;
using Remotely.Shared.Primitives;

namespace Remotely.Desktop.Shared.Abstractions;

/// <summary>
/// Records video clips from the cameras or the displays of this device.
/// </summary>
public interface ICameraCapturer
{
    /// <summary>
    /// Returns the video sources that are available on this device.  An empty
    /// list is returned when the device has neither a camera nor a display,
    /// or when they can't be listed.
    /// </summary>
    IReadOnlyList<CameraDeviceInfo> GetCameras();

    /// <summary>
    /// Returns the audio sources that are available on this device, so a
    /// recording can carry the system audio or a microphone.
    /// </summary>
    IReadOnlyList<CameraAudioDeviceInfo> GetAudioDevices();

    /// <summary>
    /// Records a clip and returns the path of the file that was written.  The
    /// caller is responsible for deleting the file.
    /// </summary>
    /// <param name="request">The video source, the audio source, the quality, and the duration of the recording.</param>
    /// <param name="cancellationToken">Stops the recording when it is cancelled.</param>
    Task<Result<string>> RecordAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken);
}
