using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Shared.Tests;

/// <summary>
/// The quality presets decide the resolution, the frame rate, and the bitrate
/// of a camera recording, and the duration of a recording is bounded so it
/// can never fill the disk of the server or of the device.
/// </summary>
[TestClass]
public class CameraQualityPresetsTests
{
    [TestMethod]
    public void GetPreset_LowQuality_ReturnsTheLowPreset()
    {
        var preset = CameraQualityPresets.GetPreset(CameraQuality.Low);

        Assert.AreEqual(640, preset.Width);
        Assert.AreEqual(480, preset.Height);
        Assert.AreEqual(15, preset.Framerate);
        Assert.AreEqual(1_000_000, preset.Bitrate);
    }

    [TestMethod]
    public void GetPreset_MediumQuality_ReturnsTheMediumPreset()
    {
        var preset = CameraQualityPresets.GetPreset(CameraQuality.Medium);

        Assert.AreEqual(1280, preset.Width);
        Assert.AreEqual(720, preset.Height);
        Assert.AreEqual(30, preset.Framerate);
        Assert.AreEqual(2_500_000, preset.Bitrate);
    }

    [TestMethod]
    public void GetPreset_HighQuality_ReturnsTheHighPreset()
    {
        var preset = CameraQualityPresets.GetPreset(CameraQuality.High);

        Assert.AreEqual(1920, preset.Width);
        Assert.AreEqual(1080, preset.Height);
        Assert.AreEqual(30, preset.Framerate);
        Assert.AreEqual(5_000_000, preset.Bitrate);
    }

    [TestMethod]
    public void GetPreset_UnknownQuality_FallsBackToTheMediumPreset()
    {
        var preset = CameraQualityPresets.GetPreset((CameraQuality)99);

        Assert.AreEqual(
            CameraQualityPresets.GetPreset(CameraQuality.Medium).Width,
            preset.Width);

        Assert.AreEqual(
            CameraQualityPresets.GetPreset(CameraQuality.Medium).Bitrate,
            preset.Bitrate);
    }

    [TestMethod]
    public void ClampDurationSeconds_TooShort_IsRaisedToTheMinimum()
    {
        Assert.AreEqual(CameraQualityPresets.MinDurationSeconds, CameraQualityPresets.ClampDurationSeconds(0));
        Assert.AreEqual(CameraQualityPresets.MinDurationSeconds, CameraQualityPresets.ClampDurationSeconds(-10));
    }

    [TestMethod]
    public void ClampDurationSeconds_TooLong_IsLoweredToTheMaximum()
    {
        Assert.AreEqual(CameraQualityPresets.MaxDurationSeconds, CameraQualityPresets.ClampDurationSeconds(100000));
    }

    [TestMethod]
    public void ClampDurationSeconds_InsideTheRange_IsNotChanged()
    {
        Assert.AreEqual(42, CameraQualityPresets.ClampDurationSeconds(42));
        Assert.AreEqual(
            CameraQualityPresets.MinDurationSeconds,
            CameraQualityPresets.ClampDurationSeconds(CameraQualityPresets.MinDurationSeconds));

        Assert.AreEqual(
            CameraQualityPresets.MaxDurationSeconds,
            CameraQualityPresets.ClampDurationSeconds(CameraQualityPresets.MaxDurationSeconds));
    }

    [TestMethod]
    public void ParseQuality_ValidName_ReturnsTheQuality()
    {
        Assert.AreEqual(CameraQuality.High, CameraQualityPresets.ParseQuality("High"));
        Assert.AreEqual(CameraQuality.Low, CameraQualityPresets.ParseQuality("low"));
    }

    [TestMethod]
    public void ParseQuality_EmptyOrInvalidValue_FallsBackToTheMediumQuality()
    {
        Assert.AreEqual(CameraQuality.Medium, CameraQualityPresets.ParseQuality(null));
        Assert.AreEqual(CameraQuality.Medium, CameraQualityPresets.ParseQuality("   "));
        Assert.AreEqual(CameraQuality.Medium, CameraQualityPresets.ParseQuality("not-a-quality"));
        Assert.AreEqual(CameraQuality.Medium, CameraQualityPresets.ParseQuality("99"));
    }
}
