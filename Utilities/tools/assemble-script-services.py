"""Assembles the script, file, listing, membership, and maintenance services."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Utilities" / "tools" / "_extracted"
SERVICES = ROOT / "Server" / "Services"

HEADERS = {}

HEADERS["SavedScriptService"] = '''using Microsoft.EntityFrameworkCore;
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

'''

HEADERS["ScriptScheduleService"] = '''using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

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

'''

HEADERS["ScriptResultService"] = '''using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The runs of the scripts and their results.
/// </summary>
public interface IScriptResultService
{
    Task<Result<ScriptResult>> AddScriptResult(ScriptResultDto dto);

    Task<Result> AddScriptResultToScriptRun(string scriptResultId, int scriptRunId);

    Task AddScriptRun(ScriptRun scriptRun);

    ScriptResult[] GetAllCommandResults(string orgId);

    ScriptResult[] GetAllCommandResultsForUser(string orgId, string userName, string deviceId);

    ScriptResult[] GetAllScriptResults(string orgId, string deviceId);

    Task<IEnumerable<ScriptRun>> GetPendingScriptRuns(string deviceId);

    Task<Result<ScriptResult>> GetScriptResult(string resultId);

    Task<Result<ScriptResult>> GetScriptResult(string resultId, string orgId);
}

public class ScriptResultService : IScriptResultService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<ScriptResultService> _logger;

    public ScriptResultService(
        IAppDbFactory appDbFactory,
        ILogger<ScriptResultService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

'''

HEADERS["SharedFileService"] = '''using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The files that the users share with each other.
/// </summary>
public interface ISharedFileService
{
    Task<string> AddSharedFile(IBrowserFile file, string organizationId, Action<double, string> progressCallback);

    Task<string> AddSharedFile(IFormFile file, string organizationId);

    Task<Result<SharedFile>> GetSharedFiled(string fileId);
}

public class SharedFileService : ISharedFileService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<SharedFileService> _logger;

    public SharedFileService(
        IAppDbFactory appDbFactory,
        ILogger<SharedFileService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

'''

HEADERS["DeviceListingService"] = '''using Microsoft.EntityFrameworkCore;
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

'''

HEADERS["DeviceGroupMembershipService"] = '''using Microsoft.EntityFrameworkCore;
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

'''

HEADERS["MaintenanceService"] = '''using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;

namespace Remotely.Server.Services;

/// <summary>
/// The periodic cleanup of the records that the server keeps for a while.
/// </summary>
public interface IMaintenanceService
{
    Task CleanupOldRecords();
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        IAppDbFactory appDbFactory,
        ILogger<MaintenanceService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

'''


def main():
    for name, header in HEADERS.items():
        bodies = (OUT / (name + ".bodies.txt")).read_text(encoding="utf-8").strip("\n")
        (SERVICES / (name + ".cs")).write_text(header + bodies + "\n}\n", encoding="utf-8")

    print("assembled", len(HEADERS), "services")


if __name__ == "__main__":
    main()
