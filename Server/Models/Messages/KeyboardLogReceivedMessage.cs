namespace Remotely.Server.Models.Messages;

/// <summary>
/// Sent to the circuit that is watching the keyboard of a device whenever a
/// burst of text is captured on it.
/// </summary>
public record KeyboardLogReceivedMessage(
    string DeviceId,
    string WindowTitle,
    string ProcessName,
    string Text,
    bool IsPasswordField,
    DateTimeOffset TimeStamp);
