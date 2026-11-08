using Remotely.Desktop.Native.Windows;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Win.Helpers;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;
using System.IO;

namespace Remotely.Desktop.Win.Services;

/// <summary>
/// Records video clips from the cameras or the displays of this device with
/// Media Foundation, through ScreenRecorderLib.  The audio of the recording
/// comes from the system audio or from a microphone.
/// </summary>
internal class CameraCapturerWin : ICameraCapturer
{
    private readonly ILogger<CameraCapturerWin> _logger;

    public CameraCapturerWin(ILogger<CameraCapturerWin> logger)
    {
        _logger = logger;
    }

#if SCREEN_RECORDER_LIB

    /// <summary>
    /// The folder that the recorded clips are written to.  The files are
    /// deleted after they are sent to the server.
    /// </summary>
    private static readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "Remotely",
        "Camera");

    public IReadOnlyList<CameraDeviceInfo> GetCameras()
    {
        var result = new List<CameraDeviceInfo>();

        // The enumeration runs on a pool thread that has no desktop of its
        // own, so EnumDisplayMonitors finds no screens. Switch to the
        // input desktop first, exactly like the screen streamer does.
        try
        {
            var switched = Win32Interop.SwitchToInputDesktop();
            _logger.LogInformation(
                "The camera enumeration switched to the input desktop: {switched}.",
                switched);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "The input desktop switch failed before the camera enumeration.");
        }

        try
        {
            result.AddRange(ScreenRecorderLib.Recorder
                .GetSystemVideoCaptureDevices()
                .Select(x => new CameraDeviceInfo(
                    x.DeviceName,
                    x.FriendlyName,
                    CameraDeviceKind.Camera)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The cameras could not be listed.");
        }

        try
        {
            var index = 1;

            foreach (var display in DisplaysEnumerationHelper.GetDisplays())
            {
                var size = $"{display.ScreenSize.X}x{display.ScreenSize.Y}";

                result.Add(new CameraDeviceInfo(
                    display.DeviceName,
                    $"Screen {index} ({size}){(display.IsPrimary ? " - primary" : "")}",
                    CameraDeviceKind.Display));

                index++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The displays could not be listed.");
        }

        _logger.LogInformation(
            "The camera enumeration found {count} cameras and screens.",
            result.Count);

        return result;
    }

    public IReadOnlyList<CameraAudioDeviceInfo> GetAudioDevices()
    {
        var result = new List<CameraAudioDeviceInfo>();

        try
        {
            Win32Interop.SwitchToInputDesktop();
        }
        catch (Exception)
        {
            // The audio devices are listed anyway; they are listed when
            // the switch works.
        }

        // The recording library's native audio enumeration crashes the
        // whole process (access violation) when the endpoint list it
        // enumerates is empty (no microphone, or no speakers). Check each
        // direction with NAudio first and never call the library for a
        // direction the device does not have.
        var endpoints = GetActiveAudioEndpoints();
        if (!endpoints.Captures && !endpoints.Renders)
        {
            _logger.LogInformation(
                "This device has no active audio endpoints, so the audio list is empty.");
            return result;
        }

        if (endpoints.Renders)
        {
            try
            {
                result.AddRange(ScreenRecorderLib.Recorder
                    .GetSystemAudioLoopbackDevices()
                    .Select(x => new CameraAudioDeviceInfo(
                        x.DeviceName,
                        x.FriendlyName,
                        CameraAudioSource.SystemAudio,
                        x.IsDefaultDevice)));
            }
            catch (Exception)
            {
                // The system audio devices can't be listed.
            }
        }

        if (endpoints.Captures)
        {
            try
            {
                result.AddRange(ScreenRecorderLib.Recorder
                    .GetSystemAudioCaptureDevices()
                    .Select(x => new CameraAudioDeviceInfo(
                        x.DeviceName,
                        x.FriendlyName,
                        CameraAudioSource.Microphone,
                        x.IsDefaultDevice)));
            }
            catch (Exception)
            {
                // The microphones can't be listed.
            }
        }

        _logger.LogInformation(
            "The audio enumeration found {count} audio devices.",
            result.Count);

        return result;
    }

    /// <summary>
    /// Whether the device has an active capture (microphone) endpoint and an
    /// active render (speaker) endpoint. The recording library crashes the
    /// process when it enumerates an audio direction that has no endpoint,
    /// so callers must not ask it for a direction the device does not have.
    /// </summary>
    private static (bool Captures, bool Renders) GetActiveAudioEndpoints()
    {
        try
        {
            using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();

            var captures =
                enumerator.EnumerateAudioEndPoints(
                    NAudio.CoreAudioApi.DataFlow.Capture,
                    NAudio.CoreAudioApi.DeviceState.Active);
            var renders =
                enumerator.EnumerateAudioEndPoints(
                    NAudio.CoreAudioApi.DataFlow.Render,
                    NAudio.CoreAudioApi.DeviceState.Active);

            return (captures.Count > 0, renders.Count > 0);
        }
        catch (Exception)
        {
            return (false, false);
        }
    }

    public async Task<Result<string>> RecordAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Recording a display needs a thread that is on the input
            // desktop, exactly like the screen streamer. GetCameras()
            // already switched, but this is again the entry point when
            // the encoding starts.
            try
            {
                Win32Interop.SwitchToInputDesktop();
            }
            catch (Exception)
            {
                // The recording is attempted anyway.
            }

            var cameras = GetCameras();
            if (cameras.Count == 0)
            {
                return Result.Fail<string>("This device does not have a camera or a screen that can be recorded.");
            }

            var source = SelectSource(cameras, request);
            if (source is null)
            {
                return Result.Fail<string>("The selected source is not available on this device.");
            }

            var preset = CameraQualityPresets.GetPreset(request.Quality);

            var options = new ScreenRecorderLib.RecorderOptions
            {
                SourceOptions = new ScreenRecorderLib.SourceOptions
                {
                    RecordingSources = new List<ScreenRecorderLib.RecordingSourceBase> { source }
                },
                VideoEncoderOptions = new ScreenRecorderLib.VideoEncoderOptions
                {
                    Framerate = preset.Framerate,
                    Bitrate = preset.Bitrate,
                    Quality = 70,
                    IsMp4FastStartEnabled = true
                },
                AudioOptions = BuildAudioOptions(request)
            };

            Directory.CreateDirectory(_tempDirectory);

            var path = Path.Combine(_tempDirectory, $"camera-{Guid.NewGuid():N}.mp4");
            var completion = new TaskCompletionSource<Result<string>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using var recorder = ScreenRecorderLib.Recorder.CreateRecorder(options);

            recorder.OnRecordingComplete += (sender, args) =>
            {
                completion.TrySetResult(Result.Ok(args.FilePath ?? path));
            };

            recorder.OnRecordingFailed += (sender, args) =>
            {
                completion.TrySetResult(Result.Fail<string>(
                    string.IsNullOrWhiteSpace(args.Error) ?
                        "The recording failed." :
                        args.Error));
            };

            recorder.Record(path);

            var durationSeconds = CameraQualityPresets.ClampDurationSeconds(request.DurationSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The recording is stopped below, so the file is written
                // properly.
            }

            try
            {
                recorder.Stop();
            }
            catch (Exception)
            {
                // The recorder may have already stopped on its own.
            }

            var result = await completion.Task;

            if (!result.IsSuccess)
            {
                return result;
            }

            var recordedPath = result.Value;
            if (string.IsNullOrWhiteSpace(recordedPath) || !File.Exists(recordedPath))
            {
                return Result.Fail<string>("The recording was not made.");
            }

            return Result.Ok(recordedPath);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<string>("The recording was canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while recording a video clip.");
            return Result.Fail<string>($"The recording failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the source of the video, which is either a camera or a display.
    /// </summary>
    private static ScreenRecorderLib.RecordingSourceBase? SelectSource(
        IReadOnlyList<CameraDeviceInfo> sources,
        CameraCaptureRequest request)
    {
        CameraDeviceInfo? source = null;

        if (!string.IsNullOrWhiteSpace(request.CameraId))
        {
            source = sources.FirstOrDefault(x =>
                string.Equals(x.Id, request.CameraId, StringComparison.OrdinalIgnoreCase) &&
                x.Kind == request.VideoSource);
        }

        source ??= sources.FirstOrDefault(x => x.Kind == request.VideoSource);
        source ??= sources.FirstOrDefault();

        if (source is null)
        {
            return null;
        }

        if (source.Kind == CameraDeviceKind.Display)
        {
            try
            {
                return new ScreenRecorderLib.DisplayRecordingSource(source.Id);
            }
            catch (Exception)
            {
                // The display can't be recorded by its device name, so the
                // primary display is used instead.
                return ScreenRecorderLib.DisplayRecordingSource.MainMonitor;
            }
        }

        return new ScreenRecorderLib.VideoCaptureRecordingSource(source.Id);
    }

    /// <summary>
    /// Builds the audio options of the recording.  The audio comes from the
    /// system audio or from a microphone, and it can be left out entirely.
    /// </summary>
    private ScreenRecorderLib.AudioOptions BuildAudioOptions(CameraCaptureRequest request)
    {
        var options = new ScreenRecorderLib.AudioOptions
        {
            IsAudioEnabled = false
        };

        if (request.AudioSource == CameraAudioSource.None)
        {
            return options;
        }

        ScreenRecorderLib.AudioSourceBase? source = null;

        try
        {
            if (request.AudioSource == CameraAudioSource.SystemAudio)
            {
                var device = SelectAudioDevice(request.AudioDeviceId, CameraAudioSource.SystemAudio);

                source = device is null ?
                    ScreenRecorderLib.LoopbackAudioSource.Default :
                    new ScreenRecorderLib.LoopbackAudioSource(device.Id);
            }
            else
            {
                var device = SelectAudioDevice(request.AudioDeviceId, CameraAudioSource.Microphone);

                source = device is null ?
                    ScreenRecorderLib.CaptureAudioSource.Default :
                    new ScreenRecorderLib.CaptureAudioSource(device.Id)
                    {
                        ForceMono = true
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The audio source could not be prepared, so the recording has no audio.");
            return options;
        }

        if (source is null)
        {
            return options;
        }

        options.IsAudioEnabled = true;
        options.AudioSources.Add(source);

        return options;
    }

    /// <summary>
    /// Finds the audio device that was picked, or the default device of its
    /// kind when the picked one is not available anymore.
    /// </summary>
    private CameraAudioDeviceInfo? SelectAudioDevice(string audioDeviceId, CameraAudioSource source)
    {
        var devices = GetAudioDevices()
            .Where(x => x.Source == source)
            .ToList();

        if (devices.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(audioDeviceId))
        {
            var selected = devices.FirstOrDefault(x =>
                string.Equals(x.Id, audioDeviceId, StringComparison.OrdinalIgnoreCase));

            if (selected is not null)
            {
                return selected;
            }
        }

        return devices.FirstOrDefault(x => x.IsDefault) ?? devices[0];
    }

#else

    /// <summary>
    /// The recording needs the native library of ScreenRecorderLib, which
    /// only supports the x86, x64, and ARM64 platforms.  This build doesn't
    /// have it, so no clip can be recorded.
    /// </summary>
    public IReadOnlyList<CameraDeviceInfo> GetCameras()
    {
        return Array.Empty<CameraDeviceInfo>();
    }

    public IReadOnlyList<CameraAudioDeviceInfo> GetAudioDevices()
    {
        return Array.Empty<CameraAudioDeviceInfo>();
    }

    public Task<Result<string>> RecordAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Fail<string>(
            "The recording is not available in this build.  " +
            "The desktop client must be built for the x64 or x86 platform."));
    }

#endif
}
