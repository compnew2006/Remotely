using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The membership of the device groups: the users that may see the devices
/// of a group.
/// </summary>
public interface IDeviceGroupMembershipService
{
    bool AddUserToDeviceGroup(string orgId, string groupId, string userName, out string resultMessage);

    Task<bool> RemoveUserFromDeviceGroup(string orgId, string groupId, string userId);
}

public class DeviceGroupMembershipService : IDeviceGroupMembershipService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<DeviceGroupMembershipService> _logger;

    public DeviceGroupMembershipService(
        IAppDbFactory appDbFactory,
        ILogger<DeviceGroupMembershipService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public bool AddUserToDeviceGroup(string orgId, string groupId, string userName, out string resultMessage)
    {
        using var dbContext = _appDbFactory.GetContext();

        resultMessage = string.Empty;

        var deviceGroup = dbContext.DeviceGroups
            .Include(x => x.Users)
            .FirstOrDefault(x =>
                x.ID == groupId &&
                x.OrganizationID == orgId);

        if (deviceGroup == null)
        {
            resultMessage = "Device group not found.";
            return false;
        }

        userName = userName.Trim().ToLower();

        var user = dbContext.Users
            .Include(x => x.DeviceGroups)
            .FirstOrDefault(x =>
                x.UserName!.ToLower() == userName &&
                x.OrganizationID == orgId);

        if (user == null)
        {
            resultMessage = "User not found.";
            return false;
        }

        deviceGroup.Devices ??= new List<Device>();
        user.DeviceGroups ??= new List<DeviceGroup>();

        if (deviceGroup.Users.Any(x => x.Id == user.Id))
        {
            resultMessage = "User already in group.";
            return false;
        }

        deviceGroup.Users.Add(user);
        user.DeviceGroups.Add(deviceGroup);
        dbContext.SaveChanges();
        resultMessage = user.Id;
        return true;
    }

    public async Task<bool> RemoveUserFromDeviceGroup(string orgID, string groupID, string userID)
    {
        using var dbContext = _appDbFactory.GetContext();

        var deviceGroup = await dbContext.DeviceGroups
            .Include(x => x.Users)
            .ThenInclude(x => x.DeviceGroups)
            .FirstOrDefaultAsync(x =>
                x.ID == groupID &&
                x.OrganizationID == orgID);

        if (deviceGroup?.Users?.Any(x => x.Id == userID) != true)
        {
            return false;
        }

        var user = deviceGroup.Users.FirstOrDefault(x => x.Id == userID);

        if (user is null)
        {
            return false;
        }

        user.DeviceGroups.Remove(deviceGroup);
        deviceGroup.Users.Remove(user);

        await dbContext.SaveChangesAsync();
        return true;
    }
}
