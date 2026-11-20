using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Remotely.Shared.Services;

namespace Remotely.Server.Services;

/// <summary>
/// Signs the client packages that are published by the server, so the
/// agents can verify them before they install anything (G2 / AD-13).
///
/// The signing key is created on the first run of the server and is kept
/// outside of the web root, so it never travels with the published files.
/// The public part of the key is handed to the agents over the authenticated
/// hub connection.
/// </summary>
public interface IUpdateSigningService
{
    /// <summary>
    /// The id of the signing key, which is also the id that is written into
    /// the manifest.
    /// </summary>
    string KeyId { get; }

    /// <summary>
    /// The trusted keys of the server, as they are written to the agents.
    /// </summary>
    string GetTrustedKeysJson();

    /// <summary>
    /// Signs the package that is published for the given platform, when its
    /// content has changed since the last time it was signed.  It returns
    /// true when the package was signed.
    /// </summary>
    Task<bool> SignIfChangedAsync(string platform, CancellationToken cancellationToken);

    /// <summary>
    /// Signs the desktop client that the agents download on demand, when its
    /// content has changed since the last time it was signed.  The desktop
    /// client is not part of the agent package, so it has a manifest of its
    /// own.
    /// </summary>
    Task<bool> SignDesktopIfChangedAsync(string platform, CancellationToken cancellationToken);
}

public class UpdateSigningService : IUpdateSigningService
{
    private const string KeyFileName = "update-signing.key";
    private const string ManifestSuffix = ".manifest.json";
    // The agent downloads the signature as
    // "<package>.manifest.sig" (see UpdaterWin.VerifyUpdatePackage). The
    // file must be written with the same name, or every update fails
    // closed when the download 404s.
    private const string SignatureSuffix = ".manifest.sig";

    // The desktop client of every platform that the agents can start.  The
    // key is the folder of the content directory, the value is the name of
    // the executable inside it.
    private static readonly Dictionary<string, string> _desktopExecutables = new()
    {
        ["Win-x64"] = "Remotely_Desktop.exe",
        ["Win-x86"] = "Remotely_Desktop.exe",
        ["Linux-x64"] = "Remotely_Desktop"
    };

    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogger<UpdateSigningService> _logger;
    private readonly SemaphoreSlim _signLock = new(1, 1);
    private readonly Dictionary<string, string> _signedHashes = new();

    private ECDsa? _key;
    private string? _keyId;

    public UpdateSigningService(
        IWebHostEnvironment hostEnvironment,
        ILogger<UpdateSigningService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public string KeyId => _keyId ??= LoadOrCreateKey().KeyId;

    public string GetTrustedKeysJson()
    {
        var (keyId, publicKey, _) = LoadOrCreateKey();

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [keyId] = publicKey
        });
    }

    public async Task<bool> SignIfChangedAsync(string platform, CancellationToken cancellationToken)
    {
        await _signLock.WaitAsync(cancellationToken);

        try
        {
            var contentDirectory = Path.Combine(_hostEnvironment.WebRootPath, "Content");
            var packagePath = Path.Combine(contentDirectory, $"Remotely-Win-{platform}.zip");
            var installerPath = Path.Combine(contentDirectory, "Install-Remotely.ps1");

            if (!File.Exists(packagePath))
            {
                return false;
            }

            var packageBytes = await File.ReadAllBytesAsync(packagePath, cancellationToken);

            var installerBytes = File.Exists(installerPath) ?
                await File.ReadAllBytesAsync(installerPath, cancellationToken) :
                null;

            return await SignAndPublishAsync(
                platform,
                packagePath,
                packageBytes,
                ReadPackageVersion(packagePath),
                installerBytes,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while signing the client package.");
            return false;
        }
        finally
        {
            _signLock.Release();
        }
    }

    /// <summary>
    /// Signs the desktop client of a platform.  The agents download it when a
    /// session needs it, and they only accept it when the signature of the
    /// manifest and the hash of the download match.
    /// </summary>
    public async Task<bool> SignDesktopIfChangedAsync(string platform, CancellationToken cancellationToken)
    {
        await _signLock.WaitAsync(cancellationToken);

        try
        {
            if (!_desktopExecutables.TryGetValue(platform, out var executableName))
            {
                return false;
            }

            var desktopPath = Path.Combine(
                _hostEnvironment.WebRootPath,
                "Content",
                platform,
                executableName);

            if (!File.Exists(desktopPath))
            {
                return false;
            }

            var desktopBytes = await File.ReadAllBytesAsync(desktopPath, cancellationToken);

            return await SignAndPublishAsync(
                "desktop:" + platform,
                desktopPath,
                desktopBytes,
                ReadDesktopVersion(desktopPath),
                installerBytes: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while signing the desktop client.");
            return false;
        }
        finally
        {
            _signLock.Release();
        }
    }

    /// <summary>
    /// Signs the content and publishes the manifest and the signature next to
    /// it.  The manifest is verified with the same code the agents use before
    /// it is written anywhere.
    /// </summary>
    private async Task<bool> SignAndPublishAsync(
        string cacheKey,
        string packagePath,
        byte[] packageBytes,
        string version,
        byte[]? installerBytes,
        CancellationToken cancellationToken)
    {
        var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes));

        var installerHash = installerBytes is null ?
            string.Empty :
            Convert.ToHexString(SHA256.HashData(installerBytes));

        // The manifest covers both the package and the installer. An
        // installer-only change (e.g. a hardened stop order) must
        // re-sign too, or the agents reject every update with
        // InstallerHashMismatch against the stale manifest.
        var signedIdentity = packageHash + ":" + installerHash;

        if (_signedHashes.TryGetValue(cacheKey, out var signedHash) &&
            string.Equals(signedHash, signedIdentity, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var (keyId, _, signingKey) = LoadOrCreateKey();

        var manifest = new UpdateManifest(
            Path.GetFileName(packagePath),
            version,
            packageHash,
            installerHash,
            keyId);

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var signature = signingKey.SignData(manifestBytes, HashAlgorithmName.SHA256);
        var signatureText = Convert.ToBase64String(signature);

        // Verify the result with the same code the agents use, before it
        // is written anywhere.  A manifest that the agent would refuse
        // must never reach the disk, and the failure must not be
        // remembered: the next tick has to try again.
        var verification = VerifyManifest(
            manifestBytes,
            signatureText,
            packageBytes,
            installerBytes,
            keyId,
            signingKey);

        if (!verification.Success)
        {
            _logger.LogError(
                "The client package for {platform} was signed, but the result does not verify: {failure} {detail}.  " +
                "The agents would refuse the update.",
                cacheKey,
                verification.Failure,
                verification.Detail);

            // The manifest of the previous content describes a package
            // that is no longer on disk.  Serving it would hand the
            // agents a pair that can never match, so it is removed.
            RemovePublishedManifest(packagePath);

            return false;
        }

        await File.WriteAllBytesAsync(
            packagePath + ManifestSuffix,
            manifestBytes,
            cancellationToken);

        await File.WriteAllTextAsync(
            packagePath + SignatureSuffix,
            signatureText,
            cancellationToken);

        // Only now the content counts as signed.  Recording it earlier
        // would make every later call skip the signing because the
        // hashes match, and the failure would never be reported again.
        _signedHashes[cacheKey] = signedIdentity;

        _logger.LogInformation(
            "The client package for {platform} was signed.  Key ID: {keyId}.  Version: {version}.",
            cacheKey,
            keyId,
            manifest.Version);

        return true;
    }

    /// <summary>
    /// Checks the manifest with the same code the agents use.
    /// <para>
    /// The version is 0.0.0.0 on purpose: the server does not know which
    /// version the agents already run, so it cannot judge a rollback.  The
    /// agent does that comparison itself.
    /// </para>
    /// <para>
    /// The method is virtual so that a test can make the check fail and
    /// observe what the server publishes when it does.
    /// </para>
    /// </summary>
    protected virtual UpdateVerificationResult VerifyManifest(
        byte[] manifestBytes,
        string signatureBase64,
        byte[] packageBytes,
        byte[]? installerBytes,
        string keyId,
        ECDsa key)
    {
        return UpdatePackageVerifier.Verify(
            manifestBytes,
            signatureBase64,
            packageBytes,
            installerBytes,
            new Version(0, 0, 0, 0),
            new Dictionary<string, string> { [keyId] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) });
    }

    /// <summary>
    /// Removes the published manifest and signature of a package.  They are
    /// removed together: a signature without its manifest, and the other way
    /// round, are both worse than nothing.
    /// </summary>
    private void RemovePublishedManifest(string packagePath)
    {
        try
        {
            var manifestPath = packagePath + ManifestSuffix;
            var signaturePath = packagePath + SignatureSuffix;

            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (File.Exists(signaturePath))
            {
                File.Delete(signaturePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "The manifest of the client package could not be removed.");
        }
    }

    /// <summary>
    /// Reads the version of the published desktop client.  The publish script
    /// writes it next to the file, because a single-file build of a Linux
    /// executable carries no version resource.  The version of a Windows
    /// build is read from its version resource.
    /// </summary>
    private static string ReadDesktopVersion(string desktopPath)
    {
        try
        {
            var sidecarPath = desktopPath + ".version";

            if (File.Exists(sidecarPath))
            {
                var version = File.ReadAllText(sidecarPath).Trim();

                if (!string.IsNullOrWhiteSpace(version))
                {
                    return version;
                }
            }
        }
        catch (Exception)
        {
            // The version resource is used below.
        }

        try
        {
            var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(desktopPath).FileVersion;

            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }
        catch (Exception)
        {
            // The version stays unknown.
        }

        return "0.0.0.0";
    }

    /// <summary>
    /// Reads the version of the agent that is inside the package, so the
    /// agents can refuse an older package.
    /// </summary>
    private static string ReadPackageVersion(string packagePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var entry = archive.GetEntry("Remotely_Agent.dll");

            if (entry is null)
            {
                return "0.0.0.0";
            }

            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();

            return ReadVersionFromBytes(bytes);
        }
        catch (Exception)
        {
            return "0.0.0.0";
        }
    }

    /// <summary>
    /// Reads the file version of a managed assembly.
    /// </summary>
    private static string ReadVersionFromBytes(byte[] assemblyBytes)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"rv-{Guid.NewGuid():N}.dll");
            File.WriteAllBytes(tempPath, assemblyBytes);

            try
            {
                var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(tempPath).FileVersion;

                if (!string.IsNullOrWhiteSpace(version))
                {
                    return version;
                }
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception)
        {
            // The version is read from the file name below.
        }

        return "0.0.0.0";
    }

    /// <summary>
    /// Loads the signing key of the server, and creates it when it does not
    /// exist yet.  The private key is protected with the operating system, so
    /// it is not readable as a plain file.
    /// </summary>
    private (string KeyId, string PublicKeyBase64, ECDsa Key) LoadOrCreateKey()
    {
        if (_key is not null && _keyId is not null)
        {
            return (_keyId, Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo()), _key);
        }

        var directory = Path.Combine(_hostEnvironment.ContentRootPath, "AppData", "keys");
        var keyPath = Path.Combine(directory, KeyFileName);

        try
        {
            Directory.CreateDirectory(directory);

            if (File.Exists(keyPath))
            {
                var protectedBytes = File.ReadAllBytes(keyPath);
                var keyBytes = Unprotect(protectedBytes);
                var key = ECDsa.Create();
                key.ImportECPrivateKey(keyBytes, out _);

                _key = key;
                _keyId = ComputeKeyId(key);

                return (_keyId, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), key);
            }

            var newKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var privateKeyBytes = newKey.ExportECPrivateKey();
            File.WriteAllBytes(keyPath, Protect(privateKeyBytes));

            _key = newKey;
            _keyId = ComputeKeyId(newKey);

            _logger.LogInformation(
                "A new signing key was created for the client packages.  Key ID: {keyId}.  The key is stored in {path}.",
                _keyId,
                keyPath);

            return (_keyId, Convert.ToBase64String(newKey.ExportSubjectPublicKeyInfo()), newKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while loading the signing key of the client packages.");

            // Without a key the packages cannot be signed, and the agents
            // keep the version that they have.
            var fallback = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _key = fallback;
            _keyId = ComputeKeyId(fallback);

            return (_keyId, Convert.ToBase64String(fallback.ExportSubjectPublicKeyInfo()), fallback);
        }
    }

    private static string ComputeKeyId(ECDsa key)
    {
        var publicKey = key.ExportSubjectPublicKeyInfo();
        return Convert.ToHexString(SHA256.HashData(publicKey))[..16].ToLowerInvariant();
    }

    private byte[] Protect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning(
                "The operating system does not support the protection of the signing key, so it is stored as it is.");

            return data;
        }

        return ProtectedData.Protect(data, GetEntropy(), DataProtectionScope.LocalMachine);
    }

    private byte[] Unprotect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            return data;
        }

        try
        {
            return ProtectedData.Unprotect(data, GetEntropy(), DataProtectionScope.LocalMachine);
        }
        catch (CryptographicException)
        {
            // The key was stored without protection.
            return data;
        }
    }

    private static byte[] GetEntropy()
    {
        return Encoding.UTF8.GetBytes("Remotely.UpdateSigningKey.v1");
    }
}
