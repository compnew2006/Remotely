using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Services;
using Remotely.Shared.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        Assert.IsTrue(File.Exists(Path.Combine(contentDirectory, "Remotely-Win-x64.zip.sig")));
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
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.sig"));

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
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.sig"));

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
            Path.Combine(contentDirectory, "Remotely-Win-x64.zip.sig"));

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
}
