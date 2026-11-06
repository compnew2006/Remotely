namespace Remotely.Server.Models.Messages;

/// <summary>
/// Sent to the circuit that requested a camera recording whenever a new
/// clip arrives from the device.
/// </summary>
public record CameraClipReceivedMessage(
    string DeviceId,
    string FileName,
    DateTimeOffset TimeStamp,
    string Quality,
    int DurationSeconds);
