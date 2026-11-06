namespace Remotely.Server.Models.Messages;

/// <summary>
/// Sent to the circuit that requested an audio recording whenever a new clip
/// arrives from the device.
/// </summary>
public record AudioClipReceivedMessage(
    string DeviceId,
    string FileName,
    DateTimeOffset TimeStamp,
    string Quality,
    int DurationSeconds,
    string Source);
