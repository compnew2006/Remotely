using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Services;

/// <summary>
/// Stores the audio clips that were recorded from remote devices, and enforces
/// the retention limits, so the disk can't fill up on the server.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// The default number of clips kept per device.
    /// </summary>
    int DefaultRetentionCount { get; }

    /// <summary>
    /// The default number of hours a clip is kept.
    /// </summary>
    int DefaultRetentionHours { get; }

    void AbortUpload(string clipId);

    void ClearClips(Device device);

    void DeleteClip(Device device, string fileName);

    Task DeleteExpiredClips(Device device);

    AudioClip[] GetClips(string deviceId, int maxCount);

    byte[]? GetClipContents(Device device, string fileName);

    string GetAudioDirectory(Device device);

    string? GetClipPath(Device device, string fileName);

    /// <summary>
    /// Starts a chunked upload of a clip.  The returned path is a temporary
    /// file that the chunks are written to.
    /// </summary>
    string? BeginUpload(Device device, string clipId);

    /// <summary>
    /// Appends a chunk to an upload that was started with
    /// <see cref="BeginUpload(Device, string)"/>.
    /// </summary>
    bool AppendChunk(string clipId, byte[] chunk);

    /// <summary>
    /// Finishes a chunked upload and stores the clip.
    /// </summary>
    Task<AudioClip?> CompleteUpload(
        Device device,
        string clipId,
        AudioQuality quality,
        int durationSeconds,
        AudioSource source);

    /// <summary>
    /// Stores a clip that was uploaded in one piece.
    /// </summary>
    Task<AudioClip?> SaveClip(
        Device device,
        byte[] contents,
        AudioQuality quality,
        int durationSeconds,
        AudioSource source);
}

public class AudioService : IAudioService
{
    /// <summary>
    /// The largest clip that is accepted from a device, in bytes.  A ten
    /// minute clip at the highest quality is about 53 MB, so this leaves a
    /// wide margin.
    /// </summary>
    public const long MaxClipBytes = 250 * 1024 * 1024;

    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<AudioService> _logger;
    private readonly IWebHostEnvironment _hostEnvironment;

    // The sessions are shared between the instances of this service, so the
    // service is registered as a singleton. A chunked upload spans several
    // hub invocations, and each of them gets a new scope: a per-instance
    // store would be empty in the scope that receives the next chunk, and
    // that chunk would be dropped silently.
    private static readonly Dictionary<string, UploadSession> _uploads = new();
    private static readonly object _uploadLock = new();

    public AudioService(
        IAppDbFactory appDbFactory,
        IWebHostEnvironment hostEnvironment,
        ILogger<AudioService> logger)
    {
        _appDbFactory = appDbFactory;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public int DefaultRetentionCount { get; } = 200;

    public int DefaultRetentionHours { get; } = 24;

    public string GetAudioDirectory(Device device)
    {
        if (!string.IsNullOrWhiteSpace(device.AudioDirectory))
        {
            return device.AudioDirectory;
        }

        return GetDefaultAudioDirectory(device);
    }

    private string GetDefaultAudioDirectory(Device device)
    {
        return Path.Combine(
            _hostEnvironment.ContentRootPath,
            "AppData",
            "Audio",
            device.ID);
    }

    private string GetUploadDirectory(Device device)
    {
        return Path.Combine(GetAudioDirectory(device), "uploads");
    }

    private int GetRetentionCount(Device device)
    {
        return device.AudioRetentionCount > 0 ?
            device.AudioRetentionCount :
            DefaultRetentionCount;
    }

    private int GetRetentionHours(Device device)
    {
        return device.AudioRetentionHours > 0 ?
            device.AudioRetentionHours :
            DefaultRetentionHours;
    }

    public AudioClip[] GetClips(string deviceId, int maxCount)
    {
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            return dbContext.AudioClips
                .Where(x => x.DeviceID == deviceId)
                .OrderByDescending(x => x.TimeStamp)
                .Take(maxCount)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting audio clips for device {deviceId}.", deviceId);
            return Array.Empty<AudioClip>();
        }
    }

    private AudioClip[] GetClipBatch(
        string deviceId,
        DateTimeOffset? cursorTimestamp,
        string? cursorId,
        int batchSize)
    {
        using var dbContext = _appDbFactory.GetContext();
        var query = dbContext.AudioClips
            .AsNoTracking()
            .Where(x => x.DeviceID == deviceId);

        if (cursorTimestamp.HasValue && cursorId is not null)
        {
            var lastTimestamp = cursorTimestamp.Value;
            var lastId = cursorId;
            query = query.Where(x =>
                x.TimeStamp < lastTimestamp ||
                (x.TimeStamp == lastTimestamp && string.Compare(x.ID, lastId) < 0));
        }

        return query
            .OrderByDescending(x => x.TimeStamp)
            .ThenByDescending(x => x.ID)
            .Take(batchSize)
            .ToArray();
    }

    /// <summary>
    /// Returns the path of a clip, or null when the file name is not a plain
    /// file name, or when the file does not exist.
    /// </summary>
    public string? GetClipPath(Device device, string fileName)
    {
        if (!IsSafeFileName(fileName))
        {
            return null;
        }

        string? path;
        using (var dbContext = _appDbFactory.GetContext())
        {
            var clip = dbContext.AudioClips
                .AsNoTracking()
                .FirstOrDefault(x => x.DeviceID == device.ID && x.FileName == fileName);

            path = clip is null ?
                Path.Combine(GetAudioDirectory(device), fileName) :
                GetClipPath(device, clip);
        }

        if (path is null || !File.Exists(path))
        {
            return null;
        }

        return path;
    }

    public byte[]? GetClipContents(Device device, string fileName)
    {
        var path = GetClipPath(device, fileName);
        if (path is null)
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reading an audio clip for device {deviceId}.", device.ID);
            return null;
        }
    }

    /// <summary>
    /// Returns true when the file name is a plain file name, so it can't be
    /// used to reach outside of the device's audio directory.
    /// </summary>
    private static bool IsSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar) ||
            fileName.Contains(".."))
        {
            return false;
        }

        return !fileName.Contains(Path.PathSeparator);
    }

    public string? BeginUpload(Device device, string clipId)
    {
        if (!Guid.TryParse(clipId, out _))
        {
            return null;
        }

        // The device id is used to build the path of the upload, so it must
        // not be able to reach outside of the audio directory.
        if (string.IsNullOrWhiteSpace(device.ID) ||
            device.ID.Contains(Path.DirectorySeparatorChar) ||
            device.ID.Contains(Path.AltDirectorySeparatorChar) ||
            device.ID.Contains(".."))
        {
            return null;
        }

        var directory = GetUploadDirectory(device);

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating the audio upload directory for device {deviceId}.", device.ID);
            return null;
        }

        var path = Path.Combine(directory, $"{clipId}.tmp");

        lock (_uploadLock)
        {
            _uploads[clipId] = new UploadSession(device.ID, path);
        }

        // Anything that is left over from an earlier attempt is discarded, so
        // the chunks of this upload are not appended to stale data.
        TryDeleteFile(path);

        return path;
    }

    public bool AppendChunk(string clipId, byte[] chunk)
    {
        if (chunk is null || chunk.Length == 0)
        {
            return false;
        }

        UploadSession? session;

        lock (_uploadLock)
        {
            if (!_uploads.TryGetValue(clipId, out session))
            {
                return false;
            }
        }

        try
        {
            using var stream = new FileStream(
                session.Path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None);

            stream.Write(chunk);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while writing an audio clip chunk.");
            return false;
        }
    }

    public void AbortUpload(string clipId)
    {
        lock (_uploadLock)
        {
            if (_uploads.Remove(clipId, out var session))
            {
                TryDeleteFile(session.Path);
            }
        }
    }

    public async Task<AudioClip?> CompleteUpload(
        Device device,
        string clipId,
        AudioQuality quality,
        int durationSeconds,
        AudioSource source)
    {
        UploadSession? session;

        lock (_uploadLock)
        {
            if (!_uploads.Remove(clipId, out session))
            {
                return null;
            }
        }

        try
        {
            if (!File.Exists(session.Path))
            {
                return null;
            }

            var size = new FileInfo(session.Path).Length;
            if (size > MaxClipBytes)
            {
                _logger.LogWarning(
                    "The audio clip from device {deviceId} is too large ({size} bytes) and was discarded.",
                    device.ID,
                    size);

                TryDeleteFile(session.Path);
                return null;
            }

            var directory = Path.GetFullPath(GetAudioDirectory(device));
            var fileName = BuildFileName();
            var destination = Path.Combine(directory, fileName);

            Directory.CreateDirectory(directory);
            File.Move(session.Path, destination, true);

            return await AddClipMetadata(
                device,
                directory,
                fileName,
                size,
                quality,
                durationSeconds,
                source);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while completing an audio clip upload for device {deviceId}.", device.ID);
            TryDeleteFile(session.Path);
            return null;
        }
    }

    public async Task<AudioClip?> SaveClip(
        Device device,
        byte[] contents,
        AudioQuality quality,
        int durationSeconds,
        AudioSource source)
    {
        if (contents is null || contents.Length == 0)
        {
            return null;
        }

        if (contents.Length > MaxClipBytes)
        {
            _logger.LogWarning(
                "The audio clip from device {deviceId} is too large ({size} bytes) and was discarded.",
                device.ID,
                contents.Length);

            return null;
        }

        string directory;
        var fileName = BuildFileName();

        try
        {
            directory = Path.GetFullPath(GetAudioDirectory(device));
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, fileName), contents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving an audio clip for device {deviceId}.", device.ID);
            return null;
        }

        return await AddClipMetadata(
            device,
            directory,
            fileName,
            contents.Length,
            quality,
            durationSeconds,
            source);
    }

    private async Task<AudioClip?> AddClipMetadata(
        Device device,
        string storageDirectory,
        string fileName,
        long size,
        AudioQuality quality,
        int durationSeconds,
        AudioSource source)
    {
        var clip = new AudioClip
        {
            ID = Guid.NewGuid().ToString(),
            DeviceID = device.ID,
            TimeStamp = DateTimeOffset.Now,
            FileName = fileName,
            StorageDirectory = storageDirectory,
            SizeBytes = size,
            Quality = AudioQualityPresets.GetPreset(quality).Quality.ToString(),
            DurationSeconds = durationSeconds,
            Source = source.ToString()
        };

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            await dbContext.AudioClips.AddAsync(clip);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving audio clip metadata for device {deviceId}.", device.ID);
        }

        await DeleteExpiredClips(device);

        return clip;
    }

    private static string BuildFileName()
    {
        return $"{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}.wav";
    }

    /// <summary>
    /// Deletes a single clip of a device.
    /// </summary>
    public void DeleteClip(Device device, string fileName)
    {
        if (!IsSafeFileName(fileName))
        {
            return;
        }

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var clip = dbContext.AudioClips
                .FirstOrDefault(x => x.DeviceID == device.ID && x.FileName == fileName);

            var path = clip is null ?
                Path.Combine(GetAudioDirectory(device), fileName) :
                GetClipPath(device, clip);

            if (path is null || !TryDeleteFile(path))
            {
                return;
            }

            if (clip is not null)
            {
                dbContext.AudioClips.Remove(clip);
                dbContext.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting audio clip {fileName}.", fileName);
        }
    }

    /// <summary>
    /// Removes every clip that was recorded from a device.
    /// </summary>
    public void ClearClips(Device device)
    {
        const int batchSize = 250;
        var removedCount = 0;
        DateTimeOffset? cursorTimestamp = null;
        string? cursorId = null;

        while (true)
        {
            var clips = GetClipBatch(
                device.ID,
                cursorTimestamp,
                cursorId,
                batchSize);

            if (clips.Length == 0)
            {
                break;
            }

            var lastClip = clips[^1];
            cursorTimestamp = lastClip.TimeStamp;
            cursorId = lastClip.ID;

            var removable = new List<AudioClip>();
            foreach (var clip in clips)
            {
                var path = GetClipPath(device, clip);
                if (path is null)
                {
                    _logger.LogWarning(
                        "Keeping audio clip metadata for {fileName}; its legacy path could not be resolved.",
                        clip.FileName);
                    continue;
                }

                if (TryDeleteFile(path))
                {
                    removable.Add(clip);
                }
            }

            if (removable.Count > 0)
            {
                try
                {
                    using var dbContext = _appDbFactory.GetContext();
                    dbContext.AudioClips.RemoveRange(removable);
                    dbContext.SaveChanges();
                    removedCount += removable.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while clearing audio clip metadata for device {deviceId}.", device.ID);
                    break;
                }
            }

            if (clips.Length < batchSize)
            {
                break;
            }
        }

        try
        {
            var directories = new HashSet<string>(GetPathComparer())
            {
                GetAudioDirectory(device),
                GetDefaultAudioDirectory(device)
            };

            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(directory, "*.wav"))
                {
                    TryDeleteFile(file);
                }

                var uploadDirectory = Path.Combine(directory, "uploads");
                if (Directory.Exists(uploadDirectory))
                {
                    foreach (var file in Directory.EnumerateFiles(uploadDirectory))
                    {
                        TryDeleteFile(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while clearing audio clips for device {deviceId}.", device.ID);
        }
    }

    public async Task DeleteExpiredClips(Device device)
    {
        const int batchSize = 250;

        try
        {
            var retentionCount = GetRetentionCount(device);
            var cutoff = DateTimeOffset.Now.AddHours(-GetRetentionHours(device));
            var sweepStartedAt = DateTimeOffset.Now;
            long processedCount = 0;
            DateTimeOffset? cursorTimestamp = null;
            string? cursorId = null;

            while (true)
            {
                var clips = GetClipBatch(
                    device.ID,
                    cursorTimestamp,
                    cursorId,
                    batchSize);

                if (clips.Length == 0)
                {
                    break;
                }

                var lastClip = clips[^1];
                cursorTimestamp = lastClip.TimeStamp;
                cursorId = lastClip.ID;

                var removable = new List<AudioClip>();
                foreach (var clip in clips)
                {
                    var overRetentionCount = processedCount >= retentionCount;
                    processedCount++;

                    var expiredByAge = clip.TimeStamp < cutoff;
                    if (!expiredByAge && !overRetentionCount)
                    {
                        continue;
                    }

                    if (overRetentionCount && clip.TimeStamp >= sweepStartedAt)
                    {
                        continue;
                    }

                    var path = GetClipPath(device, clip);
                    if (path is null)
                    {
                        _logger.LogWarning(
                            "Keeping expired audio clip metadata for {fileName}; its legacy path could not be resolved.",
                            clip.FileName);
                        continue;
                    }

                    if (TryDeleteFile(path))
                    {
                        removable.Add(clip);
                    }
                }

                if (removable.Count > 0)
                {
                    using var dbContext = _appDbFactory.GetContext();
                    dbContext.AudioClips.RemoveRange(removable);
                    await dbContext.SaveChangesAsync();
                }

                if (clips.Length < batchSize)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting expired audio clips for device {deviceId}.", device.ID);
        }
    }

    private string? GetClipPath(Device device, AudioClip clip)
    {
        if (!IsSafeFileName(clip.FileName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(clip.StorageDirectory))
        {
            return Path.Combine(clip.StorageDirectory, clip.FileName);
        }

        foreach (var directory in new[]
        {
            GetAudioDirectory(device),
            GetDefaultAudioDirectory(device)
        }.Distinct(GetPathComparer()))
        {
            try
            {
                var path = Path.Combine(directory, clip.FileName);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch
            {
                // Legacy rows are resolved only from folders whose paths can be checked.
            }
        }

        return null;
    }

    private bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            if (!File.Exists(path))
            {
                return true;
            }

            _logger.LogWarning("File remains after delete attempt: {path}.", path);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting the file {path}.", path);
            return false;
        }
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record UploadSession(string DeviceId, string Path);
}
