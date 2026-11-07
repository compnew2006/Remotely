using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Remotely.Shared.Entities;

/// <summary>
/// A burst of text that was typed on a remote device.
/// </summary>
public class KeyboardLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string ID { get; set; } = null!;

    public required string DeviceID { get; set; }

    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// The title of the window that had the focus.
    /// </summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// The name of the process that had the focus.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// The text that was typed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether the text was typed into a field that holds a secret.  The
    /// text of such a field is never stored.
    /// </summary>
    public bool IsPasswordField { get; set; }
}
