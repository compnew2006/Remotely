using Remotely.Shared.Models;
using Remotely.Shared.Primitives;

namespace Remotely.Desktop.Shared.Abstractions;

/// <summary>
/// Records audio from this device, either from the system audio or from the
/// microphone, and streams it while it is captured.
/// </summary>
public interface IAudioRecorder
{
    /// <summary>
    /// The microphones of this device, so an administrator can pick one.
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetMicrophones();

    /// <summary>
    /// Records a clip and returns the path of the file that was written.
    /// </summary>
    Task<Result<string>> RecordAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts streaming the audio of this device.  Every chunk that is passed
    /// to the callback is a complete, short WAV frame.
    /// </summary>
    void StartLive(AudioCaptureRequest request, Action<byte[]> onChunk);

    /// <summary>
    /// Stops the live stream.
    /// </summary>
    void StopLive();
}
