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

    /// <summary>
    /// Starts recording a video clip from one of the cameras of this device.
    /// The camera is selected by its device path, and an empty path selects the
    /// first camera of the device.  The quality is one of the values of
    /// <see cref="CameraQuality"/>, and the duration is in seconds.
    /// </summary>
    Task StartCameraCapture(
        string deviceId,
        string cameraId,
        int quality,
        int durationSeconds);

    /// <summary>
    /// Stops the camera recording that is currently running, if any.
    /// </summary>
    Task StopCameraCapture();

    /// <summary>
    /// Starts periodic camera recordings without a viewer attached. No clips
    /// are recorded during the pause windows.
    /// </summary>
    Task StartCameraMonitoring(
        string deviceId,
        CameraCaptureRequest request,
        int intervalSeconds,
        int[] pauseStartHours,
        int[] pauseEndHours);

    /// <summary>
    /// Stops periodic camera recordings.
    /// </summary>
    Task StopCameraMonitoring();

    Task ViewerDisconnected(string viewerId);
}
