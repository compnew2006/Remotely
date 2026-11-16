using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The runs of the scripts and their results.
/// </summary>
public interface IScriptResultService
{
    Task<Result<ScriptResult>> AddScriptResult(ScriptResultDto dto);

    Task<Result> AddScriptResultToScriptRun(string scriptResultId, int scriptRunId);

    Task AddScriptRun(ScriptRun scriptRun);

    ScriptResult[] GetAllCommandResults(string orgId);

    ScriptResult[] GetAllCommandResultsForUser(string orgId, string userName, string deviceId);

    ScriptResult[] GetAllScriptResults(string orgId, string deviceId);

    Task<IEnumerable<ScriptRun>> GetPendingScriptRuns(string deviceId);

    Task<Result<ScriptResult>> GetScriptResult(string resultId);

    Task<Result<ScriptResult>> GetScriptResult(string resultId, string orgId);
}

public class ScriptResultService : IScriptResultService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<ScriptResultService> _logger;

    public ScriptResultService(
        IAppDbFactory appDbFactory,
        ILogger<ScriptResultService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task<Result<ScriptResult>> AddScriptResult(ScriptResultDto dto)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = dbContext.Devices.Find(dto.DeviceID);

        if (device is null)
        {
            return Result.Fail<ScriptResult>("Device not found.");
        }

        var scriptResult = new ScriptResult
        {
            DeviceID = dto.DeviceID,
            OrganizationID = device.OrganizationID
        };

        var entry = dbContext.Attach(scriptResult);
        entry.CurrentValues.SetValues(dto);
        entry.State = EntityState.Added;
        await dbContext.ScriptResults.AddAsync(scriptResult);
        await dbContext.SaveChangesAsync();
        return Result.Ok(scriptResult);
    }

    public async Task<Result> AddScriptResultToScriptRun(string scriptResultId, int scriptRunId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var run = await dbContext.ScriptRuns
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == scriptRunId);

        if (run is null)
        {
            return Result.Fail("Run not found.");
        }

        var result = await dbContext.ScriptResults.FindAsync(scriptResultId);

        if (result is null)
        {
            return Result.Fail("Results not found.");
        }

        run.Results ??= new List<ScriptResult>();
        run.Results.Add(result);

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task AddScriptRun(ScriptRun scriptRun)
    {
        using var dbContext = _appDbFactory.GetContext();

        dbContext.Attach(scriptRun);
        dbContext.ScriptRuns.Add(scriptRun);
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<ScriptRun>> GetPendingScriptRuns(string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .AsNoTracking()
            .Include(x => x.ScriptRuns)
            .ThenInclude(x => x.Results)
            .Include(x => x.ScriptResults)
            .FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return Enumerable.Empty<ScriptRun>();
        }

        device.ScriptResults ??= new();
        var scriptResultsLookup = device.ScriptResults
            .Select(x => x.ScriptRunId)
            .Distinct()
            .ToHashSet();

        return device.ScriptRuns
            .OrderByDescending(x => x.RunAt)
            .DistinctBy(x => x.SavedScriptId)
            .Where(x => !scriptResultsLookup.Contains(x.Id))
            .ToArray();
    }

    public async Task<Result<ScriptResult>> GetScriptResult(string resultId, string orgId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var result = await dbContext.ScriptResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OrganizationID == orgId &&
                x.ID == resultId);

        if (result is null)
        {
            return Result.Fail<ScriptResult>("Script result not found.");
        }
        return Result.Ok(result);
    }

    public ScriptResult[] GetAllCommandResults(string orgId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.ScriptResults
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId)
            .OrderByDescending(x => x.TimeStamp)
            .ToArray();
    }

    public ScriptResult[] GetAllCommandResultsForUser(string orgId, string userName, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.ScriptResults
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId &&
                x.SenderUserName == userName &&
                x.DeviceID == deviceId)
            .OrderByDescending(x => x.TimeStamp)
            .ToArray();
    }

    public ScriptResult[] GetAllScriptResults(string orgId, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.ScriptResults
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId && x.DeviceID == deviceId)
            .OrderByDescending(x => x.TimeStamp)
            .ToArray();
    }

    public async Task<Result<ScriptResult>> GetScriptResult(string resultId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var result = await dbContext.ScriptResults.FindAsync(resultId);

        if (result is null)
        {
            return Result.Fail<ScriptResult>("Script result not found.");
        }
        return Result.Ok(result);
    }
}
