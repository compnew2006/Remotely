using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The listings of the devices: by organization, by id, and by the devices a
/// user is allowed to see.
/// </summary>
public interface IDeviceListingService
{
    Device[] GetAllDevices(string orgId);

    List<Device> GetDevices(IEnumerable<string> deviceIds);

    Device[] GetDevicesForUser(string userName);
}

public class DeviceListingService : IDeviceListingService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<DeviceListingService> _logger;

    public DeviceListingService(
        IAppDbFactory appDbFactory,
        ILogger<DeviceListingService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public Device[] GetAllDevices(string orgId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .AsNoTracking()
            .Where(x => x.OrganizationID == orgId)
            .ToArray();
    }

    public List<Device> GetDevices(IEnumerable<string> deviceIds)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices
            .AsNoTracking()
            .Where(x => deviceIds.Contains(x.ID))
            .ToList();
    }

    public Device[] GetDevicesForUser(string userName)
    {
        using var dbContext = _appDbFactory.GetContext();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return Array.Empty<Device>();
        }

        var user = dbContext.Users
            .AsNoTracking()
            .FirstOrDefault(x => x.UserName == userName);

        if (user is null)
        {
            return Array.Empty<Device>();
        }

        if (user.IsAdministrator)
        {
            return dbContext.Devices
                .AsNoTracking()
                .Where(x => x.OrganizationID == user.OrganizationID)
                .ToArray();
        }

        return dbContext.Users
            .AsNoTracking()
            .Include(x => x.DeviceGroups)
            .ThenInclude(x => x.Devices)
            .Where(x => x.UserName == userName)
            .SelectMany(x => x.DeviceGroups)
            .SelectMany(x => x.Devices)
            .ToArray();
    }
}
