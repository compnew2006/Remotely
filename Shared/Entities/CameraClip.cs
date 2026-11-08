using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Remotely.Shared.Entities;

/// <summary>
/// A video clip that was recorded from a camera of a remote device.
/// </summary>
public class CameraClip
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string ID { get; set; } = null!;

    public required string DeviceID { get; set; }

    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// The name of the file inside the device's camera directory.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// The directory where the file was stored. Null for older rows whose
    /// directory was not recorded.
    /// </summary>
    [StringLength(2048)]
    public string? StorageDirectory { get; set; }

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
    /// The name of the camera that the clip was recorded from.
    /// </summary>
    public string CameraName { get; set; } = string.Empty;
}
