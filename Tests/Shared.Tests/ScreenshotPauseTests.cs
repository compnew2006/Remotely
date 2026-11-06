using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Shared.Tests;

/// <summary>
/// The pause windows decide when a device must not be captured.  A window can
/// wrap around midnight, more than one window can be configured for a device,
/// and a window with the same start and end hour is disabled.
/// </summary>
[TestClass]
public class ScreenshotPauseTests
{
    private static DateTimeOffset At(int hour) => new(2026, 11, 6, hour, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void IsPaused_SameStartAndEndHour_IsDisabled()
    {
        Assert.IsFalse(ScreenshotPause.IsPaused(3, 3, At(3)));
        Assert.IsFalse(ScreenshotPause.IsPaused(0, 0, At(23)));
    }

    [TestMethod]
    public void IsPaused_InsideTheWindow_ReturnsTrue()
    {
        Assert.IsTrue(ScreenshotPause.IsPaused(9, 17, At(9)));
        Assert.IsTrue(ScreenshotPause.IsPaused(9, 17, At(16)));
    }

    [TestMethod]
    public void IsPaused_OutsideTheWindow_ReturnsFalse()
    {
        Assert.IsFalse(ScreenshotPause.IsPaused(9, 17, At(8)));
        Assert.IsFalse(ScreenshotPause.IsPaused(9, 17, At(17)));
    }

    [TestMethod]
    public void IsPaused_WindowThatWrapsAroundMidnight_ReturnsTrue()
    {
        Assert.IsTrue(ScreenshotPause.IsPaused(22, 7, At(23)));
        Assert.IsTrue(ScreenshotPause.IsPaused(22, 7, At(0)));
        Assert.IsTrue(ScreenshotPause.IsPaused(22, 7, At(6)));
        Assert.IsFalse(ScreenshotPause.IsPaused(22, 7, At(7)));
        Assert.IsFalse(ScreenshotPause.IsPaused(22, 7, At(12)));
    }

    [TestMethod]
    public void IsPaused_MultipleWindows_ReturnsTrueForAnyOfThem()
    {
        var windows = new[]
        {
            new ScreenshotPauseWindow(13, 14),
            new ScreenshotPauseWindow(22, 7)
        };

        Assert.IsTrue(ScreenshotPause.IsPaused(windows, At(13)));
        Assert.IsTrue(ScreenshotPause.IsPaused(windows, At(23)));
        Assert.IsFalse(ScreenshotPause.IsPaused(windows, At(12)));
    }

    [TestMethod]
    public void IsPaused_NoWindows_ReturnsFalse()
    {
        Assert.IsFalse(ScreenshotPause.IsPaused(Array.Empty<ScreenshotPauseWindow>(), At(3)));
    }

    [TestMethod]
    public void SerializeAndDeserialize_KeepsEveryWindow()
    {
        var windows = new[]
        {
            new ScreenshotPauseWindow(13, 14),
            new ScreenshotPauseWindow(22, 7)
        };

        var json = ScreenshotPause.Serialize(windows);
        var restored = ScreenshotPause.Deserialize(json);

        Assert.AreEqual(2, restored.Count);
        Assert.AreEqual(13, restored[0].StartHour);
        Assert.AreEqual(14, restored[0].EndHour);
        Assert.AreEqual(22, restored[1].StartHour);
        Assert.AreEqual(7, restored[1].EndHour);
    }

    [TestMethod]
    public void Deserialize_EmptyOrInvalidValue_ReturnsNoWindows()
    {
        Assert.AreEqual(0, ScreenshotPause.Deserialize(null).Count);
        Assert.AreEqual(0, ScreenshotPause.Deserialize("   ").Count);
        Assert.AreEqual(0, ScreenshotPause.Deserialize("not json").Count);
    }
}
