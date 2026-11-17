#nullable enable
namespace Remotely.Shared.Models;

public class ConnectionInfo
{
    private string? _host;

    public string DeviceID { get; set; } = Guid.NewGuid().ToString();
    public string? Host
    {
        get
        {
            return _host?.Trim()?.TrimEnd('/');
        }
        set
        {
            _host = value?.Trim()?.TrimEnd('/');
        }
    }
    public string? OrganizationID { get; set; }

    /// <summary>
    /// The credential that proves that this device is this device.  It never
    /// leaves the device, and the server only keeps its fingerprint.
    /// </summary>
    public string? ServerVerificationToken { get; set; }

    /// <summary>
    /// When the credential was issued.  The agent rotates a credential that
    /// has become old, so a credential that leaked is not valid forever.
    /// </summary>
    public DateTimeOffset? ServerVerificationTokenIssuedAt { get; set; }
}
