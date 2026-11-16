using Microsoft.AspNetCore.Components.Authorization;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

public interface IAuthService
{
    Task<bool> IsAuthenticated();
    Task<Result<RemotelyUser>> GetUser();
}

public class AuthService : IAuthService
{
    private readonly AuthenticationStateProvider _authProvider;
    private readonly IUserService _userService;

    public AuthService(
        AuthenticationStateProvider authProvider,
        IUserService userService)
    {
        _authProvider = authProvider;
        _userService = userService;
    }

    public async Task<bool> IsAuthenticated()
    {
        var principal = await _authProvider.GetAuthenticationStateAsync();
        return principal?.User?.Identity?.IsAuthenticated ?? false;
    }

    public async Task<Result<RemotelyUser>> GetUser()
    {
        var principal = await _authProvider.GetAuthenticationStateAsync();

        if (principal?.User?.Identity?.IsAuthenticated == true)
        {
            return await _userService.GetUserByName($"{principal.User.Identity.Name}");
        }

        return Result.Fail<RemotelyUser>("Not authenticated.");
    }
}
