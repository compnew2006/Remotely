namespace Remotely.Server.Models.Messages;

/// <summary>
/// Sent to the circuit that started monitoring a device whenever a new
/// screenshot arrives from it.
/// </summary>
public record ScreenshotReceivedMessage(string DeviceId, string FileName, DateTimeOffset TimeStamp);
