using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Services;
using Remotely.Shared;
using Remotely.Shared.Entities;

namespace Remotely.Server.API;

/// <summary>
/// Serves the video clips that were recorded from the cameras of remote
/// devices.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CamerasController : ControllerBase
{
    private readonly ICameraService _cameraService;
    private readonly IDataService _dataService;
    private readonly ILogger<CamerasController> _logger;
    private readonly IRemoteControlSessionCache _sessionCache;

    public CamerasController(
        ICameraService cameraService,
        IDataService dataService,
        IRemoteControlSessionCache sessionCache,
        ILogger<CamerasController> logger)
    {
        _cameraService = cameraService;
        _dataService = dataService;
        _sessionCache = sessionCache;
        _logger = logger;
    }

    /// <summary>
    /// Returns the metadata of the clips that were recorded from a device,
    /// newest first.
    /// </summary>
    [HttpGet("{deviceId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<CameraClip[]>> Get(string deviceId)
    {
        if (!await HasAccessToDevice(deviceId))
        {
            return Unauthorized();
        }

        return _cameraService.GetClips(deviceId, 100);
    }

    /// <summary>
    /// Returns the video of a single clip.
    /// </summary>
    [HttpGet("{deviceId}/{fileName}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> GetClip(string deviceId, string fileName)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        var path = _cameraService.GetClipPath(device, fileName);
        if (path is null)
        {
            return NotFound();
        }

        // Range requests are enabled, so the browser can seek in the clip.
        return PhysicalFile(path, "video/mp4", enableRangeProcessing: true);
    }

    /// <summary>
    /// Deletes a single clip of a device.
    /// </summary>
    [HttpDelete("{deviceId}/{fileName}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> DeleteClip(string deviceId, string fileName)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        _cameraService.DeleteClip(device, fileName);

        return Ok();
    }

    /// <summary>
    /// Deletes every clip that was recorded from a device.
    /// </summary>
    [HttpDelete("{deviceId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> ClearClips(string deviceId)
    {
        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return Unauthorized();
        }

        _cameraService.ClearClips(device);

        return Ok();
    }

    /// <summary>
    /// Receives a video clip that was recorded by a device.  The device
    /// authenticates itself with the access key of the session that the clip
    /// was requested in.
    /// </summary>
    [HttpPost("{deviceId}/upload")]
    [RequestSizeLimit(CameraService.MaxClipBytes)]
    public async Task<ActionResult> Upload(
        string deviceId,
        [FromQuery] string clipId,
        [FromQuery] int quality = 1,
        [FromQuery] int durationSeconds = 0,
        [FromQuery] string? cameraName = null)
    {
        if (!HasValidSessionKey(deviceId))
        {
            _logger.LogWarning(
                "A camera clip upload for device {deviceId} was refused because the session key was not valid.",
                deviceId);

            return Unauthorized();
        }

        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return NotFound();
        }

        var path = _cameraService.BeginUpload(device, clipId);
        if (path is null)
        {
            return BadRequest("The clip id is not valid.");
        }

        try
        {
            await using var body = Request.Body;
            await using var file = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            await body.CopyToAsync(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while receiving a camera clip from device {deviceId}.", deviceId);
            _cameraService.AbortUpload(clipId);
            return BadRequest("The clip could not be received.");
        }

        var clip = await _cameraService.CompleteUpload(
            device,
            clipId,
            quality.ToString(),
            durationSeconds,
            cameraName ?? string.Empty);

        if (clip is null)
        {
            return BadRequest("The clip could not be stored.");
        }

        _logger.LogInformation(
            "Received a camera clip from device {deviceId}.  Size: {size} bytes.",
            deviceId,
            clip.SizeBytes);

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
            _logger.LogError(ex, "Error while checking access to the camera clips of device {deviceId}.", deviceId);
            return false;
        }
    }

    /// <summary>
    /// Returns true when the request carries the access key of one of the
    /// camera sessions of the device.
    /// </summary>
    private bool HasValidSessionKey(string deviceId)
    {
        if (!Request.Headers.TryGetValue(AppConstants.SessionKeyHeaderName, out var keyValue))
        {
            return false;
        }

        var sessionKey = keyValue.ToString();
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return false;
        }

        var session = _sessionCache.Sessions
            .FirstOrDefault(x =>
                string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) &&
                x.IsCameraCapture &&
                !string.IsNullOrWhiteSpace(x.AccessKey) &&
                string.Equals(x.AccessKey, sessionKey, StringComparison.Ordinal));

        return session is not null;
    }
}
