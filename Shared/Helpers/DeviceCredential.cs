using System;
using System.Security.Cryptography;
using System.Text;

namespace Remotely.Shared.Helpers;

/// <summary>
/// The fingerprint of a device credential.
/// <para>
/// The server stores the fingerprint of the credential, not the credential
/// itself.  The credential only ever exists on the device that owns it, and
/// the agent proves that it holds it by presenting it.  A copy of the
/// database therefore cannot be used to impersonate a device: the fingerprint
/// of a credential cannot be turned back into the credential.
/// </para>
/// <para>
/// A record that was written before the fingerprinting stores the credential
/// in the clear.  Such a value is still accepted, and it is replaced by its
/// fingerprint at the first registration that presents it.
/// </para>
/// </summary>
public static class DeviceCredential
{
    /// <summary>
    /// The number of characters of a fingerprint: a SHA-256 in lower case hex.
    /// </summary>
    public const int FingerprintLength = 64;

    /// <summary>
    /// The fingerprint of a credential.
    /// </summary>
    public static string Fingerprint(string credential)
    {
        return Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)))
            .ToLowerInvariant();
    }

    /// <summary>
    /// Whether the stored value is a fingerprint rather than a credential that
    /// was written in the clear.
    /// </summary>
    public static bool IsFingerprint(string? value)
    {
        if (value is null || value.Length != FingerprintLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the presented credential is the one that the stored value
    /// stands for.  The comparison takes the same time for every input.
    /// </summary>
    public static bool Matches(string? presentedCredential, string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(presentedCredential) ||
            string.IsNullOrWhiteSpace(storedValue))
        {
            return false;
        }

        var presented = IsFingerprint(storedValue)
            ? Fingerprint(presentedCredential)
            : presentedCredential;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(storedValue));
    }

    /// <summary>
    /// The fingerprint of a stored value, whether that value is a credential
    /// in the clear or a fingerprint already.
    /// </summary>
    public static string ToFingerprint(string storedValue)
    {
        return IsFingerprint(storedValue) ? storedValue : Fingerprint(storedValue);
    }
}
