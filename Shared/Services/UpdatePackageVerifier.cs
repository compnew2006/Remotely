using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Remotely.Shared.Services;

/// <summary>
/// Update package manifest (G2 / AD-13 hybrid model). Signed with ECDsa P-256
/// over the exact manifest file bytes; the signature travels separately as
/// base64 (.sig). Private keys are never stored in this repository.
/// </summary>
public sealed record UpdateManifest(
    string Package,
    string Version,
    string Sha256,
    string InstallerSha256,
    string KeyId);

public enum UpdateVerificationFailure
{
    None,
    ManifestMalformed,
    UnknownKeyId,
    InvalidSignature,
    PackageHashMismatch,
    InstallerHashMismatch,
    VersionRollback
}

public sealed record UpdateVerificationResult(
    bool Success,
    UpdateVerificationFailure Failure,
    string Detail)
{
    public static UpdateVerificationResult Pass() =>
        new(true, UpdateVerificationFailure.None, string.Empty);

    public static UpdateVerificationResult Fail(UpdateVerificationFailure failure, string detail) =>
        new(false, failure, detail);
}

/// <summary>
/// Verifies a signed update manifest and its package before anything is
/// executed or extracted (G2 — signed and verified client updates).
///
/// Verification is fail-closed: any missing, malformed, unknown-key,
/// tampered, hash-mismatched, or older-version artifact aborts the update.
/// Production signing keys are injected at build/publish time; development
/// and tests supply keys explicitly. Signing keys never live in this repo.
/// </summary>
public static class UpdatePackageVerifier
{
    public const string ManifestMediaType = "application/json";

    /// <summary>
    /// Keys compiled into the client. Populated by the release pipeline
    /// (production pubkeys); empty until then, which makes verification
    /// fail-closed out of the box.
    /// </summary>
    public static readonly Dictionary<string, string> BuiltInTrustedKeys = new();

    /// <summary>
    /// Loads additional trusted public keys from a JSON file next to the
    /// executable: { "keyId": "base64 SPKI", ... }. Deployed by configuration
    /// management, never committed.
    /// </summary>
    public static Dictionary<string, string> LoadTrustedKeysFile(string? path)
    {
        var keys = new Dictionary<string, string>(BuiltInTrustedKeys);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return keys;
        }

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            keys[prop.Name] = prop.Value.GetString() ?? string.Empty;
        }
        return keys;
    }

    public static UpdateVerificationResult Verify(
        byte[] manifestBytes,
        string signatureBase64,
        byte[] packageBytes,
        byte[]? installerBytes,
        Version currentVersion,
        IReadOnlyDictionary<string, string> trustedKeys)
    {
        UpdateManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<UpdateManifest>(
                manifestBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new JsonException("Manifest deserialized to null.");
        }
        catch (Exception ex)
        {
            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.ManifestMalformed,
                $"Manifest could not be parsed: {ex.Message}");
        }

        if (manifest.Package is null ||
            string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.Sha256) ||
            string.IsNullOrWhiteSpace(manifest.KeyId))
        {
            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.ManifestMalformed,
                "Manifest is missing required fields.");
        }

        // 1. The signing key must be one we explicitly trust (reject unknown keys).
        if (!trustedKeys.TryGetValue(manifest.KeyId, out var publicKeyBase64) ||
            string.IsNullOrWhiteSpace(publicKeyBase64))
        {
            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.UnknownKeyId,
                $"Key ID '{manifest.KeyId}' is not trusted.");
        }

        // 2. The manifest signature must verify against that key.
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            var signature = Convert.FromBase64String(signatureBase64);
            if (!ecdsa.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256))
            {
                return UpdateVerificationResult.Fail(
                    UpdateVerificationFailure.InvalidSignature,
                    "Manifest signature is invalid.");
            }
        }
        catch (Exception ex)
        {
            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.InvalidSignature,
                $"Signature check failed: {ex.Message}");
        }

        // 3. The package must match the signed hash, before it is ever extracted.
        var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes));
        if (!string.Equals(packageHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.PackageHashMismatch,
                "Package hash does not match the signed manifest.");
        }

        // 4. The installer script (also executed by the updater) must match too.
        if (installerBytes is not null)
        {
            if (string.IsNullOrWhiteSpace(manifest.InstallerSha256))
            {
                return UpdateVerificationResult.Fail(
                    UpdateVerificationFailure.InstallerHashMismatch,
                    "Manifest does not carry an installer hash.");
            }

            var installerHash = Convert.ToHexString(SHA256.HashData(installerBytes));
            if (!string.Equals(installerHash, manifest.InstallerSha256, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateVerificationResult.Fail(
                    UpdateVerificationFailure.InstallerHashMismatch,
                    "Installer hash does not match the signed manifest.");
            }
        }

        // 5. Rollback protection: refuse versions older than the running client.
        if (!Version.TryParse(manifest.Version, out var manifestVersion))
        {
            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.ManifestMalformed,
                $"Manifest version '{manifest.Version}' is not a valid version.");
        }

        if (manifestVersion < currentVersion)
        {
            return UpdateVerificationResult.Fail(
                UpdateVerificationFailure.VersionRollback,
                $"Manifest version {manifestVersion} is older than the installed {currentVersion}.");
        }

        return UpdateVerificationResult.Pass();
    }
}
