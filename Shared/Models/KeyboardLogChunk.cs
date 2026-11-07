namespace Remotely.Shared.Models;

/// <summary>
/// A burst of text that was typed on a device.  It is sent by the desktop
/// client to the server while it is being typed.
/// </summary>
public class KeyboardLogChunk
{
    public KeyboardLogChunk()
    {
    }

    public KeyboardLogChunk(
        string windowTitle,
        string processName,
        string text,
        bool isPasswordField)
    {
        WindowTitle = windowTitle;
        ProcessName = processName;
        Text = text;
        IsPasswordField = isPasswordField;
    }

    /// <summary>
    /// The title of the window that had the focus.
    /// </summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// The name of the process that had the focus.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// The text that was typed, or a mask when it was typed into a field
    /// that holds a secret.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether the text was typed into a field that holds a secret.
    /// </summary>
    public bool IsPasswordField { get; set; }

    /// <summary>
    /// When the burst was typed.
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.Now;
}
