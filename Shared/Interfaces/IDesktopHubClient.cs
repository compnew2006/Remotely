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

    /// <summary>
    /// Starts recording an audio clip from the system audio or the microphone
    /// of this device.  A duration of zero only asks for the list of
    /// microphones.  The quality is one of the values of
    /// <see cref="AudioQuality"/>, and the duration is in seconds.
    /// </summary>
    Task StartAudioCapture(
        string deviceId,
        AudioSource source,
        string microphoneId,
        int quality,
        int durationSeconds);

    /// <summary>
    /// Stops the audio recording that is currently running, if any.
    /// </summary>
    Task StopAudioCapture();

    /// <summary>
    /// Starts periodic audio recordings without a viewer attached.  No clips
    /// are recorded during the pause windows.
    /// </summary>
    Task StartAudioMonitoring(
        string deviceId,
        AudioCaptureRequest request,
        int intervalSeconds,
        int[] pauseStartHours,
        int[] pauseEndHours);

    /// <summary>
    /// Stops periodic audio recordings.
    /// </summary>
    Task StopAudioMonitoring();

    /// <summary>
    /// Starts streaming the audio of this device to the server in real time.
    /// </summary>
    Task StartAudioLive(
        string deviceId,
        AudioSource source,
        string microphoneId);

    /// <summary>
    /// Stops the live audio stream.
    /// </summary>
    Task StopAudioLive();

    Task ViewerDisconnected(string viewerId);
}
