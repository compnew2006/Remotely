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
}

public class UpdateSigningService : IUpdateSigningService
{
    private const string KeyFileName = "update-signing.key";
    private const string ManifestSuffix = ".manifest.json";
    private const string SignatureSuffix = ".sig";

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
            var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes));

            if (_signedHashes.TryGetValue(platform, out var signedHash) &&
                string.Equals(signedHash, packageHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var installerBytes = File.Exists(installerPath) ?
                await File.ReadAllBytesAsync(installerPath, cancellationToken) :
                null;

            var installerHash = installerBytes is null ?
                string.Empty :
                Convert.ToHexString(SHA256.HashData(installerBytes));

            var (keyId, _, key) = LoadOrCreateKey();

            var manifest = new UpdateManifest(
                $"Remotely-Win-{platform}.zip",
                ReadPackageVersion(packagePath),
                packageHash,
                installerHash,
                keyId);

            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var signature = key.SignData(manifestBytes, HashAlgorithmName.SHA256);

            await File.WriteAllBytesAsync(
                packagePath + ManifestSuffix,
                manifestBytes,
                cancellationToken);

            await File.WriteAllTextAsync(
                packagePath + SignatureSuffix,
                Convert.ToBase64String(signature),
                cancellationToken);

            _signedHashes[platform] = packageHash;

            _logger.LogInformation(
                "The client package for {platform} was signed.  Key ID: {keyId}.  Version: {version}.",
                platform,
                keyId,
                manifest.Version);

            return true;
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
