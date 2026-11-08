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
    public static DesktopCapabilities For(Device device)
    {
        var platform = Enum.TryParse<Platform>(device.Platform, true, out var parsedPlatform)
            ? parsedPlatform
            : Platform.Unknown;

        return platform switch
        {
            Platform.Windows => ForWindows(device.OSArchitecture),
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

    private static DesktopCapabilities ForWindows(Architecture architecture)
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

        return new DesktopCapabilities(
            FeatureSupport.Supported,
            FeatureSupport.Supported,
            FeatureSupport.Conditional,
            FeatureSupport.Supported,
            FeatureSupport.Conditional,
            FeatureSupport.Conditional,
            FeatureSupport.Conditional,
            FeatureSupport.Conditional,
            "Screen capture, recording, and keyboard capture require a compatible updated Windows desktop client.");
    }
}
