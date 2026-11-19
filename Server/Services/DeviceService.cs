using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Server.Extensions;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Helpers;
using Remotely.Shared.Models;

namespace Remotely.Server.Services;

/// <summary>
/// The devices of the server: their registration, their settings, and their
/// online state.
/// <para>
/// These operations used to live in one god interface.  They live
/// here now, so the code that only touches devices depends on this narrow
/// interface instead of the whole data service.
/// </para>
/// </summary>
public interface IDeviceService
{
    Task<Result<Device>> AddOrUpdateDevice(DeviceClientDto device);

    Task<Result<Device>> CreateDevice(DeviceSetupOptions options);

    void DeviceDisconnected(string deviceId);

    Task<Result<Device>> GetDevice(
        string deviceId,
        Action<IQueryable<Device>>? queryBuilder = null);

    Task<Result<Device>> GetDevice(string orgId, string deviceId);

    int GetDeviceCount();

    int GetTotalDevices();

    void RemoveDevices(string[] deviceIds);

    Task SetAllDevicesNotOnline();

    void SetServerVerificationToken(string deviceId, string verificationToken);

    /// <summary>
    /// Stores the token of an agent that predates the credential header, in
    /// the clear, so that agent keeps verifying against this server.
    /// </summary>
    void SetLegacyServerVerificationToken(string deviceId, string verificationToken);

    Task<Result<Device>> UpdateDevice(DeviceSetupOptions deviceOptions, string organizationId);

    /// <summary>
    /// Verifies that the caller registering as <paramref name="deviceId"/> holds
    /// the credential bound to that device record.
    /// <para>
    /// A device record with no credential is trusted on first use: the
    /// credential the caller presents is bound to the record, and every later
    /// registration must present it.  A device record that already has a
    /// credential accepts only an exact match.
    /// </para>
    /// <para>
    /// The record holds the fingerprint of the credential, never the
    /// credential itself.  A record that was written before the
    /// fingerprinting still holds the credential in the clear; it is accepted
    /// and replaced by its fingerprint at the first registration that
    /// presents it.
    /// </para>
    /// <para>
    /// An agent that predates the credential header presents nothing.  Such a
    /// registration is accepted only while the record still holds its token in
    /// the clear, which is the state the agent needs, and the record is left
    /// alone so the agent keeps verifying.  A record that holds a fingerprint
    /// still refuses a registration without the credential.
    /// </para>
    /// </summary>
    Task<bool> VerifyDeviceVerificationToken(string deviceId, string? presentedToken);

    /// <summary>
    /// How many devices of this organization have no credential bound yet.
    /// <para>
    /// Such a device can still be claimed by the first caller that registers
    /// it, so the count is what the operator needs to see to know how much of
    /// the fleet is protected.
    /// </para>
    /// </summary>
    Task<int> GetDevicesWithoutCredentialCountAsync(string? organizationId = null);

    Task UpdateDevice(string deviceId, string? tag, string? alias, string? deviceGroupId, string? notes);

    Task UpdateTags(string deviceID, string tags);

    Task<Result> UpdateScreenshotSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null);

    Task<Result> UpdateCameraSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null);

    Task<Result> UpdateAudioSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null);

    Task<Result> UpdateKeyboardSettings(
        string deviceId,
        bool enabled,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null,
        bool? showPasswords = null);
}

public class DeviceService : IDeviceService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IHostEnvironment hostEnvironment,
        IAppDbFactory appDbFactory,
        ILogger<DeviceService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task<Result<Device>> AddOrUpdateDevice(DeviceClientDto deviceDto)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceDto.ID);

        if (device is null)
        {
            device = new Device
            {
                OrganizationID = deviceDto.OrganizationID,
                ID = deviceDto.ID,
            };
            await dbContext.Devices.AddAsync(device);
        }

        device.CurrentUser = deviceDto.CurrentUser;
        device.DeviceName = deviceDto.DeviceName;
        device.Drives = deviceDto.Drives;
        device.CpuUtilization = deviceDto.CpuUtilization;
        device.UsedMemory = deviceDto.UsedMemory;
        device.UsedStorage = deviceDto.UsedStorage;
        device.Is64Bit = deviceDto.Is64Bit;
        device.IsOnline = true;
        device.OSArchitecture = deviceDto.OSArchitecture;
        device.OSDescription = deviceDto.OSDescription;
        device.Platform = deviceDto.Platform;
        device.ProcessorCount = deviceDto.ProcessorCount;
        device.PublicIP = deviceDto.PublicIP;
        device.TotalMemory = deviceDto.TotalMemory;
        device.TotalStorage = deviceDto.TotalStorage;
        device.AgentVersion = deviceDto.AgentVersion;
        device.MacAddresses = deviceDto.MacAddresses ?? Array.Empty<string>();
        device.LastOnline = DateTimeOffset.Now;

        if (_hostEnvironment.IsDevelopment() && dbContext.Organizations.Any())
        {
            var org = await dbContext.Organizations.FirstAsync();
            device.Organization = org;
            device.OrganizationID = org.ID;
        }

        if (!await dbContext.Organizations.AnyAsync(x => x.ID == device.OrganizationID))
        {
            _logger.LogInformation(
                "Unable to add device {deviceName} because organization {organizationID}" +
                "does not exist.  Device ID: {ID}.",
                device.DeviceName,
                device.OrganizationID,
                device.ID);

            return Result.Fail<Device>("Organization does not exist.");
        }

        await dbContext.SaveChangesAsync();
        return Result.Ok(device);
    }

    public async Task<Result<Device>> CreateDevice(DeviceSetupOptions options)
    {
        using var dbContext = _appDbFactory.GetContext();

        try
        {
            if (options is null ||
                string.IsNullOrWhiteSpace(options.DeviceID) ||
                string.IsNullOrWhiteSpace(options.OrganizationID) ||
                dbContext.Devices.Any(x => x.ID == options.DeviceID))
            {
                return Result.Fail<Device>("Required parameters are missing or incorrect.");
            }

            var device = new Device()
            {
                ID = options.DeviceID,
                OrganizationID = options.OrganizationID
            };

            if (!string.IsNullOrWhiteSpace(options.DeviceAlias))
            {
                device.Alias = options.DeviceAlias;
            }

            if (!string.IsNullOrWhiteSpace(options.DeviceGroupName))
            {
                var group = dbContext.DeviceGroups.FirstOrDefault(x =>
                    x.Name.ToLower() == options.DeviceGroupName.ToLower() &&
                    x.OrganizationID == device.OrganizationID);
                device.DeviceGroup = group;
            }

            dbContext.Devices.Add(device);

            await dbContext.SaveChangesAsync();

            return Result.Ok(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating device for organization {id}.", options.OrganizationID);
            return Result.Fail<Device>("An error occurred while creating the device.");
        }
    }

    public void DeviceDisconnected(string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = dbContext.Devices.Find(deviceId);
        if (device != null)
        {
            device.LastOnline = DateTimeOffset.Now;
            device.IsOnline = false;
            dbContext.SaveChanges();
        }
    }

    public async Task<Result<Device>> GetDevice(
        string deviceId,
        Action<IQueryable<Device>>? queryBuilder = null)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .AsNoTracking()
            .Apply(queryBuilder)
            .FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return Result.Fail<Device>("Device not found.");
        }
        return Result.Ok(device);
    }

    public async Task<Result<Device>> GetDevice(string orgId, string deviceId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OrganizationID == orgId &&
                x.ID == deviceId);

        if (device is null)
        {
            return Result.Fail<Device>("Device not found.");
        }
        return Result.Ok(device);
    }

    public int GetDeviceCount()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices.Count();
    }

    public int GetTotalDevices()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Devices.Count();
    }

    public void RemoveDevices(string[] deviceIDs)
    {
        using var dbContext = _appDbFactory.GetContext();

        var devices = dbContext.Devices
            .Include(x => x.ScriptResults)
            .Include(x => x.ScriptRuns)
            .Include(x => x.ScriptSchedules)
            .Include(x => x.DeviceGroup)
            .Include(x => x.Alerts)
            .Where(x => deviceIDs.Contains(x.ID));

        dbContext.Devices.RemoveRange(devices);
        dbContext.SaveChanges();
    }

    public async Task SetAllDevicesNotOnline()
    {
        using var dbContext = _appDbFactory.GetContext();

        await dbContext.Devices.ForEachAsync(x =>
        {
            x.IsOnline = false;
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> VerifyDeviceVerificationToken(string deviceId, string? presentedToken)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceId);

        if (device is null)
        {
            // This device has never been registered.  There is no identity to
            // prove yet; the registration that follows creates the record and
            // binds the credential that every later registration must present.
            return true;
        }

        var storedValue = device.ServerVerificationToken;

        if (string.IsNullOrWhiteSpace(storedValue))
        {
            if (!string.IsNullOrWhiteSpace(presentedToken))
            {
                // A device whose record predates the credential binding (or
                // whose record was restored) binds the credential it already
                // holds.  This does not widen authority: an unbound record
                // could be claimed by the first caller either way.
                await BindCredentialAsync(dbContext, device, presentedToken, deviceId);
            }

            return true;
        }

        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            // Agents that predate the credential header send no token at all.
            // A record that still holds the credential in the clear is exactly
            // such an agent: the server returns that same value to it on
            // demand, and the agent compares it with the GUID it holds.  The
            // record is accepted without being upgraded, because the
            // fingerprint would break the agent that owns it.  A record that
            // holds a fingerprint has no such agent behind it, so a
            // registration without a credential is still refused.
            return !DeviceCredential.IsFingerprint(storedValue);
        }

        if (!DeviceCredential.Matches(presentedToken, storedValue))
        {
            return false;
        }

        if (!DeviceCredential.IsFingerprint(storedValue))
        {
            // The record still holds the credential in the clear.  Now that
            // the caller has proved that it holds it, only the fingerprint is
            // kept from now on.
            await BindCredentialAsync(dbContext, device, presentedToken, deviceId);
        }

        return true;
    }

    /// <summary>
    /// Binds the fingerprint of the credential to the device record.  The
    /// credential itself is never written to the database.
    /// </summary>
    private async Task BindCredentialAsync(
        AppDb dbContext,
        Device device,
        string credential,
        string deviceId)
    {
        device.ServerVerificationToken = DeviceCredential.Fingerprint(credential);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Bound the device credential on the registration.  Device ID: {deviceId}.",
            deviceId);
    }

    public async Task<int> GetDevicesWithoutCredentialCountAsync(string? organizationId = null)
    {
        using var dbContext = _appDbFactory.GetContext();

        var storedValues = await dbContext.Devices
            .AsNoTracking()
            .Where(x => organizationId == null || x.OrganizationID == organizationId)
            .Select(x => x.ServerVerificationToken)
            .ToListAsync();

        // A token in the clear belongs to an agent that predates the
        // credential header.  It is no more of an identity than an empty
        // record, so it is counted the same way.
        return storedValues.Count(x => !DeviceCredential.IsFingerprint(x));
    }

    public void SetServerVerificationToken(string deviceID, string verificationToken)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = dbContext.Devices.Find(deviceID);
        if (device != null)
        {
            // Only the fingerprint is kept.  The credential itself stays on
            // the device, so a copy of the database cannot be used to
            // impersonate a device.
            device.ServerVerificationToken = DeviceCredential.Fingerprint(verificationToken);
            dbContext.SaveChanges();
        }
    }

    /// <summary>
    /// Stores the token of an agent that predates the credential header, in
    /// the clear.
    /// <para>
    /// Such an agent compares the value the server returns with the GUID it
    /// holds, so a fingerprint would lock it out of the server it belongs to.
    /// Only a connection that presented no credential reaches this path; see
    /// <see cref="VerifyDeviceVerificationToken"/>.  Every agent that presents
    /// a credential keeps the fingerprint-only record.
    /// </para>
    /// </summary>
    public void SetLegacyServerVerificationToken(string deviceID, string verificationToken)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = dbContext.Devices.Find(deviceID);
        if (device != null)
        {
            device.ServerVerificationToken = verificationToken;
            dbContext.SaveChanges();
        }
    }

    public async Task<Result<Device>> UpdateDevice(DeviceSetupOptions deviceOptions, string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceOptions.DeviceID);
        if (device == null || device.OrganizationID != organizationId)
        {
            return Result.Fail<Device>("Device not found.");
        }

        var group = await dbContext.DeviceGroups.FirstOrDefaultAsync(x =>
          x.Name.ToLower() == $"{deviceOptions.DeviceGroupName}".ToLower() &&
          x.OrganizationID == device.OrganizationID);
        device.DeviceGroup = group;

        device.Alias = deviceOptions.DeviceAlias;
        await dbContext.SaveChangesAsync();
        return Result.Ok(device);
    }

    public async Task UpdateDevice(string deviceId, string? tag, string? alias, string? deviceGroupId, string? notes)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices
            .Include(x => x.DeviceGroup)
            .FirstOrDefaultAsync(x => x.ID == deviceId);

        if (device is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(deviceGroupId))
        {
            device.DeviceGroup?.Devices?.RemoveAll(x => x.ID == deviceId);
            device.DeviceGroup = null;
            device.DeviceGroupID = null;
        }
        else
        {
            device.DeviceGroupID = deviceGroupId;
        }

        device.Tags = tag;
        device.Alias = alias;
        device.Notes = notes;
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateTags(string deviceID, string tags)
    {
        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceID);
        if (device == null)
        {
            return;
        }

        device.Tags = tags;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Updates where the screenshots of a device are stored, and how many of
    /// them are kept.
    /// </summary>
    public async Task<Result> UpdateScreenshotSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null)
    {
        directory = string.IsNullOrWhiteSpace(directory) ? null : directory.Trim();
        pauseWindows = string.IsNullOrWhiteSpace(pauseWindows) ? null : pauseWindows;

        if (directory?.Length > 500)
        {
            return Result.Fail("The screenshot directory must be 500 characters or fewer.");
        }

        if (pauseWindows?.Length > 500)
        {
            return Result.Fail("The screenshot pause windows exceed the storage limit.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device is null)
        {
            return Result.Fail("Device not found.");
        }

        var screenshotDirectories = new[]
        {
            string.IsNullOrWhiteSpace(device.ScreenshotDirectory) ?
                Path.Combine(_hostEnvironment.ContentRootPath, "AppData", "Screenshots", device.ID) :
                device.ScreenshotDirectory,
            Path.Combine(_hostEnvironment.ContentRootPath, "AppData", "Screenshots", device.ID)
        };

        var legacyScreenshots = await dbContext.Screenshots
            .Where(x => x.DeviceID == device.ID && x.StorageDirectory == null)
            .ToListAsync();

        foreach (var screenshot in legacyScreenshots)
        {
            screenshot.StorageDirectory = FindExistingMediaDirectory(
                screenshot.FileName,
                screenshotDirectories);
        }

        device.ScreenshotDirectory = directory;
        device.ScreenshotRetentionCount = retentionCount > 0 ? retentionCount : 200;
        device.ScreenshotRetentionHours = retentionHours > 0 ? retentionHours : 24;
        device.ScreenshotPauseWindows = pauseWindows;

        await dbContext.SaveChangesAsync();

        return Result.Ok();
    }

    /// <summary>
    /// Updates where the camera clips of a device are stored, how many are
    /// kept, and the windows during which scheduled recordings are paused.
    /// </summary>
    public async Task<Result> UpdateCameraSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null)
    {
        directory = string.IsNullOrWhiteSpace(directory) ? null : directory.Trim();
        pauseWindows = string.IsNullOrWhiteSpace(pauseWindows) ? null : pauseWindows;

        if (directory?.Length > 500)
        {
            return Result.Fail("The camera directory must be 500 characters or fewer.");
        }

        if (pauseWindows?.Length > 500)
        {
            return Result.Fail("The camera pause windows exceed the storage limit.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device is null)
        {
            return Result.Fail("Device not found.");
        }

        var cameraDirectories = new[]
        {
            string.IsNullOrWhiteSpace(device.CameraDirectory) ?
                Path.Combine(_hostEnvironment.ContentRootPath, "AppData", "Camera", device.ID) :
                device.CameraDirectory,
            Path.Combine(_hostEnvironment.ContentRootPath, "AppData", "Camera", device.ID)
        };

        var legacyClips = await dbContext.CameraClips
            .Where(x => x.DeviceID == device.ID && x.StorageDirectory == null)
            .ToListAsync();

        foreach (var clip in legacyClips)
        {
            clip.StorageDirectory = FindExistingMediaDirectory(
                clip.FileName,
                cameraDirectories);
        }

        device.CameraDirectory = directory;
        device.CameraRetentionCount = retentionCount > 0 ? Math.Clamp(retentionCount, 1, 2000) : 200;
        device.CameraRetentionHours = retentionHours > 0 ? Math.Clamp(retentionHours, 1, 8760) : 24;
        device.CameraPauseWindows = pauseWindows;

        await dbContext.SaveChangesAsync();

        return Result.Ok();
    }

    /// <summary>
    /// Updates where the audio clips of a device are stored, how many are
    /// kept, and the windows during which scheduled recordings are paused.
    /// </summary>
    public async Task<Result> UpdateAudioSettings(
        string deviceId,
        string? directory,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null)
    {
        directory = string.IsNullOrWhiteSpace(directory) ? null : directory.Trim();
        pauseWindows = string.IsNullOrWhiteSpace(pauseWindows) ? null : pauseWindows;

        if (directory?.Length > 500)
        {
            return Result.Fail("The audio directory must be 500 characters or fewer.");
        }

        if (pauseWindows?.Length > 500)
        {
            return Result.Fail("The audio pause windows exceed the storage limit.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device is null)
        {
            return Result.Fail("Device not found.");
        }

        var audioDirectories = new[]
        {
            string.IsNullOrWhiteSpace(device.AudioDirectory) ?
                Path.Combine(_hostEnvironment.ContentRootPath, "AppData", "Audio", device.ID) :
                device.AudioDirectory,
            Path.Combine(_hostEnvironment.ContentRootPath, "AppData", "Audio", device.ID)
        };

        var legacyClips = await dbContext.AudioClips
            .Where(x => x.DeviceID == device.ID && x.StorageDirectory == null)
            .ToListAsync();

        foreach (var clip in legacyClips)
        {
            clip.StorageDirectory = FindExistingMediaDirectory(
                clip.FileName,
                audioDirectories);
        }

        device.AudioDirectory = directory;
        device.AudioRetentionCount = retentionCount > 0 ? Math.Clamp(retentionCount, 1, 2000) : 200;
        device.AudioRetentionHours = retentionHours > 0 ? Math.Clamp(retentionHours, 1, 8760) : 24;
        device.AudioPauseWindows = pauseWindows;

        await dbContext.SaveChangesAsync();

        return Result.Ok();
    }

    /// <summary>
    /// Updates whether the text that is typed on a device is captured, how
    /// many of the logs are kept, and how long they are kept.
    /// </summary>
    public async Task<Result> UpdateKeyboardSettings(
        string deviceId,
        bool enabled,
        int retentionCount,
        int retentionHours,
        string? pauseWindows = null,
        bool? showPasswords = null)
    {
        pauseWindows = string.IsNullOrWhiteSpace(pauseWindows) ? null : pauseWindows;

        if (pauseWindows?.Length > 500)
        {
            return Result.Fail("The keyboard pause windows exceed the storage limit.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device is null)
        {
            return Result.Fail("Device not found.");
        }

        device.KeyboardEnabled = enabled;
        device.KeyboardRetentionCount = retentionCount > 0 ? Math.Clamp(retentionCount, 1, 20000) : 500;
        device.KeyboardRetentionHours = retentionHours > 0 ? Math.Clamp(retentionHours, 1, 8760) : 24;
        device.KeyboardPauseWindows = pauseWindows;

        if (showPasswords.HasValue)
        {
            device.KeyboardShowPasswords = showPasswords.Value;
        }

        await dbContext.SaveChangesAsync();

        return Result.Ok();
    }

    private static string? FindExistingMediaDirectory(
        string fileName,
        IEnumerable<string> candidateDirectories)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName != Path.GetFileName(fileName) ||
            fileName.Contains(".."))
        {
            return null;
        }

        foreach (var directory in candidateDirectories.Distinct(GetPathComparer()))
        {
            try
            {
                var fullDirectory = Path.GetFullPath(directory);
                if (File.Exists(Path.Combine(fullDirectory, fileName)))
                {
                    return fullDirectory;
                }
            }
            catch
            {
                // A path that cannot be checked is not safe to assign to a legacy row.
            }
        }

        return null;
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
