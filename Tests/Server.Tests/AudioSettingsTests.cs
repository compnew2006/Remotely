using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class AudioSettingsTests
{
    private TestData _testData = null!;
    private IDataService _dataService = null!;

    [TestInitialize]
    public async Task Init()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
    }

    [TestMethod]
    public void NewDevice_HasAudioRetentionDefaults()
    {
        var device = new Device();

        Assert.AreEqual(200, device.AudioRetentionCount);
        Assert.AreEqual(24, device.AudioRetentionHours);
    }

    [TestMethod]
    public async Task UpdateAudioSettings_ChangesOnlyAudioSettings()
    {
        const string screenshotDirectory = "screenshots";
        const string cameraDirectory = "camera-clips";
        const string audioDirectory = "audio-clips";
        var screenshotPauseWindows = ScreenshotPause.Serialize(new[]
        {
            new ScreenshotPauseWindow(9, 17)
        });
        var cameraPauseWindows = ScreenshotPause.Serialize(new[]
        {
            new ScreenshotPauseWindow(22, 7)
        });
        var audioPauseWindows = ScreenshotPause.Serialize(new[]
        {
            new ScreenshotPauseWindow(22, 7),
            new ScreenshotPauseWindow(12, 13)
        });

        Assert.IsTrue((await _dataService.UpdateScreenshotSettings(
            _testData.Org1Device1.ID,
            screenshotDirectory,
            11,
            42,
            screenshotPauseWindows)).IsSuccess);

        Assert.IsTrue((await _dataService.UpdateCameraSettings(
            _testData.Org1Device1.ID,
            cameraDirectory,
            123,
            48,
            cameraPauseWindows)).IsSuccess);

        Assert.IsTrue((await _dataService.UpdateAudioSettings(
            _testData.Org1Device1.ID,
            audioDirectory,
            321,
            72,
            audioPauseWindows)).IsSuccess);

        var deviceResult = await _dataService.GetDevice(_testData.Org1Device1.ID);
        Assert.IsTrue(deviceResult.IsSuccess);

        var device = deviceResult.Value!;
        Assert.AreEqual(screenshotDirectory, device.ScreenshotDirectory);
        Assert.AreEqual(11, device.ScreenshotRetentionCount);
        Assert.AreEqual(42, device.ScreenshotRetentionHours);
        Assert.AreEqual(screenshotPauseWindows, device.ScreenshotPauseWindows);
        Assert.AreEqual(cameraDirectory, device.CameraDirectory);
        Assert.AreEqual(123, device.CameraRetentionCount);
        Assert.AreEqual(48, device.CameraRetentionHours);
        Assert.AreEqual(cameraPauseWindows, device.CameraPauseWindows);
        Assert.AreEqual(audioDirectory, device.AudioDirectory);
        Assert.AreEqual(321, device.AudioRetentionCount);
        Assert.AreEqual(72, device.AudioRetentionHours);
        Assert.AreEqual(audioPauseWindows, device.AudioPauseWindows);
    }

    [TestMethod]
    public async Task UpdateAudioSettings_ClampsRetentionToSupportedLimits()
    {
        Assert.IsTrue((await _dataService.UpdateAudioSettings(
            _testData.Org1Device1.ID,
            null,
            2501,
            9000)).IsSuccess);

        var deviceResult = await _dataService.GetDevice(_testData.Org1Device1.ID);

        Assert.IsTrue(deviceResult.IsSuccess);
        Assert.AreEqual(2000, deviceResult.Value!.AudioRetentionCount);
        Assert.AreEqual(8760, deviceResult.Value.AudioRetentionHours);
    }

    [TestMethod]
    public async Task UpdateAudioSettings_RejectsValuesThatExceedStorageLimits()
    {
        var tooLong = new string('x', 501);

        var directoryResult = await _dataService.UpdateAudioSettings(
            _testData.Org1Device1.ID,
            tooLong,
            10,
            24);
        var pauseWindowsResult = await _dataService.UpdateAudioSettings(
            _testData.Org1Device1.ID,
            null,
            10,
            24,
            tooLong);

        Assert.IsFalse(directoryResult.IsSuccess);
        Assert.IsFalse(pauseWindowsResult.IsSuccess);
    }
}
