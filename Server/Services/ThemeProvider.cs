using Remotely.Shared.Enums;

namespace Remotely.Server.Services;

public interface IThemeProvider
{
    Task<Theme> GetEffectiveTheme();
}

public class ThemeProvider : IThemeProvider
{
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;

    public ThemeProvider(IAuthService authService, ISettingsService settingsService)
    {
        _authService = authService;
        _settingsService = settingsService;
    }

    public async Task<Theme> GetEffectiveTheme()
    {
        var settings = await _settingsService.GetSettings();
        if (await _authService.IsAuthenticated())
        {
            var userResult = await _authService.GetUser();
            if (userResult.IsSuccess)
            {
                return userResult.Value.UserOptions?.Theme ?? settings.Theme;
            }
        }

        return settings.Theme;
    }
}
