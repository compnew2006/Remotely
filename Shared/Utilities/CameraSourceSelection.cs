using Remotely.Shared.Models;

namespace Remotely.Shared.Utilities;

/// <summary>
/// The video and the audio sources of a recording are picked in the browser
/// with a selector, so they are carried as one string that holds the kind of
/// the source and its device name.
/// </summary>
public static class CameraSourceSelection
{
    private const char Separator = '|';

    /// <summary>
    /// Builds the value of a video source.
    /// </summary>
    public static string GetSourceValue(CameraDeviceInfo source)
    {
        return $"{source.Kind}{Separator}{source.Id}";
    }

    /// <summary>
    /// Reads the video source back from its value.  Anything that cannot be
    /// read falls back to a camera, so a bad value never stops a recording.
    /// </summary>
    public static (CameraDeviceKind Kind, string Id) ParseSource(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (CameraDeviceKind.Camera, string.Empty);
        }

        var index = value.IndexOf(Separator);
        if (index < 0)
        {
            return (CameraDeviceKind.Camera, value);
        }

        var kind = Enum.TryParse<CameraDeviceKind>(value[..index], out var parsed) &&
            Enum.IsDefined(parsed) ?
            parsed :
            CameraDeviceKind.Camera;

        return (kind, value[(index + 1)..]);
    }

    /// <summary>
    /// Builds the value of an audio source.  A null device means that the
    /// recording has no audio at all.
    /// </summary>
    public static string GetAudioValue(CameraAudioDeviceInfo? device)
    {
        return device is null ?
            $"{CameraAudioSource.None}{Separator}" :
            $"{device.Source}{Separator}{device.Id}";
    }

    /// <summary>
    /// Reads the audio source back from its value.
    /// </summary>
    public static (CameraAudioSource Source, string Id) ParseAudio(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (CameraAudioSource.None, string.Empty);
        }

        var index = value.IndexOf(Separator);
        if (index < 0)
        {
            return (CameraAudioSource.None, string.Empty);
        }

        var source = Enum.TryParse<CameraAudioSource>(value[..index], out var parsed) &&
            Enum.IsDefined(parsed) ?
            parsed :
            CameraAudioSource.None;

        return (source, value[(index + 1)..]);
    }
}
