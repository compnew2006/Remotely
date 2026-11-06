using Remotely.Shared.Enums;
using Remotely.Shared.Models;

namespace Remotely.Shared.Interfaces;

public interface IDesktopHubClient
{
    Task Disconnect(string reason);

    Task GetScreenCast(
        string viewerId,
        string requesterName,
        bool notifyUser,
        Guid streamId,
        bool viewOnly);

    Task<PromptForAccessResult> PromptForAccess(RemoteControlAccessRequest accessRequest);

    Task RequestScreenCast(
        string viewerId,
        string requesterName,
        bool notifyUser,
        Guid streamId,
        bool viewOnly);

    Task SendDtoToClient(byte[] dtoWrapper, string viewerConnectionId);

    /// <summary>
    /// Starts capturing full screenshots of the desktop at the given interval
    /// and sending them to the server, with no viewer attached.  No screenshots
    /// are taken during the pause windows.
    /// </summary>
    Task StartMonitoring(int intervalSeconds, int[] pauseStartHours, int[] pauseEndHours);

    /// <summary>
    /// Stops capturing periodic screenshots of the desktop.
    /// </summary>
    Task StopMonitoring();

    /// <summary>
    /// Captures a single screenshot of the desktop and sends it to the server
    /// right away, without waiting for the monitoring interval.
    /// </summary>
    Task CaptureScreenshotNow();

    Task ViewerDisconnected(string viewerId);
}
