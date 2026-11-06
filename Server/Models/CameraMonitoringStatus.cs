using Remotely.Shared.Models;

namespace Remotely.Server.Models;

/// <summary>
/// The active scheduled camera recording for a device, if one exists.
/// </summary>
public sealed record CameraMonitoringStatus(
    int IntervalMinutes,
    CameraCaptureRequest CaptureRequest);
