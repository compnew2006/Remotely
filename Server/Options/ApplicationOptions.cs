namespace Remotely.Server.Options;

public class ApplicationOptions
{
    public const string SectionKey = "ApplicationOptions";
    public const string DefaultDbProvider = "SQLite";

    public string DbProvider { get; set; } = DefaultDbProvider;

    /// <summary>
    /// Normalizes a configured provider name to the lower-case form the
    /// provider switches compare against.  A missing or blank value falls back
    /// to <see cref="DefaultDbProvider"/>, so reading the raw configuration and
    /// binding the options produce the same provider.
    /// </summary>
    public static string NormalizeDbProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? DefaultDbProvider.ToLowerInvariant()
            : provider.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// When false, the server refuses to start if pending database migrations
    /// exist, instead of applying them automatically at startup (GAP-25).
    /// Operators managing migrations through their own change process set this
    /// to false and run `dotnet ef database update` explicitly.
    /// </summary>
    public bool AllowDbAutoMigration { get; set; } = true;

    public string? DockerGatewayIp { get; set; }

    /// <summary>
    /// Emit a Content-Security-Policy header.  On by default.  The policy
    /// allows scripts only from this origin and forbids framing.  A deployment
    /// that introduces a third-party or inline script must extend the policy,
    /// not disable it.
    /// </summary>
    public bool EnableContentSecurityPolicy { get; set; } = true;
}
