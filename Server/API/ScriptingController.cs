using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Hubs;
using Remotely.Server.Services;
using Remotely.Shared.Utilities;
using Remotely.Shared.Enums;
using Remotely.Server.Auth;
using Remotely.Shared.Helpers;
using Remotely.Shared;
using Remotely.Server.Extensions;
using Remotely.Shared.Entities;
using Remotely.Shared.Interfaces;

namespace Remotely.Server.API;

[ApiController]
[Route("api/[controller]")]
public class ScriptingController : ControllerBase
{
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHubContext;

    private readonly IScriptResultService _scriptResultService;
    private readonly IUserService _userService;
    private readonly IAgentHubSessionCache _serviceSessionCache;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly IApiScriptResultCache _apiScriptResultCache;

    private readonly UserManager<RemotelyUser> _userManager;

    public ScriptingController(UserManager<RemotelyUser> userManager,
        IScriptResultService scriptResultService,
        IUserService userService,
        IAgentHubSessionCache serviceSessionCache,
        IExpiringTokenService expiringTokenService,
        IApiScriptResultCache apiScriptResultCache,
        IHubContext<AgentHub, IAgentHubClient> agentHub)
    {
        _scriptResultService = scriptResultService;
        _userService = userService;
        _serviceSessionCache = serviceSessionCache;
        _expiringTokenService = expiringTokenService;
        _apiScriptResultCache = apiScriptResultCache;
        _userManager = userManager;
        _agentHubContext = agentHub;
    }

    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpPost("[action]/{mode}/{deviceID}")]
    public async Task<ActionResult<ScriptResult>> ExecuteCommand(string mode, string deviceID)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<ScriptingShell>(mode, true, out var shell))
        {
            return BadRequest("Unable to parse shell type.  Use either PSCore, WinPS, Bash, or CMD.");
        }

        var command = string.Empty;
        using (var sr = new StreamReader(Request.Body))
        {
            command = await sr.ReadToEndAsync();
        }

        if (Request.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var username = Request.HttpContext.User.Identity.Name;
            var userResult = await _userService.GetUserByName($"{username}");

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            if (!_userService.DoesUserHaveAccessToDevice(deviceID, userResult.Value))
            {
                return Unauthorized();
            }
        }

        if (!_serviceSessionCache.TryGetByDeviceId(deviceID, out var device))
        {
            return NotFound();
        }

        if (!_serviceSessionCache.TryGetConnectionId(deviceID, out var connectionId))
        {
            return NotFound();
        }

        if (device.OrganizationID != orgId)
        {
            return Unauthorized();
        }

        var requestID = Guid.NewGuid().ToString();
        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(AppConstants.ScriptRunExpirationMinutes));

        // TODO: Replace with new invoke capability in .NET 7.
        await _agentHubContext.Clients.Client(connectionId).ExecuteCommandFromApi(
            shell, 
            authToken, 
            requestID, 
            command,
            User?.Identity?.Name ?? "API Key");

        var success = await WaitHelper.WaitForAsync(() => _apiScriptResultCache.Contains(requestID), TimeSpan.FromSeconds(30));
        if (!success)
        {
            return NotFound();
        }

        if (!_apiScriptResultCache.TryTake(requestID, out var commandId))
        {
            return NotFound();
        }

        var scriptResult = await _scriptResultService.GetScriptResult($"{commandId}", orgId);
        if (!scriptResult.IsSuccess)
        {
            return NotFound();
        }
        return scriptResult.Value;
    }
}
