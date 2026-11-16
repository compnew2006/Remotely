"""Assembles the user, invite, and device group services from the extracted bodies."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Utilities" / "tools" / "_extracted"
SERVICES = ROOT / "Server" / "Services"

USER_HEADER = '''using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Server.Extensions;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;

namespace Remotely.Server.Services;

/// <summary>
/// The users of the server: their account, their roles, and the devices they
/// are allowed to see.
/// </summary>
public interface IUserService
{
    Task ChangeUserIsAdmin(string organizationId, string targetUserId, bool isAdmin);

    Task<Result<RemotelyUser>> CreateUser(string userEmail, bool isAdmin, string organizationId);

    Task<Result> DeleteUser(string orgId, string targetUserId);

    bool DoesUserExist(string userName);

    bool DoesUserHaveAccessToDevice(string deviceId, RemotelyUser remotelyUser);

    bool DoesUserHaveAccessToDevice(string deviceId, string remotelyUserId);

    string[] FilterDeviceIdsByUserPermission(string[] deviceIds, RemotelyUser remotelyUser);

    string[] FilterUsersByDevicePermission(IEnumerable<string> userIds, string deviceId);

    RemotelyUser[] GetAllUsersForServer();

    Task<RemotelyUser[]> GetAllUsersInOrganization(string orgId);

    List<string> GetServerAdmins();

    Task<Result<RemotelyUser>> GetUserByName(
        string userName,
        Action<IQueryable<RemotelyUser>>? queryBuilder = null);

    Task SetIsServerAdmin(string targetUserId, bool isServerAdmin, string callerUserId);

    Task<Result> UpdateUserOptions(string userName, RemotelyUserOptions options);
}

public class UserService : IUserService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IAppDbFactory appDbFactory,
        ILogger<UserService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

'''

USER_EXTRA = '''
    public bool DoesUserHaveAccessToDevice(string deviceId, string remotelyUserId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var remotelyUser = dbContext.Users.Find(remotelyUserId);

        if (remotelyUser is null)
        {
            return false;
        }

        return DoesUserHaveAccessToDevice(deviceId, remotelyUser);
    }
'''

INVITE_HEADER = '''using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;
using Remotely.Shared.ViewModels;

namespace Remotely.Server.Services;

/// <summary>
/// The invitation links that let a user join an organization.
/// </summary>
public interface IInviteService
{
    Task<Result<InviteLink>> AddInvite(string orgId, InviteViewModel invite);

    Task<Result> DeleteInvite(string orgId, string inviteId);

    InviteLink[] GetAllInviteLinks(string organizationId);

    Task<Result> JoinViaInvitation(string userName, string inviteId);
}

public class InviteService : IInviteService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<InviteService> _logger;

    public InviteService(
        IAppDbFactory appDbFactory,
        ILogger<InviteService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

'''

GROUP_HEADER = '''using Microsoft.EntityFrameworkCore;
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

'''


def main():
    user_bodies = (OUT / "UserService.bodies.txt").read_text(encoding="utf-8").strip("\n")
    invite_bodies = (OUT / "InviteService.bodies.txt").read_text(encoding="utf-8").strip("\n")
    group_bodies = (OUT / "DeviceGroupService.bodies.txt").read_text(encoding="utf-8").strip("\n")

    (SERVICES / "UserService.cs").write_text(
        USER_HEADER + user_bodies + "\n" + USER_EXTRA + "}\n", encoding="utf-8")
    (SERVICES / "InviteService.cs").write_text(
        INVITE_HEADER + invite_bodies + "\n}\n", encoding="utf-8")
    (SERVICES / "DeviceGroupService.cs").write_text(
        GROUP_HEADER + group_bodies + "\n}\n", encoding="utf-8")

    print("assembled the three services")


if __name__ == "__main__":
    main()
