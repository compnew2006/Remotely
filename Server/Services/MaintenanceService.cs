using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;

namespace Remotely.Server.Services;

/// <summary>
/// The periodic cleanup of the records that the server keeps for a while.
/// </summary>
public interface IMaintenanceService
{
    Task CleanupOldRecords();
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        IAppDbFactory appDbFactory,
        ISettingsService settingsService,
        ILogger<MaintenanceService> logger)
    {
        _appDbFactory = appDbFactory;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task CleanupOldRecords()
    {
        var settings = await _settingsService.GetSettings();
        using var dbContext = _appDbFactory.GetContext();

        if (settings.DataRetentionInDays < 0)
        {
            return;
        }

        var expirationDate = DateTimeOffset.Now - TimeSpan.FromDays(settings.DataRetentionInDays);

        var scriptRuns = await dbContext.ScriptRuns
            .Include(x => x.Results)
            .Include(x => x.Devices)
            .Where(x => x.RunAt < expirationDate)
            .ToArrayAsync();

        foreach (var run in scriptRuns)
        {
            run.Devices?.Clear();
            run.Results?.Clear();
        }

        dbContext.RemoveRange(scriptRuns);

        var commandResults = dbContext.ScriptResults
                                .Where(x => x.TimeStamp < expirationDate);

        dbContext.RemoveRange(commandResults);

        var sharedFiles = dbContext.SharedFiles
                                .Where(x => x.Timestamp < expirationDate);

        dbContext.RemoveRange(sharedFiles);

        await dbContext.SaveChangesAsync();
    }
}
