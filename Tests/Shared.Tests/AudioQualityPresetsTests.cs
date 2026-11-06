using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Shared.Tests;

/// <summary>
/// The quality presets decide the sample format of an audio recording, and
/// the duration of a recording is bounded so it can never fill the disk of
/// the server or of the device.
/// </summary>
[TestClass]
public class AudioQualityPresetsTests
{
    [TestMethod]
    public void GetPreset_LowQuality_ReturnsTheLowPreset()
    {
        var preset = AudioQualityPresets.GetPreset(AudioQuality.Low);

        Assert.AreEqual(16000, preset.SampleRate);
        Assert.AreEqual(1, preset.Channels);
        Assert.AreEqual(16, preset.BitsPerSample);
    }

    [TestMethod]
    public void GetPreset_MediumQuality_ReturnsTheMediumPreset()
    {
        var preset = AudioQualityPresets.GetPreset(AudioQuality.Medium);

        Assert.AreEqual(22050, preset.SampleRate);
        Assert.AreEqual(1, preset.Channels);
        Assert.AreEqual(16, preset.BitsPerSample);
    }

    [TestMethod]
    public void GetPreset_HighQuality_ReturnsTheHighPreset()
    {
        var preset = AudioQualityPresets.GetPreset(AudioQuality.High);

        Assert.AreEqual(44100, preset.SampleRate);
        Assert.AreEqual(1, preset.Channels);
        Assert.AreEqual(16, preset.BitsPerSample);
    }

    [TestMethod]
    public void GetPreset_UnknownQuality_FallsBackToTheMediumPreset()
    {
        var preset = AudioQualityPresets.GetPreset((AudioQuality)99);

        Assert.AreEqual(
            AudioQualityPresets.GetPreset(AudioQuality.Medium).SampleRate,
            preset.SampleRate);
    }

    [TestMethod]
    public void ClampDurationSeconds_TooShort_IsRaisedToTheMinimum()
    {
        Assert.AreEqual(AudioQualityPresets.MinDurationSeconds, AudioQualityPresets.ClampDurationSeconds(0));
        Assert.AreEqual(AudioQualityPresets.MinDurationSeconds, AudioQualityPresets.ClampDurationSeconds(-10));
    }

    [TestMethod]
    public void ClampDurationSeconds_TooLong_IsLoweredToTheMaximum()
    {
        Assert.AreEqual(AudioQualityPresets.MaxDurationSeconds, AudioQualityPresets.ClampDurationSeconds(100000));
    }

    [TestMethod]
    public void ClampDurationSeconds_InsideTheRange_IsNotChanged()
    {
        Assert.AreEqual(42, AudioQualityPresets.ClampDurationSeconds(42));
        Assert.AreEqual(
            AudioQualityPresets.MinDurationSeconds,
            AudioQualityPresets.ClampDurationSeconds(AudioQualityPresets.MinDurationSeconds));

        Assert.AreEqual(
            AudioQualityPresets.MaxDurationSeconds,
            AudioQualityPresets.ClampDurationSeconds(AudioQualityPresets.MaxDurationSeconds));
    }

    [TestMethod]
    public void ParseQuality_ValidName_ReturnsTheQuality()
    {
        Assert.AreEqual(AudioQuality.High, AudioQualityPresets.ParseQuality("High"));
        Assert.AreEqual(AudioQuality.Low, AudioQualityPresets.ParseQuality("low"));
    }

    [TestMethod]
    public void ParseQuality_EmptyOrInvalidValue_FallsBackToTheMediumQuality()
    {
        Assert.AreEqual(AudioQuality.Medium, AudioQualityPresets.ParseQuality(null));
        Assert.AreEqual(AudioQuality.Medium, AudioQualityPresets.ParseQuality("   "));
        Assert.AreEqual(AudioQuality.Medium, AudioQualityPresets.ParseQuality("not-a-quality"));
        Assert.AreEqual(AudioQuality.Medium, AudioQualityPresets.ParseQuality("99"));
    }

    [TestMethod]
    public void GetBytesPerSecond_MatchesTheSampleFormat()
    {
        var preset = AudioQualityPresets.GetPreset(AudioQuality.Low);

        Assert.AreEqual(32000, AudioQualityPresets.GetBytesPerSecond(preset));
    }
}
