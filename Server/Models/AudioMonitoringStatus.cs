namespace Remotely.Server.Models;

using Remotely.Shared.Models;

/// <summary>
/// Describes the scheduled audio recording that is active for a device.
/// </summary>
public record AudioMonitoringStatus(
    int IntervalMinutes,
    AudioCaptureRequest CaptureRequest);
