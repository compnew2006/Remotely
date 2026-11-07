using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class KeyboardSettingsTests
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
    public void NewDevice_HasKeyboardDefaults()
    {
        var device = new Device();

        Assert.IsFalse(device.KeyboardEnabled);
        Assert.IsFalse(device.KeyboardShowPasswords);
        Assert.AreEqual(500, device.KeyboardRetentionCount);
        Assert.AreEqual(24, device.KeyboardRetentionHours);
    }

    [TestMethod]
    public async Task UpdateKeyboardSettings_StoresWhetherPasswordsAreShown()
    {
        Assert.IsTrue((await _dataService.UpdateKeyboardSettings(
            _testData.Org1Device1.ID,
            true,
            500,
            24,
            null,
            true)).IsSuccess);

        var deviceResult = await _dataService.GetDevice(_testData.Org1Device1.ID);
        Assert.IsTrue(deviceResult.IsSuccess);
        Assert.IsTrue(deviceResult.Value!.KeyboardShowPasswords);

        Assert.IsTrue((await _dataService.UpdateKeyboardSettings(
            _testData.Org1Device1.ID,
            true,
            500,
            24,
            null,
            false)).IsSuccess);

        deviceResult = await _dataService.GetDevice(_testData.Org1Device1.ID);
        Assert.IsTrue(deviceResult.IsSuccess);
        Assert.IsFalse(deviceResult.Value!.KeyboardShowPasswords);
    }

    [TestMethod]
    public async Task UpdateKeyboardSettings_ChangesOnlyKeyboardSettings()
    {
        const string screenshotDirectory = "screenshots";
        var screenshotPauseWindows = Remotely.Shared.Utilities.ScreenshotPause.Serialize(new[]
        {
            new Remotely.Shared.Models.ScreenshotPauseWindow(9, 17)
        });
        var keyboardPauseWindows = Remotely.Shared.Utilities.ScreenshotPause.Serialize(new[]
        {
            new Remotely.Shared.Models.ScreenshotPauseWindow(22, 7)
        });

        Assert.IsTrue((await _dataService.UpdateScreenshotSettings(
            _testData.Org1Device1.ID,
            screenshotDirectory,
            11,
            42,
            screenshotPauseWindows)).IsSuccess);

        Assert.IsTrue((await _dataService.UpdateKeyboardSettings(
            _testData.Org1Device1.ID,
            true,
            321,
            72,
            keyboardPauseWindows)).IsSuccess);

        var deviceResult = await _dataService.GetDevice(_testData.Org1Device1.ID);
        Assert.IsTrue(deviceResult.IsSuccess);

        var device = deviceResult.Value!;
        Assert.AreEqual(screenshotDirectory, device.ScreenshotDirectory);
        Assert.AreEqual(11, device.ScreenshotRetentionCount);
        Assert.AreEqual(screenshotPauseWindows, device.ScreenshotPauseWindows);
        Assert.IsTrue(device.KeyboardEnabled);
        Assert.AreEqual(321, device.KeyboardRetentionCount);
        Assert.AreEqual(72, device.KeyboardRetentionHours);
        Assert.AreEqual(keyboardPauseWindows, device.KeyboardPauseWindows);
    }

    [TestMethod]
    public async Task UpdateKeyboardSettings_ClampsRetentionToSupportedLimits()
    {
        Assert.IsTrue((await _dataService.UpdateKeyboardSettings(
            _testData.Org1Device1.ID,
            false,
            99999,
            9000)).IsSuccess);

        var deviceResult = await _dataService.GetDevice(_testData.Org1Device1.ID);

        Assert.IsTrue(deviceResult.IsSuccess);
        Assert.AreEqual(20000, deviceResult.Value!.KeyboardRetentionCount);
        Assert.AreEqual(8760, deviceResult.Value.KeyboardRetentionHours);
    }

    [TestMethod]
    public async Task UpdateKeyboardSettings_RejectsPauseWindowsThatExceedTheStorageLimit()
    {
        var tooLong = new string('x', 501);

        var result = await _dataService.UpdateKeyboardSettings(
            _testData.Org1Device1.ID,
            false,
            10,
            24,
            tooLong);

        Assert.IsFalse(result.IsSuccess);
    }
}
