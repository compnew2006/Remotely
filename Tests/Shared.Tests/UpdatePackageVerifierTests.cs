using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Services;

namespace Remotely.Shared.Tests;

/// <summary>
/// G2 security tests: the update verifier must accept a legitimately signed
/// package and reject every tampered, unknown-key, hash-mismatched, or
/// rolled-back artifact (fail-closed).
/// </summary>
[TestClass]
public class UpdatePackageVerifierTests
{
    private static (ECDsa Key, string PublicKeyBase64) GenerateKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        return (key, spki);
    }

    private static byte[] BuildManifestJson(string version, string packageHash, string installerHash, string keyId)
    {
        var manifest = new UpdateManifest(
            Package: "Remotely-Win-x64.zip",
            Version: version,
            Sha256: packageHash,
            InstallerSha256: installerHash,
            KeyId: keyId);

        return JsonSerializer.SerializeToUtf8Bytes(manifest);
    }

    private static byte[] Sha256Hex(string content) =>
        Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));

    [TestMethod]
    public void Verify_ValidSignedManifest_Succeeds()
    {
        var (key, publicKey) = GenerateKey();
        var package = Encoding.UTF8.GetBytes("package-content");
        var installer = Encoding.UTF8.GetBytes("installer-content");
        var manifest = BuildManifestJson(
            "2.0.0",
            Convert.ToHexString(SHA256.HashData(package)),
            Convert.ToHexString(SHA256.HashData(installer)),
            "test-2026-11");
        var signature = Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256));

        var result = UpdatePackageVerifier.Verify(
            manifest, signature, package, installer,
            new Version(1, 0, 0),
            new Dictionary<string, string> { ["test-2026-11"] = publicKey });

        Assert.IsTrue(result.Success, result.Detail);
    }

    [TestMethod]
    public void Verify_TamperedManifest_Fails()
    {
        var (key, publicKey) = GenerateKey();
        var package = Encoding.UTF8.GetBytes("package-content");
        var manifest = BuildManifestJson("2.0.0", "aa", "bb", "test-2026-11");
        var signature = Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256));

        // Tamper with a signed field while keeping the manifest valid JSON:
        // the signature was made over different bytes, so it must not verify.
        var tampered = BuildManifestJson("2.0.1", "aa", "bb", "test-2026-11");
        Assert.IsFalse(manifest.AsSpan().SequenceEqual(tampered));

        var result = UpdatePackageVerifier.Verify(
            tampered, signature, package, null,
            new Version(1, 0, 0),
            new Dictionary<string, string> { ["test-2026-11"] = publicKey });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.InvalidSignature, result.Failure);
    }

    [TestMethod]
    public void Verify_UnknownKeyId_Fails()
    {
        var (key, _) = GenerateKey();
        var otherKey = GenerateKey();
        var package = Encoding.UTF8.GetBytes("package-content");
        var manifest = BuildManifestJson("2.0.0", "aa", "bb", "rogue-key");
        var signature = Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256));

        var result = UpdatePackageVerifier.Verify(
            manifest, signature, package, null,
            new Version(1, 0, 0),
            new Dictionary<string, string> { ["test-2026-11"] = otherKey.PublicKeyBase64 });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.UnknownKeyId, result.Failure);
    }

    [TestMethod]
    public void Verify_PackageHashMismatch_Fails()
    {
        var (key, publicKey) = GenerateKey();
        var signedPackage = Encoding.UTF8.GetBytes("real-package");
        var deliveredPackage = Encoding.UTF8.GetBytes("TAMPERED-package");
        var manifest = BuildManifestJson(
            "2.0.0",
            Convert.ToHexString(SHA256.HashData(signedPackage)),
            "bb",
            "test-2026-11");
        var signature = Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256));

        var result = UpdatePackageVerifier.Verify(
            manifest, signature, deliveredPackage, null,
            new Version(1, 0, 0),
            new Dictionary<string, string> { ["test-2026-11"] = publicKey });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.PackageHashMismatch, result.Failure);
    }

    [TestMethod]
    public void Verify_InstallerHashMismatch_Fails()
    {
        var (key, publicKey) = GenerateKey();
        var package = Encoding.UTF8.GetBytes("package-content");
        var signedInstaller = Encoding.UTF8.GetBytes("real-installer");
        var deliveredInstaller = Encoding.UTF8.GetBytes("evil-installer");
        var manifest = BuildManifestJson(
            "2.0.0",
            Convert.ToHexString(SHA256.HashData(package)),
            Convert.ToHexString(SHA256.HashData(signedInstaller)),
            "test-2026-11");
        var signature = Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256));

        var result = UpdatePackageVerifier.Verify(
            manifest, signature, package, deliveredInstaller,
            new Version(1, 0, 0),
            new Dictionary<string, string> { ["test-2026-11"] = publicKey });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.InstallerHashMismatch, result.Failure);
    }

    [TestMethod]
    public void Verify_VersionRollback_Fails()
    {
        var (key, publicKey) = GenerateKey();
        var package = Encoding.UTF8.GetBytes("package-content");
        var manifest = BuildManifestJson(
            "1.0.0",
            Convert.ToHexString(SHA256.HashData(package)),
            "bb",
            "test-2026-11");
        var signature = Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256));

        var result = UpdatePackageVerifier.Verify(
            manifest, signature, package, null,
            new Version(2, 0, 0),
            new Dictionary<string, string> { ["test-2026-11"] = publicKey });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.VersionRollback, result.Failure);
    }

    [TestMethod]
    public void Verify_MalformedManifest_Fails()
    {
        var (_, publicKey) = GenerateKey();
        var package = Encoding.UTF8.GetBytes("package-content");

        var result = UpdatePackageVerifier.Verify(
            Encoding.UTF8.GetBytes("this is not json {}"),
            Convert.ToBase64String(new byte[64]),
            package, null,
            new Version(1, 0, 0),
            new Dictionary<string, string> { ["test-2026-11"] = publicKey });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.ManifestMalformed, result.Failure);
    }

    [TestMethod]
    [Description("Fail-closed: with no trusted keys configured, nothing can pass verification.")]
    public void Verify_NoTrustedKeys_Fails()
    {
        var (key, _) = GenerateKey();
        var package = Encoding.UTF8.GetBytes("package-content");
        var manifest = BuildManifestJson("2.0.0", "aa", "bb", "test-2026-11");
        var signature = Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256));

        var result = UpdatePackageVerifier.Verify(
            manifest, signature, package, null,
            new Version(1, 0, 0),
            new Dictionary<string, string>());

        Assert.IsFalse(result.Success);
        Assert.AreEqual(UpdateVerificationFailure.UnknownKeyId, result.Failure);
    }
}
