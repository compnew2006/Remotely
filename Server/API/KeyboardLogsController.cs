using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.API;

/// <summary>
/// Serves the text that was captured on remote devices.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class KeyboardLogsController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly IKeyboardLogService _keyboardLogService;
    private readonly ILogger<KeyboardLogsController> _logger;

    public KeyboardLogsController(
        IDataService dataService,
        IKeyboardLogService keyboardLogService,
        ILogger<KeyboardLogsController> logger)
    {
        _dataService = dataService;
        _keyboardLogService = keyboardLogService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the captured text of a device, newest first.
    /// </summary>
    [HttpGet("{deviceId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<KeyboardLog[]>> Get(string deviceId)
    {
        if (!await HasAccessToDevice(deviceId))
        {
            return Unauthorized();
        }

        return _keyboardLogService.GetLogs(deviceId, 200);
    }

    /// <summary>
    /// Deletes a single captured text.
    /// </summary>
    [HttpDelete("{deviceId}/{logId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> DeleteLog(string deviceId, string logId)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        _keyboardLogService.DeleteLog(device, logId);

        return Ok();
    }

    /// <summary>
    /// Deletes every captured text of a device.
    /// </summary>
    [HttpDelete("{deviceId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> ClearLogs(string deviceId)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        _keyboardLogService.ClearLogs(device);

        return Ok();
    }

    private async Task<Device?> GetDevice(string deviceId)
    {
        if (!await HasAccessToDevice(deviceId))
        {
            return null;
        }

        var deviceResult = await _dataService.GetDevice(deviceId);
        if (!deviceResult.IsSuccess)
        {
            return null;
        }

        return deviceResult.Value;
    }

    private async Task<bool> HasAccessToDevice(string deviceId)
    {
        try
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (!userResult.IsSuccess)
            {
                return false;
            }

            return _dataService.DoesUserHaveAccessToDevice(deviceId, userResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking access to the keyboard logs of device {deviceId}.", deviceId);
            return false;
        }
    }
}
