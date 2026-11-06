using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.API;

/// <summary>
/// Serves the screenshots that were captured by monitored devices.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ScreenshotsController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly ILogger<ScreenshotsController> _logger;
    private readonly IScreenshotService _screenshotService;

    public ScreenshotsController(
        IDataService dataService,
        IScreenshotService screenshotService,
        ILogger<ScreenshotsController> logger)
    {
        _dataService = dataService;
        _screenshotService = screenshotService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the metadata of the screenshots that were captured from a
    /// device, newest first.
    /// </summary>
    [HttpGet("{deviceId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<Screenshot[]>> Get(string deviceId)
    {
        if (!await HasAccessToDevice(deviceId))
        {
            return Unauthorized();
        }

        return _screenshotService.GetScreenshots(deviceId, 100);
    }

    /// <summary>
    /// Returns the image of a single screenshot.
    /// </summary>
    [HttpGet("{deviceId}/{fileName}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> GetScreenshot(string deviceId, string fileName)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        var contents = _screenshotService.GetScreenshotContents(device, fileName);
        if (contents is null)
        {
            return NotFound();
        }

        return File(contents, "image/jpeg");
    }

    /// <summary>
    /// Deletes a single screenshot of a device.
    /// </summary>
    [HttpDelete("{deviceId}/{fileName}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> DeleteScreenshot(string deviceId, string fileName)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        _screenshotService.DeleteScreenshot(device, fileName);

        return Ok();
    }

    /// <summary>
    /// Deletes every screenshot that was captured from a device.
    /// </summary>
    [HttpDelete("{deviceId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> ClearScreenshots(string deviceId)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        _screenshotService.ClearScreenshots(device);

        return Ok();
    }

    /// <summary>
    /// Lists the folders on the server, so that the folder where the
    /// screenshots are stored can be picked.
    /// </summary>
    [HttpGet("browse")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public ActionResult<DirectoryListing> Browse([FromQuery] string? path)
    {
        return _screenshotService.BrowseDirectory(path);
    }

    /// <summary>
    /// Changes where the screenshots of a device are stored, and how many of
    /// them are kept.
    /// </summary>
    [HttpPut("{deviceId}/settings")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> UpdateSettings(
        string deviceId,
        [FromQuery] string? directory,
        [FromQuery] int retentionCount = 200,
        [FromQuery] int retentionHours = 24)
    {
        if (!await HasAccessToDevice(deviceId))
        {
            return Unauthorized();
        }

        var result = await _dataService.UpdateScreenshotSettings(
            deviceId,
            directory,
            retentionCount,
            retentionHours);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Reason);
        }

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
            _logger.LogError(ex, "Error while checking access to screenshots of device {deviceId}.", deviceId);
            return false;
        }
    }
}
