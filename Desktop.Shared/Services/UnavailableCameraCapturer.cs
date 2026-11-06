using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Reports camera capture as unavailable on desktop builds that do not
/// provide a platform-specific camera capturer.
/// </summary>
internal sealed class UnavailableCameraCapturer : ICameraCapturer
{
    public IReadOnlyList<CameraDeviceInfo> GetCameras()
    {
        return Array.Empty<CameraDeviceInfo>();
    }

    public Task<Result<string>> RecordAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Fail<string>(
            "Camera capture is not available in this desktop build."));
    }
}
