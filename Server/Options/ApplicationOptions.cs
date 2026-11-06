namespace Remotely.Server.Options;

public class ApplicationOptions
{
    public const string SectionKey = "ApplicationOptions";
    public string DbProvider { get; set; } = "SQLite";

    /// <summary>
    /// When false, the server refuses to start if pending database migrations
    /// exist, instead of applying them automatically at startup (GAP-25).
    /// Operators managing migrations through their own change process set this
    /// to false and run `dotnet ef database update` explicitly.
    /// </summary>
    public bool AllowDbAutoMigration { get; set; } = true;

    public string? DockerGatewayIp { get; set; }
}
