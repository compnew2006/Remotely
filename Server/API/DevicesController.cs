using Remotely.Shared.Extensions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;

namespace Remotely.Server.API;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceListingService _deviceListingService;
    private readonly IDeviceService _deviceService;
    private readonly IUserService _userService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDeviceListingService deviceListingService,
        IDeviceService deviceService,
        IUserService userService,
        ILogger<DevicesController> logger)
    {
        _deviceListingService = deviceListingService;
        _deviceService = deviceService;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public IEnumerable<Device> Get()
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Array.Empty<Device>();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return _deviceListingService.GetDevicesForUser($"{User.Identity.Name}");
        }

        // Authorized with API key.  Return all.
        return _deviceListingService.GetAllDevices(orgId);
    }

    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpGet("{id}")]
    public async Task<ActionResult<Device>> Get(string id)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _userService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            if (!_userService.DoesUserHaveAccessToDevice(id, userResult.Value))
            {
                return Unauthorized();
            }
        }

        var deviceResult = await _deviceService.GetDevice(orgId, id);
        _logger.LogResult(deviceResult);

        if (!deviceResult.IsSuccess)
        {
            return NotFound();
        }

        return deviceResult.Value;
    }

    [HttpPut]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Update([FromBody] DeviceSetupOptions deviceOptions)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }
        
        if (string.IsNullOrWhiteSpace(deviceOptions?.DeviceID))
        {
            return BadRequest("DeviceId is required.");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _userService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            if (!_userService.DoesUserHaveAccessToDevice(deviceOptions.DeviceID, userResult.Value))
            {
                return Unauthorized();
            }

        }

        var deviceResult = await _deviceService.UpdateDevice(deviceOptions, orgId);
        _logger.LogResult(deviceResult);

        if (!deviceResult.IsSuccess)
        {
            return BadRequest();
        }
        return Created(Request.GetDisplayUrl(), deviceResult.Value);
    }

    [HttpPost]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Create([FromBody] DeviceSetupOptions deviceOptions)
    {
        var result = await _deviceService.CreateDevice(deviceOptions);
        _logger.LogResult(result);

        if (!result.IsSuccess)
        {
            return BadRequest("Device already exists.  Use Put with authorization to update the device.");
        }
        return Created(Request.GetDisplayUrl(), result.Value);
    }
}
