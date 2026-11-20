using Remotely.Shared.Services;
using System;
using System.IO;

namespace Remotely.Agent.Services;

/// <summary>
/// Builds the options of the desktop client package for the platform the
/// agent runs on.
/// <para>
/// The desktop client is not part of the agent package anymore.  It is
/// downloaded from the server when a session needs it, so the agent package
/// stays small.
/// </para>
/// </summary>
public static class DesktopPackageOptionsFactory
{
    /// <summary>
    /// The directory the desktop client is installed in, next to the agent.
    /// </summary>
    public static string InstallDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Desktop");

    /// <summary>
    /// The file with the public keys that are trusted for update manifests.
    /// The server hands them to the agent over the authenticated hub
    /// connection.
    /// </summary>
    public static string TrustedKeysFilePath =>
        Path.Combine(AppContext.BaseDirectory, "TrustedUpdateKeys.json");

    /// <summary>
    /// Creates the options for the current platform, or null when the
    /// platform has no desktop client.
    /// </summary>
    public static DesktopPackageOptions? Create(string serverUrl)
    {
        var platform = GetPlatform();

        if (platform is null)
        {
            return null;
        }

        var (folder, executableName) = platform.Value;
        var baseUrl = serverUrl.TrimEnd('/');
        var packageUrl = $"{baseUrl}/Content/{folder}/{executableName}";

        return new DesktopPackageOptions
        {
            PackageUrl = packageUrl,
            ManifestUrl = packageUrl + ".manifest.json",
            SignatureUrl = packageUrl + ".manifest.sig",
            InstallDirectory = InstallDirectory,
            ExecutableName = executableName,
            TrustedKeysFilePath = TrustedKeysFilePath
        };
    }

    private static (string Folder, string ExecutableName)? GetPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return Environment.Is64BitOperatingSystem
                ? ("Win-x64", "Remotely_Desktop.exe")
                : ("Win-x86", "Remotely_Desktop.exe");
        }

        if (OperatingSystem.IsLinux())
        {
            return ("Linux-x64", "Remotely_Desktop");
        }

        // macOS has no desktop client that the agent can start.
        return null;
    }
}
