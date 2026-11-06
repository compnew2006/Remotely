using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;
using System.IO;

namespace Remotely.Desktop.Win.Services;

/// <summary>
/// Records video clips from the cameras of this device with Media
/// Foundation, through ScreenRecorderLib.
/// </summary>
internal class CameraCapturerWin : ICameraCapturer
{
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
        try
        {
            return ScreenRecorderLib.Recorder
                .GetSystemVideoCaptureDevices()
                .Select(x => new CameraDeviceInfo(x.DeviceName, x.FriendlyName))
                .ToList();
        }
        catch (Exception)
        {
            // The cameras can't be listed, so the device is treated as if it
            // doesn't have one.
            return Array.Empty<CameraDeviceInfo>();
        }
    }

    public async Task<Result<string>> RecordAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cameras = ScreenRecorderLib.Recorder.GetSystemVideoCaptureDevices();
            if (cameras.Count == 0)
            {
                return Result.Fail<string>("This device does not have a camera that can be used.");
            }

            var camera = SelectCamera(cameras, request.CameraId);
            if (camera is null)
            {
                return Result.Fail<string>("The selected camera is not available on this device.");
            }

            var preset = CameraQualityPresets.GetPreset(request.Quality);
            var format = SelectFormat(
                ScreenRecorderLib.Recorder.GetSupportedVideoCaptureFormatsForDevice(camera.DeviceName),
                preset);

            var source = format is null ?
                new ScreenRecorderLib.VideoCaptureRecordingSource(camera.DeviceName) :
                new ScreenRecorderLib.VideoCaptureRecordingSource(camera.DeviceName, format);

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
                AudioOptions = new ScreenRecorderLib.AudioOptions
                {
                    IsAudioEnabled = false
                }
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
                        "The camera recording failed." :
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
                return Result.Fail<string>("The camera clip was not recorded.");
            }

            return Result.Ok(recordedPath);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<string>("The camera recording was canceled.");
        }
        catch (Exception ex)
        {
            return Result.Fail<string>($"The camera recording failed: {ex.Message}");
        }
    }

    private static ScreenRecorderLib.RecordableCamera? SelectCamera(
        IReadOnlyList<ScreenRecorderLib.RecordableCamera> cameras,
        string cameraId)
    {
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            return cameras.FirstOrDefault();
        }

        return cameras.FirstOrDefault(x =>
            string.Equals(x.DeviceName, cameraId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Picks the capture format that matches the quality preset the closest.
    /// The largest available format is used when the camera doesn't support
    /// the requested resolution.
    /// </summary>
    private static ScreenRecorderLib.VideoCaptureFormat? SelectFormat(
        IReadOnlyList<ScreenRecorderLib.VideoCaptureFormat> formats,
        CameraQualityPreset preset)
    {
        if (formats.Count == 0)
        {
            return null;
        }

        var exact = formats.FirstOrDefault(x =>
            Math.Abs(x.FrameSize.Width - preset.Width) < 1 &&
            Math.Abs(x.FrameSize.Height - preset.Height) < 1);

        if (exact is not null)
        {
            return exact;
        }

        // The format that is the closest to the requested resolution, but
        // never larger than it.
        var smaller = formats
            .Where(x => x.FrameSize.Width <= preset.Width && x.FrameSize.Height <= preset.Height)
            .OrderByDescending(x => x.FrameSize.Width * x.FrameSize.Height)
            .FirstOrDefault();

        return smaller ?? formats.OrderByDescending(x => x.FrameSize.Width * x.FrameSize.Height).First();
    }

#else

    /// <summary>
    /// The camera capture needs the native library of ScreenRecorderLib, which
    /// only supports the x86, x64, and ARM64 platforms.  This build doesn't
    /// have it, so no clip can be recorded.
    /// </summary>
    public IReadOnlyList<CameraDeviceInfo> GetCameras()
    {
        return Array.Empty<CameraDeviceInfo>();
    }

    public Task<Result<string>> RecordAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Fail<string>(
            "The camera capture is not available in this build.  " +
            "The desktop client must be built for the x64 or x86 platform."));
    }

#endif
}
