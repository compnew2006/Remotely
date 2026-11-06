using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Services;
using Remotely.Shared;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;

namespace Remotely.Server.API;

/// <summary>
/// Serves the audio clips that were recorded from remote devices.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AudioClipsController : ControllerBase
{
    private readonly IAudioService _audioService;
    private readonly IDataService _dataService;
    private readonly ILogger<AudioClipsController> _logger;
    private readonly IRemoteControlSessionCache _sessionCache;

    public AudioClipsController(
        IAudioService audioService,
        IDataService dataService,
        IRemoteControlSessionCache sessionCache,
        ILogger<AudioClipsController> logger)
    {
        _audioService = audioService;
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
    public async Task<ActionResult<AudioClip[]>> Get(string deviceId)
    {
        if (!await HasAccessToDevice(deviceId))
        {
            return Unauthorized();
        }

        return _audioService.GetClips(deviceId, 100);
    }

    /// <summary>
    /// Returns a single clip.
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

        var path = _audioService.GetClipPath(device, fileName);
        if (path is null)
        {
            return NotFound();
        }

        // Range requests are enabled, so the browser can seek in the clip.
        return PhysicalFile(path, "audio/wav", enableRangeProcessing: true);
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

        _audioService.DeleteClip(device, fileName);

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

        _audioService.ClearClips(device);

        return Ok();
    }

    /// <summary>
    /// Receives an audio clip that was recorded by a device.  The device
    /// authenticates itself with the access key of the session that the clip
    /// was requested in.
    /// </summary>
    [HttpPost("{deviceId}/upload")]
    [RequestSizeLimit(AudioService.MaxClipBytes)]
    public async Task<ActionResult> Upload(
        string deviceId,
        [FromQuery] string clipId,
        [FromQuery] int quality = (int)AudioQuality.Medium,
        [FromQuery] int durationSeconds = 0,
        [FromQuery] int source = (int)AudioSource.SystemAudio)
    {
        if (!HasValidSessionKey(deviceId))
        {
            _logger.LogWarning(
                "An audio clip upload for device {deviceId} was refused because the session key was not valid.",
                deviceId);

            return Unauthorized();
        }

        var device = await GetDevice(deviceId);
        if (device is null)
        {
            return NotFound();
        }

        var path = _audioService.BeginUpload(device, clipId);
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
            _logger.LogError(ex, "Error while receiving an audio clip from device {deviceId}.", deviceId);
            _audioService.AbortUpload(clipId);
            return BadRequest("The clip could not be received.");
        }

        var clip = await _audioService.CompleteUpload(
            device,
            clipId,
            (AudioQuality)quality,
            durationSeconds,
            (AudioSource)source);

        if (clip is null)
        {
            return BadRequest("The clip could not be stored.");
        }

        _logger.LogInformation(
            "Received an audio clip from device {deviceId}.  Size: {size} bytes.",
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
            _logger.LogError(ex, "Error while checking access to the audio clips of device {deviceId}.", deviceId);
            return false;
        }
    }

    /// <summary>
    /// Returns true when the request carries the access key of one of the
    /// audio sessions of the device.
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
                x.IsAudioCapture &&
                !x.IsAudioLive &&
                !string.IsNullOrWhiteSpace(x.AccessKey) &&
                string.Equals(x.AccessKey, sessionKey, StringComparison.Ordinal));

        return session is not null;
    }
}
