using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

/// <summary>
/// The text that is captured on a device is stored with its window and its
/// process, the logs of one device are never returned for another device,
/// and the retention limits are applied.
/// </summary>
[TestClass]
public class KeyboardLogServiceTests
{
    private KeyboardLogService _keyboardLogService = null!;
    private Device _device = null!;
    private TestData _testData = null!;

    [TestInitialize]
    public async Task Init()
    {
        _testData = new TestData();
        await _testData.Init();

        _device = _testData.Org1Device1;

        var dbFactory = IoCActivator.ServiceProvider.GetRequiredService<IAppDbFactory>();

        _keyboardLogService = new KeyboardLogService(
            dbFactory,
            NullLogger<KeyboardLogService>.Instance);
    }

    [TestMethod]
    [Description("A burst of captured text is stored with its window and its process.")]
    public async Task SaveLog_StoresTheTextAndItsContext()
    {
        var log = await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk(
                "Invoice - Notepad",
                "notepad",
                "hello world",
                false));

        Assert.IsNotNull(log);
        Assert.AreEqual("Invoice - Notepad", log!.WindowTitle);
        Assert.AreEqual("notepad", log.ProcessName);
        Assert.AreEqual("hello world", log.Text);
        Assert.IsFalse(log.IsPasswordField);
        Assert.AreEqual(_device.ID, log.DeviceID);
    }

    [TestMethod]
    [Description("The text of a password field is stored as a mask, and it is marked as such.")]
    public async Task SaveLog_APasswordField_IsMarkedAndMasked()
    {
        var log = await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk(
                "Sign in",
                "explorer",
                "••••••",
                true));

        Assert.IsNotNull(log);
        Assert.IsTrue(log!.IsPasswordField);
        Assert.AreEqual("••••••", log.Text);
    }

    [TestMethod]
    [Description("An empty burst is not stored.")]
    public async Task SaveLog_EmptyText_IsNotStored()
    {
        var log = await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk("Window", "process", string.Empty, false));

        Assert.IsNull(log);
    }

    [TestMethod]
    [Description("The logs of one device are not returned for another device.")]
    public async Task GetLogs_AnotherDevice_DoesNotReturnTheLogs()
    {
        await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk("Window", "process", "text", false));

        var otherDevice = _testData.Org1Device2;

        Assert.AreEqual(1, _keyboardLogService.GetLogs(_device.ID, 10).Length);
        Assert.AreEqual(0, _keyboardLogService.GetLogs(otherDevice.ID, 10).Length);
    }

    [TestMethod]
    [Description("Deleting a log removes it from the database.")]
    public async Task DeleteLog_RemovesTheLog()
    {
        var log = await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk("Window", "process", "text", false));

        Assert.IsNotNull(log);

        _keyboardLogService.DeleteLog(_device, log!.ID);

        Assert.AreEqual(0, _keyboardLogService.GetLogs(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("Clearing a device removes every log of that device.")]
    public async Task ClearLogs_RemovesEveryLogOfTheDevice()
    {
        await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk("Window", "process", "one", false));

        await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk("Window", "process", "two", false));

        Assert.AreEqual(2, _keyboardLogService.GetLogs(_device.ID, 10).Length);

        _keyboardLogService.ClearLogs(_device);

        Assert.AreEqual(0, _keyboardLogService.GetLogs(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("The retention limit of a device is applied to its logs.")]
    public async Task DeleteExpiredLogs_UsesDeviceRetentionCount()
    {
        _device.KeyboardRetentionCount = 1;

        await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk("Window", "process", "one", false));

        await _keyboardLogService.SaveLog(
            _device,
            new KeyboardLogChunk("Window", "process", "two", false));

        Assert.AreEqual(1, _keyboardLogService.GetLogs(_device.ID, 10).Length);
    }
}
