using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Server.Models;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// Stores the screenshots captured by remote devices and enforces the
/// retention limits, so the disk can't fill up on the server.
/// </summary>
public interface IScreenshotService
{
    /// <summary>
    /// The default number of screenshots kept per device.
    /// </summary>
    int DefaultRetentionCount { get; }

    /// <summary>
    /// The default number of hours a screenshot is kept.
    /// </summary>
    int DefaultRetentionHours { get; }

    void ClearScreenshots(Device device);

    Task DeleteExpiredScreenshots(Device device);

    void DeleteScreenshot(Device device, string fileName);

    byte[]? GetScreenshotContents(Device device, string fileName);

    string GetScreenshotsDirectory(Device device);

    Screenshot[] GetScreenshots(string deviceId, int maxCount);

    Task<Screenshot?> SaveScreenshot(Device device, byte[] contents);

    DirectoryListing BrowseDirectory(string? path);
}

public class ScreenshotService : IScreenshotService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<ScreenshotService> _logger;
    private readonly IWebHostEnvironment _hostEnvironment;

    public ScreenshotService(
        IAppDbFactory appDbFactory,
        IWebHostEnvironment hostEnvironment,
        ILogger<ScreenshotService> logger)
    {
        _appDbFactory = appDbFactory;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public int DefaultRetentionCount { get; } = 200;

    public int DefaultRetentionHours { get; } = 24;

    public string GetScreenshotsDirectory(Device device)
    {
        if (!string.IsNullOrWhiteSpace(device.ScreenshotDirectory))
        {
            return device.ScreenshotDirectory;
        }

        return GetDefaultScreenshotsDirectory(device);
    }

    private string GetDefaultScreenshotsDirectory(Device device)
    {
        return Path.Combine(
            _hostEnvironment.ContentRootPath,
            "AppData",
            "Screenshots",
            device.ID);
    }

    private int GetRetentionCount(Device device)
    {
        return device.ScreenshotRetentionCount > 0 ?
            device.ScreenshotRetentionCount :
            DefaultRetentionCount;
    }

    private int GetRetentionHours(Device device)
    {
        return device.ScreenshotRetentionHours > 0 ?
            device.ScreenshotRetentionHours :
            DefaultRetentionHours;
    }

    public async Task<Screenshot?> SaveScreenshot(Device device, byte[] contents)
    {
        if (contents.Length == 0)
        {
            return null;
        }

        var timestamp = DateTimeOffset.Now;
        var fileName = $"{timestamp:yyyyMMdd-HHmmssfff}.jpg";
        string directory;

        try
        {
            directory = Path.GetFullPath(GetScreenshotsDirectory(device));
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, fileName), contents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving a screenshot for device {deviceId}.", device.ID);
            return null;
        }

        var screenshot = new Screenshot
        {
            ID = Guid.NewGuid().ToString(),
            DeviceID = device.ID,
            TimeStamp = timestamp,
            FileName = fileName,
            StorageDirectory = directory,
            SizeBytes = contents.Length
        };

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            await dbContext.Screenshots.AddAsync(screenshot);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving screenshot metadata for device {deviceId}.", device.ID);
        }

        await DeleteExpiredScreenshots(device);

        return screenshot;
    }

    public Screenshot[] GetScreenshots(string deviceId, int maxCount)
    {
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            return dbContext.Screenshots
                .Where(x => x.DeviceID == deviceId)
                .OrderByDescending(x => x.TimeStamp)
                .Take(maxCount)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting screenshots for device {deviceId}.", deviceId);
            return Array.Empty<Screenshot>();
        }
    }

    private Screenshot[] GetScreenshotBatch(
        string deviceId,
        DateTimeOffset? cursorTimestamp,
        string? cursorId,
        int batchSize)
    {
        using var dbContext = _appDbFactory.GetContext();
        var query = dbContext.Screenshots
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

    public byte[]? GetScreenshotContents(Device device, string fileName)
    {
        try
        {
            if (!IsSafeFileName(fileName))
            {
                return null;
            }

            using var dbContext = _appDbFactory.GetContext();
            var screenshot = dbContext.Screenshots
                .AsNoTracking()
                .FirstOrDefault(x => x.DeviceID == device.ID && x.FileName == fileName);

            if (screenshot is null)
            {
                return null;
            }

            var path = GetScreenshotPath(device, screenshot);
            if (path is null || !File.Exists(path))
            {
                return null;
            }

            return File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reading a screenshot for device {deviceId}.", device.ID);
            return null;
        }
    }

    /// <summary>
    /// Deletes a single screenshot of a device.
    /// </summary>
    public void DeleteScreenshot(Device device, string fileName)
    {
        if (!IsSafeFileName(fileName))
        {
            return;
        }

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var screenshot = dbContext.Screenshots
                .FirstOrDefault(x => x.DeviceID == device.ID && x.FileName == fileName);

            if (screenshot is null)
            {
                return;
            }

            var path = GetScreenshotPath(device, screenshot);
            if (path is null || !TryDeleteFile(path))
            {
                return;
            }

            dbContext.Screenshots.Remove(screenshot);
            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting screenshot {fileName}.", fileName);
        }
    }

    /// <summary>
    /// Removes every screenshot that was captured from a device.
    /// </summary>
    public void ClearScreenshots(Device device)
    {
        const int batchSize = 250;
        var removedCount = 0;
        DateTimeOffset? cursorTimestamp = null;
        string? cursorId = null;

        while (true)
        {
            var screenshots = GetScreenshotBatch(
                device.ID,
                cursorTimestamp,
                cursorId,
                batchSize);

            if (screenshots.Length == 0)
            {
                break;
            }

            var lastScreenshot = screenshots[^1];
            cursorTimestamp = lastScreenshot.TimeStamp;
            cursorId = lastScreenshot.ID;

            var removable = new List<Screenshot>();
            foreach (var screenshot in screenshots)
            {
                var path = GetScreenshotPath(device, screenshot);
                if (path is null)
                {
                    _logger.LogWarning(
                        "Keeping screenshot metadata for {fileName}; its legacy path could not be resolved.",
                        screenshot.FileName);
                    continue;
                }

                if (TryDeleteFile(path))
                {
                    removable.Add(screenshot);
                }
            }

            if (removable.Count > 0)
            {
                try
                {
                    using var dbContext = _appDbFactory.GetContext();
                    dbContext.Screenshots.RemoveRange(removable);
                    dbContext.SaveChanges();
                    removedCount += removable.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while clearing screenshot metadata for device {deviceId}.", device.ID);
                    break;
                }
            }

            if (screenshots.Length < batchSize)
            {
                break;
            }
        }

        // Also clear orphaned files from the current and default folders.
        var directories = new HashSet<string>(GetPathComparer())
        {
            GetScreenshotsDirectory(device),
            GetDefaultScreenshotsDirectory(device)
        };

        foreach (var directory in directories)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(directory, "*.jpg"))
                {
                    TryDeleteFile(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clearing screenshots for device {deviceId}.", device.ID);
            }
        }

        _logger.LogInformation(
            "Cleared {count} screenshots for device {deviceId}.",
            removedCount,
            device.ID);
    }

    public async Task DeleteExpiredScreenshots(Device device)
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
                var screenshots = GetScreenshotBatch(
                    device.ID,
                    cursorTimestamp,
                    cursorId,
                    batchSize);

                if (screenshots.Length == 0)
                {
                    break;
                }

                var lastScreenshot = screenshots[^1];
                cursorTimestamp = lastScreenshot.TimeStamp;
                cursorId = lastScreenshot.ID;

                var removable = new List<Screenshot>();
                foreach (var screenshot in screenshots)
                {
                    var overRetentionCount = processedCount >= retentionCount;
                    processedCount++;

                    var expiredByAge = screenshot.TimeStamp < cutoff;
                    if (!expiredByAge && !overRetentionCount)
                    {
                        continue;
                    }

                    if (overRetentionCount && screenshot.TimeStamp >= sweepStartedAt)
                    {
                        continue;
                    }

                    var path = GetScreenshotPath(device, screenshot);
                    if (path is null)
                    {
                        _logger.LogWarning(
                            "Keeping expired screenshot metadata for {fileName}; its legacy path could not be resolved.",
                            screenshot.FileName);
                        continue;
                    }

                    if (TryDeleteFile(path))
                    {
                        removable.Add(screenshot);
                    }
                }

                if (removable.Count > 0)
                {
                    using var dbContext = _appDbFactory.GetContext();
                    dbContext.Screenshots.RemoveRange(removable);
                    await dbContext.SaveChangesAsync();
                }

                if (screenshots.Length < batchSize)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting expired screenshots for device {deviceId}.", device.ID);
        }
    }

    private string? GetScreenshotPath(Device device, Screenshot screenshot)
    {
        if (!IsSafeFileName(screenshot.FileName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(screenshot.StorageDirectory))
        {
            return Path.Combine(screenshot.StorageDirectory, screenshot.FileName);
        }

        foreach (var directory in new[]
        {
            GetScreenshotsDirectory(device),
            GetDefaultScreenshotsDirectory(device)
        }.Distinct(GetPathComparer()))
        {
            try
            {
                var path = Path.Combine(directory, screenshot.FileName);
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

    private static bool IsSafeFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
            fileName == Path.GetFileName(fileName) &&
            !fileName.Contains("..");
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
            _logger.LogWarning(ex, "Error while deleting screenshot {fileName}.", path);
            return false;
        }
    }

    /// <summary>
    /// Lists the folders on the server so that a screenshot folder can be
    /// picked.
    /// </summary>
    public DirectoryListing BrowseDirectory(string? path)
    {
        var listing = new DirectoryListing();

        if (string.IsNullOrWhiteSpace(path))
        {
            listing.Path = string.Empty;
            listing.Drives = DriveInfo.GetDrives()
                .Where(x => x.IsReady)
                .Select(x => x.RootDirectory.FullName)
                .ToArray();

            return listing;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
                listing.Path = path;
                listing.Directories = Array.Empty<string>();
                return listing;
            }

            listing.Path = fullPath;

            var parent = Directory.GetParent(fullPath);
            listing.ParentPath = parent?.FullName;

            listing.Directories = Directory
                .EnumerateDirectories(fullPath)
                .OrderBy(x => x)
                .ToArray();

            listing.Drives = DriveInfo.GetDrives()
                .Where(x => x.IsReady)
                .Select(x => x.RootDirectory.FullName)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while browsing the directory {path}.", path);
        }

        return listing;
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
