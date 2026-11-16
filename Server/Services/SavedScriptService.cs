using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The scripts that the users save and reuse.
/// </summary>
public interface ISavedScriptService
{
    Task<Result> AddOrUpdateSavedScript(SavedScript script, string userId);

    Task DeleteSavedScript(Guid scriptId);

    Task<List<SavedScript>> GetQuickScripts(string userId);

    Task<Result<SavedScript>> GetSavedScript(Guid scriptId);

    Task<Result<SavedScript>> GetSavedScript(string userId, Guid scriptId);

    Task<List<SavedScript>> GetSavedScriptsWithoutContent(string userId, string organizationId);
}

public class SavedScriptService : ISavedScriptService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<SavedScriptService> _logger;

    public SavedScriptService(
        IAppDbFactory appDbFactory,
        ILogger<SavedScriptService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task<Result> AddOrUpdateSavedScript(SavedScript script, string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        // The method adds and updates, so the state has to follow the row.
        // Update() alone marks the script as modified, and the update of a
        // script that was never stored affects no row and ends in a
        // concurrency exception.
        var exists = await dbContext.SavedScripts
            .AsNoTracking()
            .AnyAsync(x => x.Id == script.Id);

        if (exists)
        {
            dbContext.SavedScripts.Update(script);
        }
        else
        {
            // The script is new.  It is attached first, so a creator that
            // comes with it is treated as an existing row instead of being
            // inserted a second time.
            dbContext.SavedScripts.Attach(script);
            dbContext.Entry(script).State = EntityState.Added;
        }

        if (script.Creator is null)
        {
            var user = await dbContext.Users.FindAsync(userId);
            if (user is null)
            {
                return Result.Fail("User not found.");
            }

            script.CreatorId = user.Id;
            script.Creator = user;
            script.OrganizationID = user.OrganizationID;
        }

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task DeleteSavedScript(Guid scriptId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var schedules = await dbContext.ScriptSchedules
            .Where(x => x.SavedScriptId == scriptId)
            .ToListAsync();

        if (schedules.Count > 0)
        {
            dbContext.ScriptSchedules.RemoveRange(schedules);
        }

        var script = await dbContext.SavedScripts
            .Include(x => x.ScriptResults)
            .Include(x => x.ScriptRuns)
            .FirstOrDefaultAsync(x => x.Id == scriptId);

        if (script is not null)
        {
            dbContext.SavedScripts.Remove(script);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<Result<SavedScript>> GetSavedScript(string userId, Guid scriptId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var script = await dbContext.SavedScripts
            .AsNoTracking()
            .Include(x => x.Creator)
            .FirstOrDefaultAsync(x =>
                x.Id == scriptId &&
                (x.IsPublic || x.CreatorId == userId));

        if (script is null)
        {
            return Result.Fail<SavedScript>("Script not found.");
        }
        return Result.Ok(script);
    }

    public async Task<List<SavedScript>> GetSavedScriptsWithoutContent(string userId, string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return await dbContext.SavedScripts
            .AsNoTracking()
            .Include(x => x.Creator)
            .Where(x => 
                x.Creator!.OrganizationID == organizationId &&
                (x.IsPublic || x.CreatorId == userId))
            .Select(x => new SavedScript()
            {
                Creator = x.Creator,
                CreatorId = x.CreatorId,
                FolderPath = x.FolderPath,
                Id = x.Id,
                IsPublic = x.IsPublic,
                IsQuickScript = x.IsQuickScript,
                Name = x.Name,
                OrganizationID = x.OrganizationID
            })
            .ToListAsync();
    }

    public async Task<List<SavedScript>> GetQuickScripts(string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return await dbContext.SavedScripts
            .Where(x => x.CreatorId == userId && x.IsQuickScript)
            .ToListAsync();
    }

    public async Task<Result<SavedScript>> GetSavedScript(Guid scriptId)
    {
        using var dbContext = _appDbFactory.GetContext();
        var script = await dbContext.SavedScripts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == scriptId);

        if (script is null)
        {
            return Result.Fail<SavedScript>("Script not found.");
        }
        return Result.Ok(script);
    }
}
