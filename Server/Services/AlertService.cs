using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The alerts of the server.  An alert is raised for a device and reaches
/// the users that are allowed to see that device.
/// </summary>
public interface IAlertService
{
    Task AddAlert(string deviceId, string organizationId, string alertMessage, string? details = null);

    Task DeleteAlert(Alert alert);

    /// <summary>
    /// Removes every alert of the organization.  This is the operation of an
    /// administrator and of an API key that holds the organization.
    /// </summary>
    Task DeleteAllAlerts(string orgId);

    /// <summary>
    /// Removes the alerts of one user.  Nothing is removed when the user
    /// cannot be resolved: a name that does not match a user must never fall
    /// back to the whole organization.
    /// </summary>
    Task DeleteAllAlertsForUser(string orgId, string? userName);

    Task<Result<Alert>> GetAlert(string alertId);

    Alert[] GetAlerts(string userId);
}

public class AlertService : IAlertService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<AlertService> _logger;

    public AlertService(
        IAppDbFactory appDbFactory,
        ILogger<AlertService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task AddAlert(string deviceId, string organizationId, string alertMessage, string? details = null)
    {
        using var dbContext = _appDbFactory.GetContext();

        // The alerts table has a foreign key to the devices, and the column
        // cannot be empty.  An alert without a device, or for a device that
        // does not exist, therefore cannot be stored.  The database used to
        // raise the constraint error; the caller gets a warning instead.
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _logger.LogWarning(
                "An alert without a device was refused.  Message: {message}",
                alertMessage);

            return;
        }

        if (!await dbContext.Devices.AnyAsync(x => x.ID == deviceId))
        {
            _logger.LogWarning(
                "An alert for the unknown device {deviceId} was refused.",
                deviceId);

            return;
        }

        var users = dbContext.Users
           .Include(x => x.Alerts)
           .Where(x => x.OrganizationID == organizationId);

        var filteredUserIDs = DevicePermissionFilter.FilterUsersByDevicePermission(
            dbContext,
            users.Select(x => x.Id),
            deviceId);

        users = users.Where(x => filteredUserIDs.Contains(x.Id));

        await users.ForEachAsync(x =>
        {
            var alert = new Alert()
            {
                CreatedOn = DateTimeOffset.Now,
                DeviceID = deviceId,
                Message = alertMessage,
                OrganizationID = organizationId,
                Details = details
            };
            x.Alerts ??= new List<Alert>();
            x.Alerts.Add(alert);
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAlert(Alert alert)
    {
        using var dbContext = _appDbFactory.GetContext();

        dbContext.Alerts.Remove(alert);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAllAlerts(string orgId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var alerts = dbContext.Alerts.Where(x => x.OrganizationID == orgId);

        dbContext.Alerts.RemoveRange(alerts);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAllAlertsForUser(string orgId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            _logger.LogWarning(
                "The alerts of a user were not removed, because no user name was given.  " +
                "Organization: {orgId}.",
                orgId);

            return;
        }

        using var dbContext = _appDbFactory.GetContext();

        // The lookup is the same one that IUserService.GetUserByName does:
        // the name is compared without regard to case and without the
        // surrounding whitespace, so the result does not depend on the
        // collation of the database.
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.UserName!.ToLower().Trim() == userName.ToLower().Trim());

        if (user is null)
        {
            // Failing closed: the name did not resolve to a user, so the
            // alerts of the organization must not be touched.  The old code
            // fell through to the organization and removed the alerts of
            // every user in it.
            _logger.LogWarning(
                "The alerts of the user {userName} were not removed, because no such " +
                "user exists in the organization {orgId}.",
                userName,
                orgId);

            return;
        }

        var alerts = dbContext.Alerts.Where(x =>
            x.OrganizationID == orgId && x.UserID == user.Id);

        dbContext.Alerts.RemoveRange(alerts);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Result<Alert>> GetAlert(string alertId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var alert = await dbContext.Alerts
            .AsNoTracking()
            .Include(x => x.Device)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.ID == alertId);

        if (alert is null)
        {
            return Result.Fail<Alert>("Alert not found.");
        }

        return Result.Ok(alert);
    }

    public Alert[] GetAlerts(string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Alerts
            .AsNoTracking()
            .Include(x => x.Device)
            .Include(x => x.User)
            .Where(x => x.UserID == userId)
            .OrderByDescending(x => x.CreatedOn)
            .ToArray();
    }
}
