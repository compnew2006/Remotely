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
        var directory = GetScreenshotsDirectory(device);

        try
        {
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

    public byte[]? GetScreenshotContents(Device device, string fileName)
    {
        try
        {
            // Only allow plain file names, so the file name can't be used to
            // reach outside of the device's screenshot directory.
            if (fileName.Contains(Path.DirectorySeparatorChar) ||
                fileName.Contains(Path.AltDirectorySeparatorChar) ||
                fileName.Contains(".."))
            {
                return null;
            }

            var path = Path.Combine(GetScreenshotsDirectory(device), fileName);
            if (!File.Exists(path))
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
        if (fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar) ||
            fileName.Contains(".."))
        {
            return;
        }

        try
        {
            var path = Path.Combine(GetScreenshotsDirectory(device), fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting screenshot {fileName}.", fileName);
        }

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var screenshot = dbContext.Screenshots
                .FirstOrDefault(x => x.DeviceID == device.ID && x.FileName == fileName);

            if (screenshot is not null)
            {
                dbContext.Screenshots.Remove(screenshot);
                dbContext.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting screenshot metadata for {fileName}.", fileName);
        }
    }

    /// <summary>
    /// Removes every screenshot that was captured from a device.
    /// </summary>
    public void ClearScreenshots(Device device)
    {
        var screenshots = GetScreenshots(device.ID, int.MaxValue);

        // The screenshots may have been stored in a different folder before the
        // folder was changed, so the default location is cleared as well.
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error while deleting screenshot {fileName}.", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clearing screenshots for device {deviceId}.", device.ID);
            }
        }

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var rows = dbContext.Screenshots.Where(x => x.DeviceID == device.ID);
            dbContext.Screenshots.RemoveRange(rows);
            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while clearing screenshot metadata for device {deviceId}.", device.ID);
        }

        _logger.LogInformation(
            "Cleared {count} screenshots for device {deviceId}.",
            screenshots.Length,
            device.ID);
    }

    public async Task DeleteExpiredScreenshots(Device device)
    {
        try
        {
            var expired = new List<Screenshot>();
            var retentionCount = GetRetentionCount(device);
            var screenshots = GetScreenshots(device.ID, retentionCount * 2);

            var cutoff = DateTimeOffset.Now.AddHours(-GetRetentionHours(device));
            foreach (var screenshot in screenshots)
            {
                if (screenshot.TimeStamp < cutoff)
                {
                    expired.Add(screenshot);
                }
            }

            // Keep the newest ones and drop everything over the limit.
            foreach (var screenshot in screenshots.Skip(retentionCount))
            {
                if (!expired.Any(x => x.ID == screenshot.ID))
                {
                    expired.Add(screenshot);
                }
            }

            if (expired.Count == 0)
            {
                return;
            }

            var directory = GetScreenshotsDirectory(device);
            foreach (var screenshot in expired)
            {
                try
                {
                    var path = Path.Combine(directory, screenshot.FileName);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while deleting screenshot {fileName}.", screenshot.FileName);
                }
            }

            using var dbContext = _appDbFactory.GetContext();
            dbContext.Screenshots.RemoveRange(expired);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting expired screenshots for device {deviceId}.", device.ID);
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
}
