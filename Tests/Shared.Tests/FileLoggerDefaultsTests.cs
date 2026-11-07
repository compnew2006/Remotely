using System;
using System.IO;
using Remotely.Shared.Services;
using Remotely.Shared.Utilities;

namespace Remotely.Shared.Tests;

[TestClass]
public class FileLoggerDefaultsTests
{
    [TestMethod]
    public void LogsFolderPath_UsesRdpRootOnWindowsAndKeepsUnixPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RDP",
                "Logs");

            if (EnvironmentHelper.IsDebug)
            {
                expected += "_Debug";
            }

            Assert.AreEqual(
                Path.GetFullPath(expected),
                Path.GetFullPath(FileLoggerDefaults.LogsFolderPath));
            return;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var expected = EnvironmentHelper.IsDebug ? "/var/log/remotely_debug" : "/var/log/remotely";
            Assert.AreEqual(expected, FileLoggerDefaults.LogsFolderPath);
            return;
        }

        Assert.ThrowsException<PlatformNotSupportedException>(() => _ = FileLoggerDefaults.LogsFolderPath);
    }
}
