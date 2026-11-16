"""Assembles Server/Services/DeviceService.cs from the extracted method blocks."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEVICE = ROOT / "Server" / "Services" / "DeviceService.cs"

HEADER = '''using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;

namespace Remotely.Server.Services;

/// <summary>
/// The devices of the server: their registration, their settings, and their
/// online state.
/// <para>
/// These operations were part of the IDataService god interface.  They live
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

    Task<Result<Device>> UpdateDevice(DeviceSetupOptions deviceOptions, string organizationId);

    Task UpdateDevice(string deviceId, string? tag, string? alias, string? deviceGroupId, string? notes);

    void UpdateTags(string deviceID, string tags);

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

'''

FOOTER = "}\n"


def main():
    bodies = DEVICE.read_text(encoding="utf-8").strip("\n")
    DEVICE.write_text(HEADER + bodies + "\n" + FOOTER, encoding="utf-8")
    print("assembled", DEVICE)


if __name__ == "__main__":
    main()
