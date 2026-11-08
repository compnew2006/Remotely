using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Remotely.Shared.Entities;

/// <summary>
/// A screenshot captured on a remote device by the monitoring feature.
/// </summary>
public class Screenshot
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string ID { get; set; } = null!;

    public required string DeviceID { get; set; }

    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// The name of the file inside the device's screenshot directory.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// The directory where the file was stored. Null for older rows whose
    /// directory was not recorded.
    /// </summary>
    [StringLength(2048)]
    public string? StorageDirectory { get; set; }

    public long SizeBytes { get; set; }
}
