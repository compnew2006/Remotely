using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// The audio recorder is not available in this build, so nothing can be
/// recorded.
/// </summary>
internal sealed class UnavailableAudioRecorder : IAudioRecorder
{
    public IReadOnlyList<AudioDeviceInfo> GetMicrophones()
    {
        return Array.Empty<AudioDeviceInfo>();
    }

    public Task<Result<string>> RecordAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Fail<string>(
            "The audio recording is not available in this build."));
    }

    public void StartLive(AudioCaptureRequest request, Action<byte[]> onChunk)
    {
        // Nothing can be captured.
    }

    public void StopLive()
    {
        // Nothing has to be stopped.
    }
}
