namespace Remotely.Shared;

public class AppConstants
{
    public const string DefaultProductName = "Remotely";
    public const string DefaultPublisherName = "Immense Networks";
    public const string DebugOrgId = "e8f4ad87-4a4b-4da1-bcb2-1788eaeb80e8";
    public const int EmbeddedDataBlockLength = 256;
    public const long MaxUploadFileSize = 100_000_000;
    public const double ScriptRunExpirationMinutes = 30;
    public const string ApiKeyHeaderName = "X-Api-Key";
    public const string ExpiringTokenHeaderName = "X-Expiring-Token";

    /// <summary>
    /// The header an agent presents when it opens its hub connection, carrying
    /// the verification token that is bound to its device record.  The server
    /// refuses to register a device when the presented token does not match,
    /// so knowing a device ID and an organization ID is not enough to
    /// impersonate the device.
    /// </summary>
    public const string DeviceVerificationTokenHeaderName = "X-Remotely-Device-Token";

    /// <summary>
    /// The header that a device uses to authenticate a camera clip upload
    /// with the access key of its session.
    /// </summary>
    public const string SessionKeyHeaderName = "X-Remotely-Session-Key";
}
