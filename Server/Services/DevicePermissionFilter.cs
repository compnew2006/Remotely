using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;

namespace Remotely.Server.Services;

/// <summary>
/// The users that may see a device: the members of its organization, and
/// within them the administrators and the members of its device group.
/// <para>
/// The rule is needed by more than one service (the alerts and the permission
/// filters of the data service), so it lives here instead of being copied.
/// </para>
/// </summary>
internal static class DevicePermissionFilter
{
    public static string[] FilterUsersByDevicePermission(
        AppDb dbContext,
        IEnumerable<string> userIDs,
        string deviceID)
    {
        var device = dbContext.Devices
             .Include(x => x.DeviceGroup)
             .ThenInclude(x => x!.Users)
             .FirstOrDefault(x => x.ID == deviceID);

        if (device is null)
        {
            return Array.Empty<string>();
        }

        var orgUsers = dbContext.Users
            .Where(user =>
                user.OrganizationID == device.OrganizationID &&
                userIDs.Contains(user.Id));

        if (string.IsNullOrWhiteSpace(device.DeviceGroupID))
        {
            return orgUsers
                .Select(x => x.Id)
                .ToArray();
        }

        var allowedUsers = device?.DeviceGroup?.Users?.Select(x => x.Id) ?? Array.Empty<string>();

        return orgUsers
            .Where(user =>
                user.IsAdministrator ||
                allowedUsers.Contains(user.Id)
            )
            .Select(x => x.Id)
            .ToArray();
    }
}
