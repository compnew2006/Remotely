using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Services;
using Remotely.Shared.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

/// <summary>
/// The client packages of the server are signed, and the signature is
/// verified with the key that the agent receives from the server.  A package
/// that was changed after it was signed is refused.
/// </summary>
[TestClass]
public class UpdateSigningServiceTests
{
    private string _contentRoot = string.Empty;
    private string _webRoot = string.Empty;
    private IWebHostEnvironment _hostEnvironment = null!;
    private UpdateSigningService _signingService = null!;

    [TestInitialize]
    public void Init()
    {
        _contentRoot = Path.Combine(
            Path.GetTempPath(),
            "RemotelySigningTests",
            Guid.NewGuid().ToString("N"));

        _webRoot = Path.Combine(_contentRoot, "wwwroot");
        Directory.CreateDirectory(Path.Combine(_webRoot, "Content"));

        File.WriteAllText(
            Path.Combine(_webRoot, "Content", "Install-Remotely.ps1"),
            "Write-Host 'install'");

        var hostEnvironment = new Mock<IWebHostEnvironment>();
        hostEnvironment.Setup(x => x.ContentRootPath).Returns(_contentRoot);
        hostEnvironment.Setup(x => x.WebRootPath).Returns(_webRoot);

        _hostEnvironment = hostEnvironment.Object;

        _signingService = new UpdateSigningService(
            hostEnvironment.Object,
            NullLogger<UpdateSigningService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_contentRoot))
            {
                Directory.Delete(_contentRoot, true);
            }
        }
        catch
        {
            // The temporary folder is left behind when it can't be removed.
        }
    }

    [TestMethod]
    public async Task SignIfChangedAsync_WritesAManifestAndASignature()
    {
        CreatePackage("Remotely-Win-x64.zip");

        var signed = await _signingService.SignIfChangedAsync("x64", default);

        Assert.IsTrue(signed);

        var contentDirectory = Path.Combine(_webRoot, "Content");
        Assert.IsTrue(File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.json")));
        Assert.IsTrue(File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.sig")));
    }

    [TestMethod]
    public async Task SignIfChangedAsync_OnlySignsWhenThePackageChanged()
    {
        CreatePackage("Remotely-Win-x64.zip");

        Assert.IsTrue(await _signingService.SignIfChangedAsync("x64", default));
        Assert.IsFalse(await _signingService.SignIfChangedAsync("x64", default));

        // A new package with different content is signed again.
        CreatePackage("Remotely-Win-x64.zip", "second");
        Assert.IsTrue(await _signingService.SignIfChangedAsync("x64", default));
    }

    [TestMethod]
    public async Task SignIfChangedAsync_ProducesAManifestThatVerifies()
    {
        var packagePath = CreatePackage("Remotely-Win-x64.zip");

        await _signingService.SignIfChangedAsync("x64", default);

        var contentDirectory = Path.Combine(_webRoot, "Content");
        var manifestBytes = File.ReadAllBytes(
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.json"));
        var signature = File.ReadAllText(
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.sig"));

        var packageBytes = File.ReadAllBytes(packagePath);
        var installerBytes = File.ReadAllBytes(
            Path.Combine(contentDirectory, "Install-Remotely.ps1"));

        var trustedKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(
            _signingService.GetTrustedKeysJson())!;

        var result = UpdatePackageVerifier.Verify(
            manifestBytes,
            signature,
            packageBytes,
            installerBytes,
            new Version(0, 0, 0, 0),
            trustedKeys);

        Assert.IsTrue(result.Success, result.Detail);
    }

    [TestMethod]
    public async Task SignIfChangedAsync_AChangedPackageIsRefused()
    {
        var packagePath = CreatePackage("Remotely-Win-x64.zip");

        await _signingService.SignIfChangedAsync("x64", default);

        var contentDirectory = Path.Combine(_webRoot, "Content");
        var manifestBytes = File.ReadAllBytes(
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.json"));
        var signature = File.ReadAllText(
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.sig"));

        // The package is changed after it was signed.
        File.WriteAllBytes(packagePath, new byte[] { 1, 2, 3, 4, 5 });

        var trustedKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(
            _signingService.GetTrustedKeysJson())!;

        var result = UpdatePackageVerifier.Verify(
            manifestBytes,
            signature,
            File.ReadAllBytes(packagePath),
            File.ReadAllBytes(Path.Combine(contentDirectory, "Install-Remotely.ps1")),
            new Version(0, 0, 0, 0),
            trustedKeys);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.PackageHashMismatch, result.Failure);
    }

    [TestMethod]
    public async Task SignIfChangedAsync_AnUnknownKeyIsRefused()
    {
        var packagePath = CreatePackage("Remotely-Win-x64.zip");

        await _signingService.SignIfChangedAsync("x64", default);

        var contentDirectory = Path.Combine(_webRoot, "Content");
        var manifestBytes = File.ReadAllBytes(
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.json"));
        var signature = File.ReadAllText(
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.sig"));

        var result = UpdatePackageVerifier.Verify(
            manifestBytes,
            signature,
            File.ReadAllBytes(packagePath),
            null,
            new Version(0, 0, 0, 0),
            new Dictionary<string, string>());

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.UnknownKeyId, result.Failure);
    }

    [TestMethod]
    public async Task SignIfChangedAsync_PublishesNothingWhenTheResultDoesNotVerify()
    {
        CreatePackage("Remotely-Win-x64.zip");

        var service = new FailingVerificationSigningService(
            _hostEnvironment,
            NullLogger<UpdateSigningService>.Instance);

        var signed = await service.SignIfChangedAsync("x64", default);

        Assert.IsFalse(signed);

        var contentDirectory = Path.Combine(_webRoot, "Content");

        // A manifest that the agents would refuse must never reach the disk.
        Assert.IsFalse(
            File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.json")),
            "The manifest was published even though it does not verify.");
        Assert.IsFalse(
            File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.sig")),
            "The signature was published even though it does not verify.");
    }

    [TestMethod]
    public async Task SignIfChangedAsync_RemovesTheManifestOfThePreviousContentWhenTheResultDoesNotVerify()
    {
        CreatePackage("Remotely-Win-x64.zip");

        Assert.IsTrue(await _signingService.SignIfChangedAsync("x64", default));

        var contentDirectory = Path.Combine(_webRoot, "Content");
        Assert.IsTrue(File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.json")));

        // The package changes and the new manifest does not verify.  The old
        // manifest describes a package that is no longer on disk, so it must
        // not be served next to the new one.
        CreatePackage("Remotely-Win-x64.zip", "second");

        var service = new FailingVerificationSigningService(
            _hostEnvironment,
            NullLogger<UpdateSigningService>.Instance);

        Assert.IsFalse(await service.SignIfChangedAsync("x64", default));

        Assert.IsFalse(
            File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.json")),
            "The stale manifest is still published.");
        Assert.IsFalse(
            File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.manifest.sig")),
            "The stale signature is still published.");
    }

    [TestMethod]
    public async Task SignIfChangedAsync_TriesAgainAfterAFailedVerification()
    {
        CreatePackage("Remotely-Win-x64.zip");

        var service = new FailingVerificationSigningService(
            _hostEnvironment,
            NullLogger<UpdateSigningService>.Instance);

        await service.SignIfChangedAsync("x64", default);
        await service.SignIfChangedAsync("x64", default);

        // The failure must not be remembered as "already signed", otherwise
        // the server would never sign this package again.
        Assert.AreEqual(2, service.VerificationAttempts);
    }

    private string CreatePackage(string fileName, string content = "first")
    {
        var path = Path.Combine(_webRoot, "Content", fileName);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("Remotely_Agent.dll");
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        return path;
    }

    [TestMethod]
    public async Task SignDesktopIfChangedAsync_WritesAManifestAndASignature()
    {
        CreateDesktopClient("Win-x64", "Remotely_Desktop.exe");

        var signed = await _signingService.SignDesktopIfChangedAsync("Win-x64", default);

        Assert.IsTrue(signed);

        var folder = Path.Combine(_webRoot, "Content", "Win-x64");
        Assert.IsTrue(File.Exists(Path.Combine(folder, "Remotely_Desktop.exe.manifest.json")));
        Assert.IsTrue(File.Exists(Path.Combine(folder, "Remotely_Desktop.exe.manifest.sig")));
    }

    [TestMethod]
    public async Task SignDesktopIfChangedAsync_ProducesAManifestThatVerifies()
    {
        var desktopPath = CreateDesktopClient("Win-x64", "Remotely_Desktop.exe");

        await _signingService.SignDesktopIfChangedAsync("Win-x64", default);

        var folder = Path.Combine(_webRoot, "Content", "Win-x64");
        var manifestBytes = File.ReadAllBytes(
            Path.Combine(folder, "Remotely_Desktop.exe.manifest.json"));
        var signature = File.ReadAllText(
            Path.Combine(folder, "Remotely_Desktop.exe.manifest.sig"));

        var trustedKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(
            _signingService.GetTrustedKeysJson())!;

        // The agents download the client without an installer, so the
        // installer hash of the manifest is not checked here.
        var result = UpdatePackageVerifier.Verify(
            manifestBytes,
            signature,
            File.ReadAllBytes(desktopPath),
            null,
            new Version(0, 0, 0, 0),
            trustedKeys);

        Assert.IsTrue(result.Success, result.Detail);
    }

    [TestMethod]
    public async Task SignDesktopIfChangedAsync_UsesTheVersionNextToTheClient()
    {
        CreateDesktopClient("Win-x64", "Remotely_Desktop.exe", "2026.11.19.1208");

        await _signingService.SignDesktopIfChangedAsync("Win-x64", default);

        var folder = Path.Combine(_webRoot, "Content", "Win-x64");
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(
            File.ReadAllText(Path.Combine(folder, "Remotely_Desktop.exe.manifest.json")));

        Assert.IsNotNull(manifest);
        Assert.AreEqual("2026.11.19.1208", manifest!.Version);
        Assert.AreEqual("Remotely_Desktop.exe", manifest.Package);
    }

    [TestMethod]
    public async Task SignDesktopIfChangedAsync_OnlySignsWhenTheClientChanged()
    {
        CreateDesktopClient("Win-x64", "Remotely_Desktop.exe");

        Assert.IsTrue(await _signingService.SignDesktopIfChangedAsync("Win-x64", default));
        Assert.IsFalse(await _signingService.SignDesktopIfChangedAsync("Win-x64", default));

        CreateDesktopClient("Win-x64", "Remotely_Desktop.exe", "2026.11.19.1208", "second");
        Assert.IsTrue(await _signingService.SignDesktopIfChangedAsync("Win-x64", default));
    }

    [TestMethod]
    public async Task SignDesktopIfChangedAsync_DoesNothingWhenTheClientIsNotPublished()
    {
        var signed = await _signingService.SignDesktopIfChangedAsync("Win-x64", default);

        Assert.IsFalse(signed);
    }

    /// <summary>
    /// Publishes the desktop client the way the publish script does, with the
    /// version of the build next to it.
    /// </summary>
    private string CreateDesktopClient(
        string platform,
        string fileName,
        string version = "2026.11.19.1208",
        string content = "desktop-client")
    {
        var folder = Path.Combine(_webRoot, "Content", platform);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        File.WriteAllText(path + ".version", version);

        return path;
    }

    /// <summary>
    /// A signing service whose verification always fails, so that the
    /// behaviour of the server when a manifest does not verify can be
    /// observed.
    /// </summary>
    private sealed class FailingVerificationSigningService : UpdateSigningService
    {
        public FailingVerificationSigningService(
            IWebHostEnvironment hostEnvironment,
            ILogger<UpdateSigningService> logger)
            : base(hostEnvironment, logger)
        {
        }

        public int VerificationAttempts { get; private set; }

        protected override UpdateVerificationResult VerifyManifest(
            byte[] manifestBytes,
            string signatureBase64,
            byte[] packageBytes,
            byte[]? installerBytes,
            string keyId,
            ECDsa key)
        {
            VerificationAttempts++;

            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.InvalidSignature,
                "Refused on purpose by the test.");
        }
    }
}
