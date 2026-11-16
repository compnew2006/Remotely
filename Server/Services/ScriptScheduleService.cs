using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Services;

/// <summary>
/// The schedules that run a saved script on a device.
/// </summary>
public interface IScriptScheduleService
{
    Task AddOrUpdateScriptSchedule(ScriptSchedule schedule);

    Task DeleteScriptSchedule(int scriptScheduleId);

    Task<List<ScriptSchedule>> GetScriptSchedules(string organizationId);

    Task<List<ScriptSchedule>> GetScriptSchedulesDue();
}

public class ScriptScheduleService : IScriptScheduleService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<ScriptScheduleService> _logger;

    public ScriptScheduleService(
        IAppDbFactory appDbFactory,
        ILogger<ScriptScheduleService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task AddOrUpdateScriptSchedule(ScriptSchedule schedule)
    {
        using var dbContext = _appDbFactory.GetContext();

        var existingSchedule = await dbContext.ScriptSchedules
            .Include(x => x.Creator)
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .FirstOrDefaultAsync(x => x.Id == schedule.Id);

        if (existingSchedule is null)
        {
            dbContext.Update(schedule);
        }
        else
        {
            var entry = dbContext.Entry(existingSchedule);
            entry.CurrentValues.SetValues(schedule);

            existingSchedule.Devices.Clear();
            if (schedule.Devices?.Any() == true)
            {
                var deviceIds = schedule.Devices.Select(x => x.ID);
                var newDevices = await dbContext.Devices
                    .Where(x => deviceIds.Contains(x.ID))
                    .ToListAsync();
                existingSchedule.Devices.AddRange(newDevices);
            }

            existingSchedule.DeviceGroups.Clear();
            if (schedule.DeviceGroups?.Any() == true)
            {
                var deviceGroupIds = schedule.DeviceGroups.Select(x => x.ID);
                var newDeviceGroups = await dbContext.DeviceGroups
                    .Where(x => deviceGroupIds.Contains(x.ID))
                    .ToListAsync();
                existingSchedule.DeviceGroups.AddRange(newDeviceGroups);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteScriptSchedule(int scriptScheduleId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var schedule = await dbContext.ScriptSchedules
            .Include(x => x.ScriptRuns)
            .ThenInclude(x => x.Results)
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .FirstOrDefaultAsync(x => x.Id == scriptScheduleId);

        if (schedule is not null)
        {
            dbContext.ScriptSchedules.Remove(schedule);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<List<ScriptSchedule>> GetScriptSchedules(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();
        return await dbContext.ScriptSchedules
            .AsNoTracking()
            .Include(x => x.Creator)
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .Where(x => x.OrganizationID == organizationId)
            .ToListAsync();
    }

    public async Task<List<ScriptSchedule>> GetScriptSchedulesDue()
    {
        using var dbContext = _appDbFactory.GetContext();

        var now = Time.Now;

        return await dbContext.ScriptSchedules
            .AsNoTracking()
            .Include(x => x.Devices)
            .Include(x => x.DeviceGroups)
            .ThenInclude(x => x.Devices)
            .Where(x => x.NextRun < now)
            .ToListAsync();
    }
}
