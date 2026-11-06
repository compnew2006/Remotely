using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Remotely.Shared.Entities;

/// <summary>
/// An audio clip that was recorded from a remote device.
/// </summary>
public class AudioClip
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string ID { get; set; } = null!;

    public required string DeviceID { get; set; }

    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// The name of the file inside the device's audio directory.
    /// </summary>
    public required string FileName { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>
    /// The name of the quality preset that was used.
    /// </summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>
    /// How long the clip runs, in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// The source that was captured (system audio or microphone).
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
