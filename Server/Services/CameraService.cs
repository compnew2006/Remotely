using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Server.Models;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Services;

/// <summary>
/// Stores the video clips that were recorded from the cameras of remote
/// devices, and enforces the retention limits, so the disk can't fill up on
/// the server.
/// </summary>
public interface ICameraService
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

    CameraClip[] GetClips(string deviceId, int maxCount);

    byte[]? GetClipContents(Device device, string fileName);

    string GetCameraDirectory(Device device);

    string? GetClipPath(Device device, string fileName);

    /// <summary>
    /// Starts a chunked upload of a clip.  The returned path is a temporary
    /// file that the chunks are written to.
    /// </summary>
    string? BeginUpload(Device device, string clipId);

    /// <summary>
    /// Appends a chunk to an upload that was started with
    /// <see cref="BeginUpload(string, string)"/>.
    /// </summary>
    bool AppendChunk(string clipId, byte[] chunk);

    /// <summary>
    /// Finishes a chunked upload and stores the clip.
    /// </summary>
    Task<CameraClip?> CompleteUpload(
        Device device,
        string clipId,
        string quality,
        int durationSeconds,
        string cameraName);

    /// <summary>
    /// Stores a clip that was uploaded in one piece.
    /// </summary>
    Task<CameraClip?> SaveClip(
        Device device,
        byte[] contents,
        string quality,
        int durationSeconds,
        string cameraName);

    /// <summary>
    /// Stores a clip from a file that was already written to disk.
    /// </summary>
    Task<CameraClip?> SaveClipFromFile(
        Device device,
        string sourcePath,
        string quality,
        int durationSeconds,
        string cameraName);
}

public class CameraService : ICameraService
{
    /// <summary>
    /// The largest clip that is accepted from a device, in bytes.
    /// </summary>
    public const long MaxClipBytes = 250 * 1024 * 1024;

    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<CameraService> _logger;
    private readonly IWebHostEnvironment _hostEnvironment;

    // The sessions are shared between the instances of this service, so the
    // service is registered as a singleton. A chunked upload spans several
    // hub invocations, and each of them gets a new scope: a per-instance
    // store would be empty in the scope that receives the next chunk, and
    // that chunk would be dropped silently.
    private static readonly Dictionary<string, UploadSession> _uploads = new();
    private static readonly object _uploadLock = new();

    public CameraService(
        IAppDbFactory appDbFactory,
        IWebHostEnvironment hostEnvironment,
        ILogger<CameraService> logger)
    {
        _appDbFactory = appDbFactory;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public int DefaultRetentionCount { get; } = 200;

    public int DefaultRetentionHours { get; } = 24;

    public string GetCameraDirectory(Device device)
    {
        if (!string.IsNullOrWhiteSpace(device.CameraDirectory))
        {
            return device.CameraDirectory;
        }

        return GetDefaultCameraDirectory(device);
    }

    private string GetDefaultCameraDirectory(Device device)
    {
        return Path.Combine(
            _hostEnvironment.ContentRootPath,
            "AppData",
            "Camera",
            device.ID);
    }

    private string GetUploadDirectory(Device device)
    {
        return Path.Combine(GetCameraDirectory(device), "uploads");
    }

    private int GetRetentionCount(Device device)
    {
        return device.CameraRetentionCount > 0 ?
            device.CameraRetentionCount :
            DefaultRetentionCount;
    }

    private int GetRetentionHours(Device device)
    {
        return device.CameraRetentionHours > 0 ?
            device.CameraRetentionHours :
            DefaultRetentionHours;
    }

    public CameraClip[] GetClips(string deviceId, int maxCount)
    {
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            return dbContext.CameraClips
                .Where(x => x.DeviceID == deviceId)
                .OrderByDescending(x => x.TimeStamp)
                .Take(maxCount)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting camera clips for device {deviceId}.", deviceId);
            return Array.Empty<CameraClip>();
        }
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

        var path = Path.Combine(GetCameraDirectory(device), fileName);

        if (!File.Exists(path))
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
            _logger.LogError(ex, "Error while reading a camera clip for device {deviceId}.", device.ID);
            return null;
        }
    }

    /// <summary>
    /// Returns true when the file name is a plain file name, so it can't be
    /// used to reach outside of the device's camera directory.
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
        // not be able to reach outside of the camera directory.
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
            _logger.LogError(ex, "Error while creating the camera upload directory for device {deviceId}.", device.ID);
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
                _logger.LogWarning(
                    "A camera clip chunk {clipId} was ignored because its upload session does not exist.",
                    clipId);
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
            _logger.LogError(ex, "Error while writing a camera clip chunk.");
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

    public async Task<CameraClip?> CompleteUpload(
        Device device,
        string clipId,
        string quality,
        int durationSeconds,
        string cameraName)
    {
        UploadSession? session;

        lock (_uploadLock)
        {
            if (!_uploads.Remove(clipId, out session))
            {
                _logger.LogWarning(
                    "The camera clip upload {clipId} was completed without a matching session, so nothing was stored.",
                    clipId);
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
                    "The camera clip from device {deviceId} is too large ({size} bytes) and was discarded.",
                    device.ID,
                    size);

                TryDeleteFile(session.Path);
                return null;
            }

            return await SaveClipFromFile(device, session.Path, quality, durationSeconds, cameraName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while completing a camera clip upload for device {deviceId}.", device.ID);
            TryDeleteFile(session.Path);
            return null;
        }
    }

    public async Task<CameraClip?> SaveClip(
        Device device,
        byte[] contents,
        string quality,
        int durationSeconds,
        string cameraName)
    {
        if (contents is null || contents.Length == 0)
        {
            return null;
        }

        if (contents.Length > MaxClipBytes)
        {
            _logger.LogWarning(
                "The camera clip from device {deviceId} is too large ({size} bytes) and was discarded.",
                device.ID,
                contents.Length);

            return null;
        }

        var directory = GetCameraDirectory(device);
        var fileName = BuildFileName();

        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, fileName), contents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving a camera clip for device {deviceId}.", device.ID);
            return null;
        }

        return await AddClipMetadata(device, fileName, contents.Length, quality, durationSeconds, cameraName);
    }

    public async Task<CameraClip?> SaveClipFromFile(
        Device device,
        string sourcePath,
        string quality,
        int durationSeconds,
        string cameraName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var directory = GetCameraDirectory(device);
        var fileName = BuildFileName();
        var destination = Path.Combine(directory, fileName);

        try
        {
            Directory.CreateDirectory(directory);
            File.Move(sourcePath, destination, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving a camera clip for device {deviceId}.", device.ID);
            TryDeleteFile(sourcePath);
            return null;
        }

        long size;
        try
        {
            size = new FileInfo(destination).Length;
        }
        catch
        {
            size = 0;
        }

        return await AddClipMetadata(device, fileName, size, quality, durationSeconds, cameraName);
    }

    private async Task<CameraClip?> AddClipMetadata(
        Device device,
        string fileName,
        long size,
        string quality,
        int durationSeconds,
        string cameraName)
    {
        var clip = new CameraClip
        {
            ID = Guid.NewGuid().ToString(),
            DeviceID = device.ID,
            TimeStamp = DateTimeOffset.Now,
            FileName = fileName,
            SizeBytes = size,
            Quality = CameraQualityPresets.ParseQuality(quality).ToString(),
            DurationSeconds = durationSeconds,
            CameraName = cameraName ?? string.Empty
        };

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            await dbContext.CameraClips.AddAsync(clip);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving camera clip metadata for device {deviceId}.", device.ID);
        }

        await DeleteExpiredClips(device);

        return clip;
    }

    private static string BuildFileName()
    {
        return $"{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}.mp4";
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
            var path = Path.Combine(GetCameraDirectory(device), fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting camera clip {fileName}.", fileName);
        }

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var clip = dbContext.CameraClips
                .FirstOrDefault(x => x.DeviceID == device.ID && x.FileName == fileName);

            if (clip is not null)
            {
                dbContext.CameraClips.Remove(clip);
                dbContext.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting camera clip metadata for {fileName}.", fileName);
        }
    }

    /// <summary>
    /// Removes every clip that was recorded from a device.
    /// </summary>
    public void ClearClips(Device device)
    {
        var clips = GetClips(device.ID, int.MaxValue);

        try
        {
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GetCameraDirectory(device),
                GetDefaultCameraDirectory(device)
            };

            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(directory, "*.mp4"))
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
            _logger.LogError(ex, "Error while clearing camera clips for device {deviceId}.", device.ID);
        }

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var rows = dbContext.CameraClips.Where(x => x.DeviceID == device.ID);
            dbContext.CameraClips.RemoveRange(rows);
            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while clearing camera clip metadata for device {deviceId}.", device.ID);
        }

        _logger.LogInformation(
            "Cleared {count} camera clips for device {deviceId}.",
            clips.Length,
            device.ID);
    }

    public async Task DeleteExpiredClips(Device device)
    {
        try
        {
            var expired = new List<CameraClip>();
            var retentionCount = GetRetentionCount(device);
            var clips = GetClips(device.ID, int.MaxValue);

            var cutoff = DateTimeOffset.Now.AddHours(-GetRetentionHours(device));
            foreach (var clip in clips)
            {
                if (clip.TimeStamp < cutoff)
                {
                    expired.Add(clip);
                }
            }

            // Keep the newest ones and drop everything over the limit.
            foreach (var clip in clips.Skip(retentionCount))
            {
                if (!expired.Any(x => x.ID == clip.ID))
                {
                    expired.Add(clip);
                }
            }

            if (expired.Count == 0)
            {
                return;
            }

            var directory = GetCameraDirectory(device);
            foreach (var clip in expired)
            {
                try
                {
                    var path = Path.Combine(directory, clip.FileName);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while deleting camera clip {fileName}.", clip.FileName);
                }
            }

            using var dbContext = _appDbFactory.GetContext();
            dbContext.CameraClips.RemoveRange(expired);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting expired camera clips for device {deviceId}.", device.ID);
        }
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

    private sealed record UploadSession(string DeviceId, string Path);
}
