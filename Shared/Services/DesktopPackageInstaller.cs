using System.Security.Cryptography;
using System.Text.Json;

namespace Remotely.Shared.Services;

/// <summary>
/// Fetches the content of a package that the server publishes.
/// </summary>
public interface IPackageContentFetcher
{
    /// <summary>
    /// Downloads a file to the given path.  The file is written to a
    /// temporary name and only moved to its final name by the caller, so a
    /// failed download never leaves a half-written file behind.
    /// </summary>
    Task FetchToFileAsync(string url, string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a small file and returns its content.
    /// </summary>
    Task<byte[]> FetchBytesAsync(string url, CancellationToken cancellationToken);
}

/// <summary>
/// The reason a desktop client could not be made available.
/// </summary>
public enum DesktopPackageFailure
{
    None,
    DownloadFailed,
    ManifestUnavailable,
    ManifestInvalid,
    PackageHashMismatch,
    RollbackRefused,
    InstallFailed
}

public sealed record DesktopPackageResult(
    bool Success,
    string? ExecutablePath,
    DesktopPackageFailure Failure,
    string Detail)
{
    public static DesktopPackageResult Ok(string executablePath) =>
        new(true, executablePath, DesktopPackageFailure.None, string.Empty);

    public static DesktopPackageResult Fail(DesktopPackageFailure failure, string detail) =>
        new(false, null, failure, detail);
}

/// <summary>
/// Where the desktop client comes from, and where it is installed.
/// </summary>
public sealed class DesktopPackageOptions
{
    /// <summary>
    /// The address of the published desktop client.
    /// </summary>
    public required string PackageUrl { get; init; }

    /// <summary>
    /// The address of the signed manifest that describes the package.
    /// </summary>
    public required string ManifestUrl { get; init; }

    /// <summary>
    /// The address of the signature of the manifest.
    /// </summary>
    public required string SignatureUrl { get; init; }

    /// <summary>
    /// The directory the desktop client is installed in.
    /// </summary>
    public required string InstallDirectory { get; init; }

    /// <summary>
    /// The name of the desktop client executable.
    /// </summary>
    public required string ExecutableName { get; init; }

    /// <summary>
    /// The file with the public keys that are trusted for update manifests.
    /// </summary>
    public required string TrustedKeysFilePath { get; init; }

    /// <summary>
    /// How long a verified manifest is reused before the server is asked again.
    /// </summary>
    public TimeSpan ManifestCacheDuration { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// The state of the installed desktop client, so the agent knows what it has
/// and does not download the same client over and over.
/// </summary>
public sealed record DesktopPackageState
{
    public string Version { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public DateTimeOffset InstalledAt { get; init; }
}

public interface IDesktopPackageInstaller
{
    /// <summary>
    /// Makes sure that the desktop client is installed and returns the path of
    /// the executable.  The client is downloaded from the server when it is
    /// missing or outdated, and the download is only accepted when it matches
    /// the signed manifest of the server.
    /// </summary>
    Task<DesktopPackageResult> EnsureInstalledAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads the desktop client from the server when it is needed, and keeps
/// it in a directory of its own version so that a running client is never
/// overwritten.
/// <para>
/// The client is verified before it is used: the manifest must be signed with
/// a key that is trusted, and the download must match the hash of the
/// manifest.  A client that cannot be verified is never installed.
/// </para>
/// </summary>
public sealed class DesktopPackageInstaller : IDesktopPackageInstaller
{
    private const string StateFileName = "desktop-package.json";

    private readonly IPackageContentFetcher _fetcher;
    private readonly DesktopPackageOptions _options;
    private readonly SemaphoreSlim _installLock = new(1, 1);
    private UpdateManifest? _cachedManifest;
    private DateTimeOffset _manifestFetchedAt = DateTimeOffset.MinValue;

    public DesktopPackageInstaller(IPackageContentFetcher fetcher, DesktopPackageOptions options)
    {
        _fetcher = fetcher;
        _options = options;
    }

    public async Task<DesktopPackageResult> EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        await _installLock.WaitAsync(cancellationToken);

        try
        {
            var state = ReadState();
            var installedPath = ResolveInstalledPath(state);

            var manifest = await GetManifestAsync(cancellationToken);

            if (manifest is null)
            {
                // The server could not be reached, or the manifest could not be
                // trusted.  A client that is already installed keeps working;
                // without one there is nothing to fall back to.
                if (installedPath is not null)
                {
                    return DesktopPackageResult.Ok(installedPath);
                }

                return DesktopPackageResult.Fail(
                    DesktopPackageFailure.ManifestUnavailable,
                    "The signed manifest of the desktop client could not be obtained from the server.");
            }

            if (!string.Equals(manifest.Package, _options.ExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                return DesktopPackageResult.Fail(
                    DesktopPackageFailure.ManifestInvalid,
                    $"The manifest describes '{manifest.Package}', but '{_options.ExecutableName}' was expected.");
            }

            if (installedPath is not null)
            {
                if (string.Equals(state?.Version, manifest.Version, StringComparison.OrdinalIgnoreCase))
                {
                    return DesktopPackageResult.Ok(installedPath);
                }

                if (IsNewer(state?.Version, manifest.Version))
                {
                    // The installed client is newer than the one the server
                    // publishes.  It is not replaced with an older one.
                    return DesktopPackageResult.Ok(installedPath);
                }
            }

            return await InstallAsync(manifest, cancellationToken);
        }
        finally
        {
            _installLock.Release();
        }
    }

    private async Task<DesktopPackageResult> InstallAsync(UpdateManifest manifest, CancellationToken cancellationToken)
    {
        var versionDirectory = Path.Combine(_options.InstallDirectory, SanitizeName(manifest.Version));
        var targetPath = Path.Combine(versionDirectory, _options.ExecutableName);
        var tempPath = targetPath + ".download";

        try
        {
            Directory.CreateDirectory(versionDirectory);

            try
            {
                await _fetcher.FetchToFileAsync(_options.PackageUrl, tempPath, cancellationToken);
            }
            catch (Exception ex)
            {
                return DesktopPackageResult.Fail(
                    DesktopPackageFailure.DownloadFailed,
                    $"The desktop client could not be downloaded: {ex.Message}");
            }

            string hash;

            try
            {
                hash = await ComputeHashAsync(tempPath, cancellationToken);
            }
            catch (Exception ex)
            {
                return DesktopPackageResult.Fail(
                    DesktopPackageFailure.InstallFailed,
                    $"The downloaded desktop client could not be read: {ex.Message}");
            }

            if (!string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return DesktopPackageResult.Fail(
                    DesktopPackageFailure.PackageHashMismatch,
                    "The downloaded desktop client does not match the hash of the signed manifest.");
            }

            try
            {
                File.Move(tempPath, targetPath, overwrite: true);
            }
            catch (Exception ex)
            {
                return DesktopPackageResult.Fail(
                    DesktopPackageFailure.InstallFailed,
                    $"The desktop client could not be installed: {ex.Message}");
            }
        }
        finally
        {
            TryDelete(tempPath);
        }

        WriteState(new DesktopPackageState
        {
            Version = manifest.Version,
            Sha256 = manifest.Sha256,
            ExecutablePath = targetPath,
            InstalledAt = DateTimeOffset.Now
        });

        RemoveOutdatedVersions(targetPath);

        return DesktopPackageResult.Ok(targetPath);
    }

    private async Task<UpdateManifest?> GetManifestAsync(CancellationToken cancellationToken)
    {
        if (_cachedManifest is not null &&
            DateTimeOffset.Now - _manifestFetchedAt < _options.ManifestCacheDuration)
        {
            return _cachedManifest;
        }

        byte[] manifestBytes;
        byte[] signatureBytes;

        try
        {
            manifestBytes = await _fetcher.FetchBytesAsync(_options.ManifestUrl, cancellationToken);
            signatureBytes = await _fetcher.FetchBytesAsync(_options.SignatureUrl, cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }

        var trustedKeys = UpdatePackageVerifier.LoadTrustedKeysFile(_options.TrustedKeysFilePath);

        var result = UpdatePackageVerifier.VerifyManifestSignature(
            manifestBytes,
            System.Text.Encoding.UTF8.GetString(signatureBytes).Trim(),
            trustedKeys);

        if (!result.Success || result.Manifest is null)
        {
            return null;
        }

        _cachedManifest = result.Manifest;
        _manifestFetchedAt = DateTimeOffset.Now;

        return _cachedManifest;
    }

    private string? ResolveInstalledPath(DesktopPackageState? state)
    {
        if (state is null || string.IsNullOrWhiteSpace(state.ExecutablePath))
        {
            return null;
        }

        return File.Exists(state.ExecutablePath) ? state.ExecutablePath : null;
    }

    private DesktopPackageState? ReadState()
    {
        try
        {
            var path = Path.Combine(_options.InstallDirectory, StateFileName);

            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<DesktopPackageState>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void WriteState(DesktopPackageState state)
    {
        try
        {
            Directory.CreateDirectory(_options.InstallDirectory);
            File.WriteAllText(
                Path.Combine(_options.InstallDirectory, StateFileName),
                JsonSerializer.Serialize(state));
        }
        catch (Exception)
        {
            // The state file is a cache.  Without it the client is downloaded
            // again, which is slow but not wrong.
        }
    }

    /// <summary>
    /// Removes the directories of the versions that are no longer used.  A
    /// client that is still running keeps its files, so the removal is done on
    /// a best-effort basis.
    /// </summary>
    private void RemoveOutdatedVersions(string currentPath)
    {
        try
        {
            var currentDirectory = Path.GetDirectoryName(currentPath);

            foreach (var directory in Directory.EnumerateDirectories(_options.InstallDirectory))
            {
                if (string.Equals(directory, currentDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryDelete(directory, recursive: true);
            }
        }
        catch (Exception)
        {
            // Leftover directories are only a waste of disk space.
        }
    }

    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);

        return Convert.ToHexString(hash);
    }

    private static bool IsNewer(string? installedVersion, string manifestVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return false;
        }

        if (!Version.TryParse(installedVersion, out var installed) ||
            !Version.TryParse(manifestVersion, out var published))
        {
            return false;
        }

        return installed > published;
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(x => invalid.Contains(x) ? '_' : x).ToArray();

        return new string(chars);
    }

    private static void TryDelete(string path, bool recursive = false)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (recursive && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception)
        {
            // A file that is still in use is kept.  It is cleaned up by the
            // next installation.
        }
    }
}
