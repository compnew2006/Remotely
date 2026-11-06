namespace Remotely.Shared.Models;

/// <summary>
/// A window of the day during which screenshots are not taken.  It can wrap
/// around midnight (for example, 22:00 to 07:00).
/// </summary>
public class ScreenshotPauseWindow
{
    public ScreenshotPauseWindow()
    {
    }

    public ScreenshotPauseWindow(int startHour, int endHour)
    {
        StartHour = startHour;
        EndHour = endHour;
    }

    public int StartHour { get; set; }

    public int EndHour { get; set; }
}
