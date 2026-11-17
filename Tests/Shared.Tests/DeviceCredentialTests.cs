using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

/// <summary>
/// The fingerprint of a device credential.
/// <para>
/// The server keeps the fingerprint and never the credential, so the rules
/// have to hold exactly: a credential must match its own fingerprint, a
/// different credential must not, and a record that was written before the
/// fingerprinting must still be accepted and then upgraded.
/// </para>
/// </summary>
[TestClass]
public class DeviceCredentialTests
{
    [TestMethod]
    public void Fingerprint_IsStableAndNotTheCredential()
    {
        var fingerprint = DeviceCredential.Fingerprint("the-credential");

        Assert.AreEqual(fingerprint, DeviceCredential.Fingerprint("the-credential"));
        Assert.AreNotEqual("the-credential", fingerprint);
        Assert.AreEqual(DeviceCredential.FingerprintLength, fingerprint.Length);
        Assert.IsTrue(DeviceCredential.IsFingerprint(fingerprint));
    }

    [TestMethod]
    public void Matches_AcceptsTheCredentialThatProducedTheFingerprint()
    {
        var fingerprint = DeviceCredential.Fingerprint("the-credential");

        Assert.IsTrue(DeviceCredential.Matches("the-credential", fingerprint));
    }

    [TestMethod]
    public void Matches_RefusesADifferentCredential()
    {
        var fingerprint = DeviceCredential.Fingerprint("the-credential");

        Assert.IsFalse(DeviceCredential.Matches("another-credential", fingerprint));
        Assert.IsFalse(DeviceCredential.Matches(string.Empty, fingerprint));
        Assert.IsFalse(DeviceCredential.Matches(null, fingerprint));
        Assert.IsFalse(DeviceCredential.Matches("the-credential", string.Empty));
        Assert.IsFalse(DeviceCredential.Matches("the-credential", null));
    }

    [TestMethod]
    public void Matches_AcceptsALegacyCredentialThatWasStoredInTheClear()
    {
        Assert.IsTrue(DeviceCredential.Matches("legacy-credential", "legacy-credential"));
        Assert.IsFalse(DeviceCredential.Matches("other-credential", "legacy-credential"));
    }

    [TestMethod]
    public void IsFingerprint_RejectsValuesThatAreNotOne()
    {
        Assert.IsFalse(DeviceCredential.IsFingerprint(null));
        Assert.IsFalse(DeviceCredential.IsFingerprint(string.Empty));
        Assert.IsFalse(DeviceCredential.IsFingerprint("legacy-credential"));
        Assert.IsFalse(DeviceCredential.IsFingerprint(new string('z', 64)));
    }

    [TestMethod]
    public void ToFingerprint_KeepsAFingerprintAndFingerprintsACredential()
    {
        var fingerprint = DeviceCredential.Fingerprint("the-credential");

        Assert.AreEqual(fingerprint, DeviceCredential.ToFingerprint(fingerprint));
        Assert.AreEqual(fingerprint, DeviceCredential.ToFingerprint("the-credential"));
    }
}
