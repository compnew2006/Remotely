using Remotely.Server.Services;

namespace Remotely.Server.Auth;

/// <summary>
/// The admission condition for the remote-control viewer.
/// <para>
/// The same rule protects the Viewer page and the ViewerHub connection.  The
/// page filter used to be the only enforcement point, so a client that skipped
/// the page could open /hubs/viewer directly and reach the device.  Both entry
/// points now evaluate this policy.
/// </para>
/// <para>
/// The two entry points differ in the credential they accept, not in the rule:
/// the page accepts the one-time password from the query string and exchanges
/// it for an admission ticket in a cookie; the hub accepts only the ticket.
/// A hub connection cannot read a query parameter, and a credential that
/// travels in a URL ends up in the access log of every proxy on the way and
/// in the history of the browser that opened the link.
/// </para>
/// </summary>
internal static class ViewerAdmissionPolicy
{
    /// <summary>
    /// The name of the cookie that carries the admission ticket.
    /// </summary>
    public const string AdmissionCookieName = "remotely-viewer-admission";

    /// <summary>
    /// The name of the query parameter that carries the one-time password.
    /// </summary>
    public const string OtpQueryParameterName = "otp";

    /// <summary>
    /// Authorizes a request for the viewer page.
    /// <para>
    /// Allowed when remote control does not require authentication, when the
    /// caller is authenticated, or when the request carries a valid one-time
    /// password.  A password that is accepted is consumed and exchanged for
    /// an admission ticket, which is written to a cookie so that the hub
    /// connection of the same browser is admitted as well.
    /// </para>
    /// </summary>
    public static async Task<bool> AuthorizePageRequest(
        HttpContext httpContext,
        ISettingsService settingsService,
        IOtpProvider otpProvider)
    {
        var settings = await settingsService.GetSettings();
        if (!settings.RemoteControlRequiresAuthentication)
        {
            return true;
        }

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        // A browser that already holds an admission ticket is admitted again.
        // The one-time password is good for one use only, so without this a
        // refresh of the page would send the viewer back to the login.
        var existingTicket = httpContext.Request.Cookies[AdmissionCookieName];
        if (otpProvider.HasAdmission(existingTicket))
        {
            return true;
        }

        if (httpContext.Request.Query.TryGetValue(OtpQueryParameterName, out var otp))
        {
            var ticket = otpProvider.RedeemOtp(otp.ToString());
            if (!string.IsNullOrEmpty(ticket))
            {
                WriteAdmissionCookie(httpContext, ticket);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Authorizes a connection to the viewer hub.
    /// <para>
    /// Allowed when remote control does not require authentication, when the
    /// caller is authenticated, or when the browser presents an admission
    /// ticket that the page has issued.
    /// </para>
    /// </summary>
    public static async Task<bool> AuthorizeHubConnection(
        HttpContext httpContext,
        ISettingsService settingsService,
        IOtpProvider otpProvider)
    {
        var settings = await settingsService.GetSettings();
        if (!settings.RemoteControlRequiresAuthentication)
        {
            return true;
        }

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        var ticket = httpContext.Request.Cookies[AdmissionCookieName];

        return otpProvider.HasAdmission(ticket);
    }

    private static void WriteAdmissionCookie(HttpContext httpContext, string ticket)
    {
        httpContext.Response.Cookies.Append(
            AdmissionCookieName,
            ticket,
            new CookieOptions
            {
                // The ticket is a credential, so the script of the page must
                // not be able to read it.
                HttpOnly = true,
                // The hub connection is on the same site, so a strict policy
                // does not keep the browser from sending it.
                SameSite = SameSiteMode.Strict,
                Path = "/",
                // The cookie must not travel over plain HTTP.
                Secure = httpContext.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.Add(OtpProvider.AdmissionLifetime)
            });
    }
}
