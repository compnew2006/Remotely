using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;

namespace Remotely.Server.Services;

/// <summary>
/// Stores the text that was typed on remote devices, and enforces the
/// retention limits, so the database can't fill up on the server.
/// </summary>
public interface IKeyboardLogService
{
    /// <summary>
    /// The default number of logs kept per device.
    /// </summary>
    int DefaultRetentionCount { get; }

    /// <summary>
    /// The default number of hours a log is kept.
    /// </summary>
    int DefaultRetentionHours { get; }

    void ClearLogs(Device device);

    void DeleteLog(Device device, string logId);

    Task DeleteExpiredLogs(Device device);

    KeyboardLog[] GetLogs(string deviceId, int maxCount);

    /// <summary>
    /// Stores one burst of captured text.
    /// </summary>
    Task<KeyboardLog?> SaveLog(Device device, KeyboardLogChunk chunk);
}

public class KeyboardLogService : IKeyboardLogService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<KeyboardLogService> _logger;

    public KeyboardLogService(
        IAppDbFactory appDbFactory,
        ILogger<KeyboardLogService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public int DefaultRetentionCount { get; } = 500;

    public int DefaultRetentionHours { get; } = 24;

    private int GetRetentionCount(Device device)
    {
        return device.KeyboardRetentionCount > 0 ?
            device.KeyboardRetentionCount :
            DefaultRetentionCount;
    }

    private int GetRetentionHours(Device device)
    {
        return device.KeyboardRetentionHours > 0 ?
            device.KeyboardRetentionHours :
            DefaultRetentionHours;
    }

    public KeyboardLog[] GetLogs(string deviceId, int maxCount)
    {
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            return dbContext.KeyboardLogs
                .Where(x => x.DeviceID == deviceId)
                .OrderByDescending(x => x.TimeStamp)
                .Take(maxCount)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting the keyboard logs of device {deviceId}.", deviceId);
            return Array.Empty<KeyboardLog>();
        }
    }

    public async Task<KeyboardLog?> SaveLog(Device device, KeyboardLogChunk chunk)
    {
        if (chunk is null || string.IsNullOrEmpty(chunk.Text))
        {
            return null;
        }

        var log = new KeyboardLog
        {
            ID = Guid.NewGuid().ToString(),
            DeviceID = device.ID,
            TimeStamp = chunk.TimeStamp == default ? DateTimeOffset.Now : chunk.TimeStamp,
            WindowTitle = chunk.WindowTitle ?? string.Empty,
            ProcessName = chunk.ProcessName ?? string.Empty,
            Text = chunk.Text,
            IsPasswordField = chunk.IsPasswordField
        };

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            await dbContext.KeyboardLogs.AddAsync(log);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving a keyboard log of device {deviceId}.", device.ID);
            return null;
        }

        await DeleteExpiredLogs(device);

        return log;
    }

    public void DeleteLog(Device device, string logId)
    {
        if (string.IsNullOrWhiteSpace(logId))
        {
            return;
        }

        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var log = dbContext.KeyboardLogs
                .FirstOrDefault(x => x.DeviceID == device.ID && x.ID == logId);

            if (log is not null)
            {
                dbContext.KeyboardLogs.Remove(log);
                dbContext.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting a keyboard log of device {deviceId}.", device.ID);
        }
    }

    public void ClearLogs(Device device)
    {
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var rows = dbContext.KeyboardLogs.Where(x => x.DeviceID == device.ID);
            dbContext.KeyboardLogs.RemoveRange(rows);
            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while clearing the keyboard logs of device {deviceId}.", device.ID);
        }

        _logger.LogInformation("Cleared the keyboard logs of device {deviceId}.", device.ID);
    }

    public async Task DeleteExpiredLogs(Device device)
    {
        try
        {
            var expired = new List<KeyboardLog>();
            var retentionCount = GetRetentionCount(device);
            var logs = GetLogs(device.ID, int.MaxValue);

            var cutoff = DateTimeOffset.Now.AddHours(-GetRetentionHours(device));
            foreach (var log in logs)
            {
                if (log.TimeStamp < cutoff)
                {
                    expired.Add(log);
                }
            }

            // Keep the newest ones and drop everything over the limit.
            foreach (var log in logs.Skip(retentionCount))
            {
                if (!expired.Any(x => x.ID == log.ID))
                {
                    expired.Add(log);
                }
            }

            if (expired.Count == 0)
            {
                return;
            }

            using var dbContext = _appDbFactory.GetContext();
            dbContext.KeyboardLogs.RemoveRange(expired);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting the expired keyboard logs of device {deviceId}.", device.ID);
        }
    }
}
