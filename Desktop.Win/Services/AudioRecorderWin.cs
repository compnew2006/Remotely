using NAudio.CoreAudioApi;
using NAudio.Wave;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;
using System.IO;

namespace Remotely.Desktop.Win.Services;

/// <summary>
/// Records audio from this device with NAudio.  The system audio is captured
/// with a loopback device, and the microphone is captured directly.  Every
/// recording is written as an uncompressed WAV file, so the browser can play
/// it without an extra codec.
/// </summary>
internal sealed class AudioRecorderWin : IAudioRecorder
{
    /// <summary>
    /// The folder that the recorded clips are written to.  The files are
    /// deleted after they are sent to the server.
    /// </summary>
    private static readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "Remotely",
        "Audio");

    /// <summary>
    /// How long one live chunk is, in seconds.  It is short enough to feel
    /// live, and short enough to keep every message small.
    /// </summary>
    private const double LiveChunkSeconds = 0.25;

    private readonly ILogger<AudioRecorderWin> _logger;
    private readonly object _liveSync = new();

    private WasapiCapture? _liveCapture;
    private MediaFoundationResampler? _liveResampler;
    private CancellationTokenSource? _liveCancellation;
    private Task? _liveTask;

    public AudioRecorderWin(ILogger<AudioRecorderWin> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<AudioDeviceInfo> GetMicrophones()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(x => new AudioDeviceInfo(x.ID, x.FriendlyName))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while listing the microphones of this device.");
            return Array.Empty<AudioDeviceInfo>();
        }
    }

    public async Task<Result<string>> RecordAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken)
    {
        WasapiCapture? capture = null;

        try
        {
            var preset = AudioQualityPresets.GetPreset(request.Quality);
            var targetFormat = new WaveFormat(preset.SampleRate, preset.BitsPerSample, preset.Channels);
            var durationSeconds = AudioQualityPresets.ClampDurationSeconds(request.DurationSeconds);

            capture = CreateCapture(request);

            if (capture is null)
            {
                return Result.Fail<string>(
                    request.Source == AudioSource.Microphone ?
                        "This device does not have a microphone that can be used." :
                        "The system audio of this device cannot be captured.");
            }

            Directory.CreateDirectory(_tempDirectory);

            var rawPath = Path.Combine(_tempDirectory, $"audio-{Guid.NewGuid():N}-raw.wav");

            using (var writer = new WaveFileWriter(rawPath, capture.WaveFormat))
            {
                capture.DataAvailable += (_, args) =>
                {
                    try
                    {
                        writer.Write(args.Buffer, 0, args.BytesRecorded);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error while writing an audio sample.");
                    }
                };

                capture.StartRecording();

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
                    capture.StopRecording();
                }
                catch (Exception)
                {
                    // The capture may have already stopped on its own.
                }

                // The writer is disposed here, so the header of the file is
                // complete.
                await Task.Delay(100, CancellationToken.None);
            }

            if (!File.Exists(rawPath) || new FileInfo(rawPath).Length <= 44)
            {
                TryDeleteFile(rawPath);
                return Result.Fail<string>("The audio clip was not recorded.");
            }

            var path = Transcode(rawPath, targetFormat);

            return Result.Ok(path);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<string>("The audio recording was canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while recording an audio clip.");
            return Result.Fail<string>($"The audio recording failed: {ex.Message}");
        }
        finally
        {
            capture?.Dispose();
        }
    }

    public void StartLive(AudioCaptureRequest request, Action<byte[]> onChunk)
    {
        StopLive();

        WasapiCapture? capture = null;

        try
        {
            capture = CreateCapture(request);

            if (capture is null)
            {
                _logger.LogWarning(
                    "The live audio stream could not be started, because the audio source is not available.");
                return;
            }

            var preset = AudioQualityPresets.GetPreset(request.Quality);
            var targetFormat = new WaveFormat(preset.SampleRate, preset.BitsPerSample, preset.Channels);

            var buffered = new BufferedWaveProvider(capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };

            capture.DataAvailable += (_, args) =>
            {
                try
                {
                    buffered.AddSamples(args.Buffer, 0, args.BytesRecorded);
                }
                catch (Exception)
                {
                    // The buffer is full, so the oldest samples are dropped.
                }
            };

            MediaFoundationResampler? resampler = null;

            try
            {
                resampler = new MediaFoundationResampler(buffered, targetFormat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "The audio could not be resampled, so the live stream uses the format of the device.");
            }

            var cancellationTokenSource = new CancellationTokenSource();

            // When the audio cannot be resampled, the frames carry the format
            // of the device, so the header of every frame has to describe
            // that format instead of the requested one.
            var frameFormat = resampler is not null ? targetFormat : capture.WaveFormat;
            var chunkSize = (int)(frameFormat.AverageBytesPerSecond * LiveChunkSeconds);
            var chunk = new List<byte>(chunkSize);

            capture.StartRecording();

            var task = Task.Run(async () =>
            {
                var token = cancellationTokenSource.Token;
                var buffer = new byte[frameFormat.AverageBytesPerSecond / 4];

                while (!token.IsCancellationRequested)
                {
                    var read = 0;

                    try
                    {
                        if (resampler is not null)
                        {
                            read = resampler.Read(buffer, 0, buffer.Length);
                        }
                        else if (buffered.Read(buffer, 0, buffer.Length) > 0)
                        {
                            read = buffer.Length;
                        }
                    }
                    catch (Exception)
                    {
                        // There is no data available, so the loop waits.
                        read = 0;
                    }

                    if (read > 0)
                    {
                        chunk.AddRange(buffer.Take(read));

                        if (chunk.Count >= chunkSize)
                        {
                            var frame = BuildWavFrame(chunk.ToArray(), frameFormat);
                            chunk.Clear();

                            try
                            {
                                onChunk(frame);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error while sending a live audio chunk.");
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(20, token);
                    }
                }
            });

            lock (_liveSync)
            {
                _liveCapture = capture;
                _liveResampler = resampler;
                _liveCancellation = cancellationTokenSource;
                _liveTask = task;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting the live audio stream.");
            capture?.Dispose();
        }
    }

    public void StopLive()
    {
        WasapiCapture? capture;
        MediaFoundationResampler? resampler;
        CancellationTokenSource? cancellationTokenSource;
        Task? task;

        lock (_liveSync)
        {
            capture = _liveCapture;
            resampler = _liveResampler;
            cancellationTokenSource = _liveCancellation;
            task = _liveTask;

            _liveCapture = null;
            _liveResampler = null;
            _liveCancellation = null;
            _liveTask = null;
        }

        try
        {
            cancellationTokenSource?.Cancel();
        }
        catch
        {
            // The stream was already stopped.
        }

        try
        {
            capture?.StopRecording();
        }
        catch
        {
            // The capture may have already stopped on its own.
        }

        try
        {
            capture?.Dispose();
        }
        catch
        {
            // Nothing can be done about it.
        }

        try
        {
            resampler?.Dispose();
        }
        catch
        {
            // Nothing can be done about it.
        }

        if (task is not null)
        {
            try
            {
                task.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // The task was canceled.
            }
        }

        cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// Creates the capture that matches the requested source.
    /// </summary>
    private WasapiCapture? CreateCapture(AudioCaptureRequest request)
    {
        try
        {
            if (request.Source == AudioSource.SystemAudio)
            {
                return new WasapiLoopbackCapture();
            }

            using var enumerator = new MMDeviceEnumerator();

            MMDevice? device = null;

            if (!string.IsNullOrWhiteSpace(request.MicrophoneId))
            {
                try
                {
                    device = enumerator.GetDevice(request.MicrophoneId);
                }
                catch (Exception)
                {
                    // The microphone is not available anymore.
                    device = null;
                }
            }

            device ??= enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .FirstOrDefault();

            if (device is null)
            {
                return null;
            }

            return new WasapiCapture(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating the audio capture.");
            return null;
        }
    }

    /// <summary>
    /// Converts the recorded file to the requested sample format.  The
    /// original file is returned when the conversion fails, so a recording is
    /// never lost.
    /// </summary>
    private string Transcode(string rawPath, WaveFormat targetFormat)
    {
        var transcodedPath = Path.Combine(_tempDirectory, $"audio-{Guid.NewGuid():N}.wav");

        try
        {
            using var reader = new WaveFileReader(rawPath);
            using var resampler = new MediaFoundationResampler(reader, targetFormat);
            WaveFileWriter.CreateWaveFile(transcodedPath, resampler);

            if (!File.Exists(transcodedPath) || new FileInfo(transcodedPath).Length <= 44)
            {
                TryDeleteFile(transcodedPath);
                return rawPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The audio clip could not be converted, so the original is used.");
            TryDeleteFile(transcodedPath);
            return rawPath;
        }

        TryDeleteFile(rawPath);

        return transcodedPath;
    }

    /// <summary>
    /// Wraps raw samples in a WAV header, so a chunk can be played on its
    /// own.
    /// </summary>
    private static byte[] BuildWavFrame(byte[] pcm, WaveFormat format)
    {
        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;
        var blockAlign = format.Channels * (format.BitsPerSample / 8);
        var byteRate = format.SampleRate * blockAlign;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        writer.Write(36 + pcm.Length);
        writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
        writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        writer.Write(16);
        writer.Write((short)(isFloat ? 3 : 1));
        writer.Write((short)format.Channels);
        writer.Write(format.SampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)format.BitsPerSample);
        writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        writer.Write(pcm.Length);
        writer.Write(pcm);
        writer.Flush();

        return stream.ToArray();
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting the file {path}.", path);
        }
    }
}
