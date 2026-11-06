using System.Text.Json;
using Remotely.Shared.Models;

namespace Remotely.Shared.Utilities;

/// <summary>
/// The windows during which screenshots are not taken.  A device can have
/// more than one of them, and they can wrap around midnight (for example,
/// 22:00 to 07:00).
/// </summary>
public static class ScreenshotPause
{
    /// <summary>
    /// Returns true when the given hour falls inside the pause window.  The
    /// window is disabled when both hours are the same.
    /// </summary>
    public static bool IsPaused(int startHour, int endHour, DateTimeOffset now)
    {
        if (startHour == endHour)
        {
            return false;
        }

        var hour = now.Hour;

        if (startHour < endHour)
        {
            return hour >= startHour && hour < endHour;
        }

        // The window wraps around midnight.
        return hour >= startHour || hour < endHour;
    }

    /// <summary>
    /// Returns true when the given hour falls inside any of the pause windows.
    /// </summary>
    public static bool IsPaused(
        IReadOnlyList<ScreenshotPauseWindow> windows,
        DateTimeOffset now)
    {
        foreach (var window in windows)
        {
            if (IsPaused(window.StartHour, window.EndHour, now))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Serializes the pause windows for storage on the device.
    /// </summary>
    public static string Serialize(IEnumerable<ScreenshotPauseWindow> windows)
    {
        return JsonSerializer.Serialize(windows.ToList());
    }

    /// <summary>
    /// Reads the pause windows that were stored for a device.  Anything that
    /// cannot be read is ignored, so a bad value never stops the screenshots.
    /// </summary>
    public static List<ScreenshotPauseWindow> Deserialize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<ScreenshotPauseWindow>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ScreenshotPauseWindow>>(value)
                ?? new List<ScreenshotPauseWindow>();
        }
        catch (JsonException)
        {
            return new List<ScreenshotPauseWindow>();
        }
    }
}
