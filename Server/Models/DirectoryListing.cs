namespace Remotely.Server.Models;

/// <summary>
/// A folder on the server, used to pick where screenshots are stored.
/// </summary>
public class DirectoryListing
{
    /// <summary>
    /// The path that was listed.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The parent of the listed path, or null at the root.
    /// </summary>
    public string? ParentPath { get; set; }

    /// <summary>
    /// The drives available on the server, shown when no path is selected.
    /// </summary>
    public string[] Drives { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The subdirectories of the listed path.
    /// </summary>
    public string[] Directories { get; set; } = Array.Empty<string>();
}
