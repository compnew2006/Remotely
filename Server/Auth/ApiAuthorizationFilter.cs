using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Remotely.Server.Services;
using Remotely.Shared;
using System.Net;

namespace Remotely.Server.Auth;

public class ApiAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IApiTokenService _apiTokenService;
    private readonly IUserService _userService;
    private readonly ILogger<ApiAuthorizationFilter> _logger;

    public ApiAuthorizationFilter(
        IApiTokenService apiTokenService,
        IUserService userService,
        ILogger<ApiAuthorizationFilter> logger)
    {
        _apiTokenService = apiTokenService;
        _userService = userService;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!await IsAuthorizedAsync(context.HttpContext))
        {
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Result = new UnauthorizedResult();
        }
    }

    /// <summary>
    /// Returns true when the request is authorized as either an authenticated
    /// administrator or a valid API key. If authorization is in doubt, returns
    /// false (fail closed).
    /// </summary>
    public async Task<bool> IsAuthorizedAsync(HttpContext http)
    {
        try
        {
            http.Request.Headers["OrganizationID"] = string.Empty;

            if (http.User.Identity?.IsAuthenticated == true)
            {
                var userResult = await _userService.GetUserByName($"{http.User.Identity.Name}");
                if (userResult.IsSuccess && userResult.Value.IsAdministrator)
                {
                    http.Request.Headers["OrganizationID"] = userResult.Value.OrganizationID;
                    return true;
                }
            }

            if (http.Request.Headers.TryGetValue(AppConstants.ApiKeyHeaderName, out var apiHeaderValue))
            {
                var headerComponents = apiHeaderValue.ToString().Split(":");
                if (headerComponents.Length < 2)
                {
                    return false;
                }

                var keyId = headerComponents[0].Trim();
                var secret = headerComponents[1].Trim();

                var isValid = await _apiTokenService.ValidateApiKey(
                        keyId,
                        secret,
                        http.Request.Path,
                        $"{http.Connection.RemoteIpAddress}");

                if (isValid)
                {
                    var keyResult = await _apiTokenService.GetApiKey(keyId);

                    if (keyResult.IsSuccess)
                    {
                        http.Request.Headers["OrganizationID"] = keyResult.Value.OrganizationID;
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            // Fail closed: if authorization itself faults (e.g. DB unavailable),
            // deny the request instead of letting the action proceed.
            _logger.LogError(ex, "Error while authorizing API key.");
            return false;
        }
    }
}
