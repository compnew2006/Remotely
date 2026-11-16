using System.Text.Json;
using Remotely.Server.Data;
using Remotely.Server.Extensions;
using Remotely.Server.Models;

namespace Remotely.Server.Services;

/// <summary>
/// The application settings are stored as one row in <c>KeyValueRecords</c>.
/// This service owns reading and writing that row.
/// </summary>
public interface ISettingsService
{
    Task<SettingsModel> GetSettings();

    Task SaveSettings(SettingsModel settings);
}

/// <summary>
/// Registered as a singleton on purpose.
/// <para>
/// The previous implementation kept the serialization semaphore on
/// a transient service, so two
/// concurrent requests each got their own semaphore and the read-modify-write
/// of the settings row was not serialized at all.  A singleton owns one gate
/// per process.
/// </para>
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        IAppDbFactory appDbFactory,
        ILogger<SettingsService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task<SettingsModel> GetSettings()
    {
        await _gate.WaitAsync();
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            return await dbContext.GetAppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting settings from database.");
            return new();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveSettings(SettingsModel settings)
    {
        await _gate.WaitAsync();
        try
        {
            using var dbContext = _appDbFactory.GetContext();
            var record = await dbContext.KeyValueRecords.FindAsync(SettingsModel.DbKey);
            if (record is null)
            {
                record = new()
                {
                    Key = SettingsModel.DbKey,
                };
                await dbContext.KeyValueRecords.AddAsync(record);
                await dbContext.SaveChangesAsync();
            }

            record.Value = JsonSerializer.Serialize(settings);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving settings to database.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
