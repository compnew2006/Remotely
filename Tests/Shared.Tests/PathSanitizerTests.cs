using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

/// <summary>
/// G3 security tests: remote-supplied file names must reduce to bare file
/// names and must never survive sanitization with directory traversal,
/// rooted paths, or malformed content.
/// </summary>
[TestClass]
public class PathSanitizerTests
{
    [TestMethod]
    [DataRow(@"..\..\..\Windows\System32\evil.exe", "evil.exe")]
    [DataRow("../../../../etc/passwd", "passwd")]
    [DataRow(@"C:\Windows\System32\evil.dll", "evil.dll")]
    [DataRow("/etc/shadow", "shadow")]
    [DataRow(@"sub\dir\file.txt", "file.txt")]
    [DataRow("file.txt", "file.txt")]
    public void TrySanitizeFileName_DirectoryComponentsAreStripped(string input, string expected)
    {
        Assert.IsTrue(PathSanitizer.TrySanitizeFileName(input, out var sanitized));
        Assert.AreEqual(expected, sanitized);
        Assert.IsFalse(sanitized.Contains('\\'));
        Assert.IsFalse(sanitized.Contains('/'));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(@"..\..")]
    [DataRow("..")]
    [DataRow(".")]
    [DataRow("///")]
    [DataRow(@"c:\")]
    [DataRow("file<name>.txt")]
    [DataRow("file:name.txt")]
    [DataRow("con.txt")]
    public void TrySanitizeFileName_UnsafeNamesAreRejected(string input)
    {
        Assert.IsFalse(
            PathSanitizer.TrySanitizeFileName(input, out var sanitized),
            $"'{input}' must be rejected.");
        Assert.AreEqual(string.Empty, sanitized);
    }

    [TestMethod]
    public void TrySanitizeFileName_NullIsRejected()
    {
        Assert.IsFalse(PathSanitizer.TrySanitizeFileName(null, out _));
    }

    [TestMethod]
    public void TrySanitizeFileName_SanitizedNameCombinedWithBaseDirectory_StaysInside()
    {
        // The end-to-end guarantee the file transfer service relies on: after
        // sanitization, combining with the base directory can never escape it.
        var baseDir = OperatingSystem.IsWindows() ? @"C:\ProgramData\RemoteControl\Shared" : "/var/lib/remotely/shared";
        var result = PathSanitizer.TrySanitizeFileName(@"..\..\..\Windows\System32\evil.exe", out var sanitized);

        Assert.IsTrue(result);
        var combined = System.IO.Path.Combine(baseDir, sanitized);
        var full = System.IO.Path.GetFullPath(combined);
        Assert.IsTrue(full.StartsWith(System.IO.Path.GetFullPath(baseDir), StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(full.Contains(".."));
    }
}
