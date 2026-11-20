using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Services;

namespace Remotely.Shared.Tests;

/// <summary>
/// The desktop client is not part of the agent package anymore.  The agent
/// downloads it from the server when a session needs it, and it only accepts
/// a download that matches the signed manifest of the server (fail-closed).
/// </summary>
[TestClass]
public class DesktopPackageInstallerTests
{
    private const string PackageUrl = "https://server/Content/Win-x64/Remotely_Desktop.exe";
    private const string ExecutableName = "Remotely_Desktop.exe";
    private const string KeyId = "test-2026-11";

    private string _root = string.Empty;
    private string _installDirectory = string.Empty;
    private string _trustedKeysPath = string.Empty;
    private ECDsa _key = null!;
    private FakeFetcher _fetcher = null!;

    [TestInitialize]
    public void Init()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "RemotelyDesktopPackageTests",
            Guid.NewGuid().ToString("N"));

        _installDirectory = Path.Combine(_root, "Desktop");
        _trustedKeysPath = Path.Combine(_root, "TrustedUpdateKeys.json");

        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_installDirectory);

        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        File.WriteAllText(
            _trustedKeysPath,
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [KeyId] = Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo())
            }));

        _fetcher = new FakeFetcher();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch
        {
            // The temporary folder is left behind when it can't be removed.
        }
    }

    [TestMethod]
    public async Task EnsureInstalledAsync_DownloadsAndInstallsTheClient()
    {
        var content = Encoding.UTF8.GetBytes("desktop-client-content");
        Publish("2.0.0.0", content);

        var installer = CreateInstaller();

        var result = await installer.EnsureInstalledAsync();

        Assert.IsTrue(result.Success, result.Detail);
        Assert.IsNotNull(result.ExecutablePath);
        Assert.IsTrue(File.Exists(result.ExecutablePath));
        Assert.AreEqual("desktop-client-content", File.ReadAllText(result.ExecutablePath));
    }

    [TestMethod]
    public async Task EnsureInstalledAsync_RefusesAPackageThatDoesNotMatchTheManifest()
    {
        Publish("2.0.0.0", Encoding.UTF8.GetBytes("signed-content"));

        // The server sends different content than the manifest describes.
        _fetcher.AddFile(PackageUrl, Encoding.UTF8.GetBytes("tampered-content"));

        var installer = CreateInstaller();

        var result = await installer.EnsureInstalledAsync();

        Assert.IsFalse(result.Success);
        Assert.AreEqual(DesktopPackageFailure.PackageHashMismatch, result.Failure);

        // Nothing is installed when the download does not match.
        var installedFiles = Directory.Exists(_installDirectory)
            ? Directory.GetFiles(_installDirectory, ExecutableName, SearchOption.AllDirectories)
            : Array.Empty<string>();

        Assert.AreEqual(0, installedFiles.Length);
    }

    [TestMethod]
    public async Task EnsureInstalledAsync_RefusesAManifestThatIsNotTrusted()
    {
        var content = Encoding.UTF8.GetBytes("desktop-client-content");
        Publish("2.0.0.0", content);

        // The keys of the file are empty, so the key of the manifest is not
        // trusted.
        File.WriteAllText(_trustedKeysPath, "{}");

        var installer = CreateInstaller();

        var result = await installer.EnsureInstalledAsync();

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.ExecutablePath);
    }

    [TestMethod]
    public async Task EnsureInstalledAsync_DoesNotDownloadTheClientTwice()
    {
        var content = Encoding.UTF8.GetBytes("desktop-client-content");
        Publish("2.0.0.0", content);

        var installer = CreateInstaller();

        var first = await installer.EnsureInstalledAsync();
        var downloadsAfterFirst = _fetcher.PackageDownloads;

        var second = await installer.EnsureInstalledAsync();

        Assert.IsTrue(first.Success, first.Detail);
        Assert.IsTrue(second.Success, second.Detail);
        Assert.AreEqual(first.ExecutablePath, second.ExecutablePath);
        Assert.AreEqual(downloadsAfterFirst, _fetcher.PackageDownloads);
    }

    [TestMethod]
    public async Task EnsureInstalledAsync_KeepsANewerClientAndDoesNotDowngrade()
    {
        var content = Encoding.UTF8.GetBytes("desktop-client-content");
        Publish("2.0.0.0", content);

        var installer = CreateInstaller();
        var first = await installer.EnsureInstalledAsync();

        // The server publishes an older version than the one that is installed.
        Publish("1.0.0.0", Encoding.UTF8.GetBytes("older-content"));
        _fetcher.AddFile(PackageUrl, Encoding.UTF8.GetBytes("older-content"));

        var second = await installer.EnsureInstalledAsync();

        Assert.IsTrue(second.Success, second.Detail);
        Assert.AreEqual(first.ExecutablePath, second.ExecutablePath);
        Assert.AreEqual("desktop-client-content", File.ReadAllText(second.ExecutablePath!));
    }

    [TestMethod]
    public async Task EnsureInstalledAsync_UsesTheInstalledClientWhenTheServerCannotBeReached()
    {
        var content = Encoding.UTF8.GetBytes("desktop-client-content");
        Publish("2.0.0.0", content);

        var installer = CreateInstaller();
        var first = await installer.EnsureInstalledAsync();

        // A new installer has no cached manifest, and the server is gone.
        _fetcher.Clear();
        var second = await CreateInstaller().EnsureInstalledAsync();

        Assert.IsTrue(first.Success, first.Detail);
        Assert.IsTrue(second.Success, second.Detail);
        Assert.AreEqual(first.ExecutablePath, second.ExecutablePath);
    }

    [TestMethod]
    public async Task EnsureInstalledAsync_FailsWhenThereIsNoClientAndTheServerCannotBeReached()
    {
        var installer = CreateInstaller();

        var result = await installer.EnsureInstalledAsync();

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.ExecutablePath);
    }

    private DesktopPackageInstaller CreateInstaller()
    {
        return new DesktopPackageInstaller(_fetcher, new DesktopPackageOptions
        {
            PackageUrl = PackageUrl,
            ManifestUrl = PackageUrl + ".manifest.json",
            SignatureUrl = PackageUrl + ".manifest.sig",
            InstallDirectory = _installDirectory,
            ExecutableName = ExecutableName,
            TrustedKeysFilePath = _trustedKeysPath
        });
    }

    /// <summary>
    /// Publishes the client and the signed manifest of it, the way the server
    /// does.
    /// </summary>
    private void Publish(string version, byte[] content)
    {
        var manifest = new UpdateManifest(
            ExecutableName,
            version,
            Convert.ToHexString(SHA256.HashData(content)),
            string.Empty,
            KeyId);

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var signature = Convert.ToBase64String(_key.SignData(manifestBytes, HashAlgorithmName.SHA256));

        _fetcher.AddFile(PackageUrl, content);
        _fetcher.AddFile(PackageUrl + ".manifest.json", manifestBytes);
        _fetcher.AddFile(PackageUrl + ".manifest.sig", Encoding.UTF8.GetBytes(signature));
    }

    private sealed class FakeFetcher : IPackageContentFetcher
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public int PackageDownloads { get; private set; }

        public void AddFile(string url, byte[] content) => _files[url] = content;

        public void Clear() => _files.Clear();

        public Task FetchToFileAsync(string url, string filePath, CancellationToken cancellationToken)
        {
            if (!_files.TryGetValue(url, out var content))
            {
                throw new FileNotFoundException($"The file '{url}' was not published.");
            }

            PackageDownloads++;
            File.WriteAllBytes(filePath, content);

            return Task.CompletedTask;
        }

        public Task<byte[]> FetchBytesAsync(string url, CancellationToken cancellationToken)
        {
            if (!_files.TryGetValue(url, out var content))
            {
                throw new FileNotFoundException($"The file '{url}' was not published.");
            }

            return Task.FromResult(content);
        }
    }
}
