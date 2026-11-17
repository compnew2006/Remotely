using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Remotely.Server.Auth;
using Remotely.Server.Services;

namespace Remotely.Server.Filters;

internal class ViewerAuthorizationFilter(
    ISettingsService _settingsService,
    IOtpProvider _otpProvider) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // The policy redeems a one-time password and writes the admission
        // ticket that admits the hub connection of the same browser.
        if (await ViewerAdmissionPolicy.AuthorizePageRequest(
            context.HttpContext,
            _settingsService,
            _otpProvider))
        {
            return;
        }

        context.Result = new RedirectResult("/Account/Login");
    }
}
