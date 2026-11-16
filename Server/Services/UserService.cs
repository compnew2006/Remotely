using Microsoft.EntityFrameworkCore;
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

    Task<Result> CreateUser(string userEmail, bool isAdmin, string organizationId);

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

    public async Task<Result> CreateUser(string userEmail, bool isAdmin, string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        try
        {
            var user = new RemotelyUser()
            {
                UserName = userEmail.Trim().ToLower(),
                Email = userEmail.Trim().ToLower(),
                IsAdministrator = isAdmin,
                OrganizationID = organizationId,
                UserOptions = new RemotelyUserOptions(),
                LockoutEnabled = true
            };
            var org = dbContext.Organizations
                .Include(x => x.RemotelyUsers)
                .FirstOrDefault(x => x.ID == organizationId);

            if (org is null)
            {
                return Result.Fail("Organization not found.");
            }

            dbContext.Users.Add(user);
            org.RemotelyUsers.Add(user);
            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating user for organization {id}.", organizationId);
            return Result.Fail("An error occurred while creating user.");
        }
    }

    public async Task<Result> DeleteUser(string orgId, string targetUserId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = dbContext
            .Organizations
            .Include(x => x.RemotelyUsers)
            .FirstOrDefault(x => x.ID == orgId);

        if (org is null)
        {
            return Result.Fail("Organization not found.");
        }

        // All the joins are necessary for client-side cascade delete.
        // This method will be called rarely, so I'm not concerned
        // about the performance.
        var target = dbContext.Users
            .Include(x => x.DeviceGroups)
            .ThenInclude(x => x.Devices)
            .Include(x => x.Organization)
            .Include(x => x.Alerts)
            .Include(x => x.SavedScripts)
            .ThenInclude(x => x.ScriptRuns)
            .Include(x => x.SavedScripts)
            .ThenInclude(x => x.ScriptResults)
            .Include(x => x.ScriptSchedules)
            .ThenInclude(x => x.ScriptRuns)
            .ThenInclude(x => x.Results)
            .FirstOrDefault(x =>
                x.Id == targetUserId &&
                x.OrganizationID == orgId);

        if (target is null)
        {
            return Result.Fail("User not found.");
        }

        dbContext.Users.Remove(target);

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task ChangeUserIsAdmin(string organizationId, string targetUserId, bool isAdmin)
    {
        using var dbContext = _appDbFactory.GetContext();

        var targetUser = await dbContext.Users.FirstOrDefaultAsync(x =>
                            x.OrganizationID == organizationId &&
                            x.Id == targetUserId);

        if (targetUser != null)
        {
            targetUser.IsAdministrator = isAdmin;
            dbContext.SaveChanges();
        }
    }

    public async Task<Result<RemotelyUser>> GetUserByName(
        string userName,
        Action<IQueryable<RemotelyUser>>? queryBuilder = null)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail<RemotelyUser>("Username cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users
            .AsNoTracking()
            .Apply(queryBuilder)
            .FirstOrDefaultAsync(x =>
                x.UserName!.ToLower().Trim() == userName.ToLower().Trim());

        if (user is null)
        {
            return Result.Fail<RemotelyUser>("User not found.");
        }
        return Result.Ok(user);
    }

    public async Task<RemotelyUser[]> GetAllUsersInOrganization(string orgId)
    {
        if (string.IsNullOrWhiteSpace(orgId))
        {
            return Array.Empty<RemotelyUser>();
        }

        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .Include(x => x.RemotelyUsers)
            .FirstOrDefaultAsync(x => x.ID == orgId);

        if (organization is null)
        {
            return Array.Empty<RemotelyUser>();
        }

        return organization.RemotelyUsers.ToArray();
    }

    public RemotelyUser[] GetAllUsersForServer()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Users
            .AsNoTracking()
            .ToArray();
    }

    public bool DoesUserExist(string userName)
    {
        using var dbContext = _appDbFactory.GetContext();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        return dbContext.Users
            .Where(x => x.UserName != null)
            .Any(x => x.UserName!.Trim().ToLower() == userName.Trim().ToLower());
    }

    public bool DoesUserHaveAccessToDevice(string deviceId, RemotelyUser remotelyUser)
    {
        if (remotelyUser is null)
        {
            return false;
        }

        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .Include(x => x.DeviceGroup)
            .ThenInclude(x => x!.Users)
            .Any(device =>
                device.OrganizationID == remotelyUser.OrganizationID &&
                device.ID == deviceId &&
                (
                    remotelyUser.IsAdministrator ||
                    // A device without a group belongs to the administrators.
                    // The check has to be explicit: the null-forgiving
                    // operator hides the null from the compiler, but the
                    // in-memory provider evaluates the expression in memory
                    // and would throw on it.
                    (device.DeviceGroup != null &&
                        device.DeviceGroup.Users.Any(user => user.Id == remotelyUser.Id))
                ));
    }

    public string[] FilterDeviceIdsByUserPermission(string[] deviceIds, RemotelyUser remotelyUser)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .Include(x => x.DeviceGroup)
            .ThenInclude(x => x!.Users)
            .Where(device =>
                device.OrganizationID == remotelyUser.OrganizationID &&
                deviceIds.Contains(device.ID) &&
                (
                    remotelyUser.IsAdministrator ||
                    // A device without a group belongs to the administrators.
                    (device.DeviceGroup != null &&
                        device.DeviceGroup.Users.Any(user => user.Id == remotelyUser.Id))
                ))
            .Select(x => x.ID)
            .ToArray();
    }

    public string[] FilterUsersByDevicePermission(IEnumerable<string> userIds, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return DevicePermissionFilter.FilterUsersByDevicePermission(dbContext, userIds, deviceId);
    }

    public async Task<Result> UpdateUserOptions(string userName, RemotelyUserOptions options)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }
        user.UserOptions = options;
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task SetIsServerAdmin(string targetUserId, bool isServerAdmin, string callerUserId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var caller = await dbContext.Users.FindAsync(callerUserId);
        if (caller?.IsServerAdmin != true)
        {
            return;
        }

        var targetUser = await dbContext.Users.FindAsync(targetUserId);

        if (targetUser is null)
        {
            return;
        }

        if (caller.Id == targetUser.Id)
        {
            // A server admin can't change themselves.
            return;
        }

        targetUser.IsServerAdmin = isServerAdmin;
        await dbContext.SaveChangesAsync();
    }

    public List<string> GetServerAdmins()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsServerAdmin && x.UserName != null)
            .Select(x => x.UserName!)
            .ToList();
    }

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
}
