using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The device groups of an organization, and the devices that belong to them.
/// </summary>
public interface IDeviceGroupService
{
    Task<Result<DeviceGroup>> AddDeviceGroup(string orgId, DeviceGroup deviceGroup);

    Task<Result> AddDeviceToGroup(string deviceId, string groupId);

    Task<Result> DeleteDeviceGroup(string orgId, string deviceGroupId);

    DeviceGroup[] GetDeviceGroups(string username);

    DeviceGroup[] GetDeviceGroupsForOrganization(string organizationId);
}

public class DeviceGroupService : IDeviceGroupService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<DeviceGroupService> _logger;

    public DeviceGroupService(
        IAppDbFactory appDbFactory,
        ILogger<DeviceGroupService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task<Result<DeviceGroup>> AddDeviceGroup(string orgId, DeviceGroup deviceGroup)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = dbContext.Organizations
            .Include(x => x.DeviceGroups)
            .FirstOrDefault(x => x.ID == orgId);

        if (organization is null)
        {
            return Result.Fail<DeviceGroup>("Organization not found.");
        }

        if (dbContext.DeviceGroups.Any(x =>
            x.OrganizationID == orgId &&
            x.Name.ToLower() == deviceGroup.Name.ToLower()))
        {
            return Result.Fail<DeviceGroup>("Device group already exists.");
        }

        dbContext.Attach(deviceGroup);
        deviceGroup.Organization = organization;
        deviceGroup.OrganizationID = orgId;

        organization.DeviceGroups ??= new List<DeviceGroup>();
        organization.DeviceGroups.Add(deviceGroup);
        await dbContext.SaveChangesAsync();
        return Result.Ok(deviceGroup);
    }

    public async Task<Result> DeleteDeviceGroup(string orgId, string deviceGroupID)
    {
        using var dbContext = _appDbFactory.GetContext();

        var deviceGroup = dbContext.DeviceGroups
            .Include(x => x.Devices)
            .Include(x => x.Users)
            .ThenInclude(x => x.DeviceGroups)
            .FirstOrDefault(x =>
                x.ID == deviceGroupID &&
                x.OrganizationID == orgId);

        if (deviceGroup is null)
        {
            return Result.Fail("Device group not found.");
        }

        deviceGroup.Devices.ForEach(x =>
        {
            x.DeviceGroup = null;
            x.DeviceGroupID = null;
        });

        deviceGroup.Users.ForEach(x =>
        {
            x.DeviceGroups.Remove(deviceGroup);
        });

        deviceGroup.Devices.Clear();
        deviceGroup.Users.Clear();

        dbContext.DeviceGroups.Remove(deviceGroup);

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> AddDeviceToGroup(string deviceId, string groupId)
    {
        using var context = _appDbFactory.GetContext();
        var device = await context.Devices.FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return Result.Fail("Device not found.");
        }

        var group = await context.DeviceGroups.FirstOrDefaultAsync(x => 
            x.OrganizationID == device.OrganizationID &&
            x.ID == groupId);

        if (group is null)
        {
            return Result.Fail("Group not found.");
        }

        group.Devices ??= new();
        group.Devices.Add(device);
        device.DeviceGroup = group;
        device.DeviceGroupID = group.ID;
        await context.SaveChangesAsync();

        return Result.Ok();
    }

    public DeviceGroup[] GetDeviceGroups(string username)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = dbContext.Users
            .AsNoTracking()
            .FirstOrDefault(x => x.UserName == username);

        if (user is null)
        {
            return Array.Empty<DeviceGroup>();
        }
        var userId = user.Id;

        var groupIds = dbContext.DeviceGroups
            .AsNoTracking()
            .Include(x => x.Users)
            .ThenInclude(x => x.DeviceGroups)
            .Where(x =>
                x.OrganizationID == user.OrganizationID &&
                (
                    user.IsAdministrator ||
                    x.Users.Any(x => x.Id == userId)
                )
            )
            .Select(x => x.ID)
            .ToHashSet();

        if (groupIds.Any())
        {
            return dbContext.DeviceGroups
                .AsNoTracking()
                .Where(x => groupIds.Contains(x.ID))
                .OrderBy(x => x.Name)
                .ToArray();
        }

        return Array.Empty<DeviceGroup>();
    }

    public DeviceGroup[] GetDeviceGroupsForOrganization(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.DeviceGroups
            .AsNoTracking()
            .Include(x => x.Users)
            .Where(x => x.OrganizationID == organizationId)
            .OrderBy(x => x.Name)
            .ToArray();
    }
}
