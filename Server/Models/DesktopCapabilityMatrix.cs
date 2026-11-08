using System.Runtime.InteropServices;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;

namespace Remotely.Server.Models;

public enum FeatureSupport
{
    Unknown,
    Supported,
    Conditional,
    Unsupported
}

public sealed record DesktopCapabilities(
    FeatureSupport Chat,
    FeatureSupport RemoteControl,
    FeatureSupport Screenshots,
    FeatureSupport TaskManager,
    FeatureSupport CameraRecording,
    FeatureSupport AudioRecording,
    FeatureSupport LiveAudio,
    FeatureSupport KeyboardCapture,
    string Notice)
{
    public static string GetStatusLabel(FeatureSupport status) => status switch
    {
        FeatureSupport.Supported => "Supported",
        FeatureSupport.Conditional => "Conditional",
        FeatureSupport.Unsupported => "Not supported",
        _ => "Unverified"
    };
}

public static class DesktopCapabilityMatrix
{
    /// <summary>
    /// The first Windows client version whose Desktop process includes the
    /// capture and recording features. Older clients know none of them:
    /// screenshot monitoring needs the newer ScreenMonitor, recording needs
    /// ScreenRecorderLib, keyboard capture needs the low-level hook. The
    /// agent reports its assembly version, and Device.AgentVersion carries
    /// that same version stamp for both the agent and the client.
    /// </summary>
    private static readonly Version CaptureFeaturesVersion = new(2026, 11, 7, 1500);

    public static DesktopCapabilities For(Device device)
    {
        var platform = Enum.TryParse<Platform>(device.Platform, true, out var parsedPlatform)
            ? parsedPlatform
            : Platform.Unknown;

        return platform switch
        {
            Platform.Windows => ForWindows(device.OSArchitecture, device.AgentVersion),
            Platform.Linux => new DesktopCapabilities(
                FeatureSupport.Supported,
                FeatureSupport.Conditional,
                FeatureSupport.Conditional,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                "Linux screen control and screenshots depend on an X11-compatible session. Wayland/Xwayland may show a black screen. Camera, audio, and keyboard capture are unavailable in this build."),
            Platform.MacOS => new DesktopCapabilities(
                FeatureSupport.Supported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                "Desktop client features are not implemented for macOS."),
            _ => new DesktopCapabilities(
                FeatureSupport.Supported,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                "The device platform is unknown. Feature availability has not been verified for this client.")
        };
    }

    private static DesktopCapabilities ForWindows(Architecture architecture, string? agentVersion)
    {
        if (architecture is Architecture.Arm or Architecture.Arm64)
        {
            return new DesktopCapabilities(
                FeatureSupport.Supported,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unsupported,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                "Windows desktop capabilities are unverified for this processor architecture; camera recording requires x86 or x64.");
        }

        if (architecture is not (Architecture.X86 or Architecture.X64))
        {
            return new DesktopCapabilities(
                FeatureSupport.Supported,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                FeatureSupport.Unknown,
                "Windows desktop capabilities are unverified for this processor architecture.");
        }

        if (!Version.TryParse(agentVersion, out var version) ||
            version < CaptureFeaturesVersion)
        {
            return new DesktopCapabilities(
                FeatureSupport.Supported,
                FeatureSupport.Supported,
                FeatureSupport.Unsupported,
                FeatureSupport.Supported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                FeatureSupport.Unsupported,
                "This client's version is too old for screenshot capture, recording, and keyboard capture. Update the agent to a recent client first.");
        }

        return new DesktopCapabilities(
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            string.Empty);
    }
}
