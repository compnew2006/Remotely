#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Models;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using System;
using System.Runtime.InteropServices;

namespace Remotely.Server.Tests;

/// <summary>
/// Verifies the Windows desktop capability matrix used by the device cards.
/// Capture and recording features are available from the client version
/// that ships ScreenRecorderLib and the low-level capture hooks onwards.
/// </summary>
[TestClass]
public class DesktopCapabilityMatrixTests
{
    private static Device WindowsDevice(string agentVersion, Architecture architecture = Architecture.X64)
    {
        return new Device
        {
            ID = Guid.NewGuid().ToString(),
            DeviceName = "WINDOWS-01",
            Platform = Platform.Windows.ToString(),
            OSArchitecture = architecture,
            AgentVersion = agentVersion
        };
    }

    [TestMethod]
    public void For_RecentWindowsClient_IsFullySupported()
    {
        var capabilities = DesktopCapabilityMatrix.For(WindowsDevice("2026.11.07.2400"));

        Assert.AreEqual(FeatureSupport.Supported, capabilities.Screenshots);
        Assert.AreEqual(FeatureSupport.Supported, capabilities.CameraRecording);
        Assert.AreEqual(FeatureSupport.Supported, capabilities.AudioRecording);
        Assert.AreEqual(FeatureSupport.Supported, capabilities.KeyboardCapture);
        Assert.IsTrue(string.IsNullOrWhiteSpace(capabilities.Notice));
    }

    [TestMethod]
    public void For_OldWindowsClient_ReportsCaptureFeaturesUnsupported()
    {
        var capabilities = DesktopCapabilityMatrix.For(WindowsDevice("2026.11.07.1300"));

        Assert.AreEqual(FeatureSupport.Unsupported, capabilities.Screenshots);
        Assert.AreEqual(FeatureSupport.Unsupported, capabilities.CameraRecording);
        Assert.AreEqual(FeatureSupport.Unsupported, capabilities.AudioRecording);
        Assert.AreEqual(FeatureSupport.Unsupported, capabilities.KeyboardCapture);
        Assert.IsFalse(string.IsNullOrWhiteSpace(capabilities.Notice));
    }

    [TestMethod]
    public void For_UnknownWindowsVersion_ReportsCaptureFeaturesUnsupported()
    {
        var capabilities = DesktopCapabilityMatrix.For(WindowsDevice(string.Empty));

        Assert.AreEqual(FeatureSupport.Unsupported, capabilities.CameraRecording);
        Assert.AreEqual(FeatureSupport.Unsupported, capabilities.KeyboardCapture);
    }

    [TestMethod]
    public void For_WindowsArm64_StillUnsupportedForCameraRecording()
    {
        var capabilities = DesktopCapabilityMatrix.For(WindowsDevice("2026.11.07.2400", Architecture.Arm64));

        Assert.AreEqual(FeatureSupport.Unsupported, capabilities.CameraRecording);
        Assert.AreEqual(FeatureSupport.Unknown, capabilities.Screenshots);
    }
}
