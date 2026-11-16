using Remotely.Server.Options;

namespace Remotely.Server.Extensions;

public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds the response headers that stop the browser from guessing, framing,
    /// or leaking referrers.  These are safe for every response and every
    /// deployment, so they are always on.
    /// </summary>
    private static readonly (string Name, string Value)[] BaselineHeaders =
    [
        ("X-Content-Type-Options", "nosniff"),
        ("X-Frame-Options", "DENY"),
        ("Referrer-Policy", "no-referrer"),
        ("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()"),
        ("Cross-Origin-Opener-Policy", "same-origin")
    ];

    /// <summary>
    /// The console loads every script from its own origin: the Blazor runtime,
    /// the SignalR client, and the viewer interop script are all served by the
    /// server.  There are no inline script blocks, so 'unsafe-inline' and
    /// 'unsafe-eval' are not required and are deliberately absent.
    /// <para>
    /// The Bootswatch themes import their fonts from Google Fonts.  Without an
    /// explicit exception the browser blocks those stylesheets, the console
    /// falls back to the system font, and the browser logs a policy error on
    /// every page.  Only the two font origins are allowed, and only for the
    /// styles and the fonts: the scripts stay restricted to this origin.
    /// </para>
    /// <para>
    /// If a deployment adds a third-party script or an inline block, the policy
    /// must be extended explicitly rather than weakened globally.  The policy
    /// can be turned off with
    /// <c>Remotely_ApplicationOptions__EnableContentSecurityPolicy=false</c>.
    /// </para>
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "connect-src 'self' ws: wss:; " +
        "media-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        ApplicationOptions? options = null)
    {
        var emitCsp = options?.EnableContentSecurityPolicy ?? true;

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            foreach (var (name, value) in BaselineHeaders)
            {
                // Do not overwrite a value that a specific endpoint set
                // deliberately (for example a download that must be framed).
                if (!headers.ContainsKey(name))
                {
                    headers[name] = value;
                }
            }

            if (emitCsp && !headers.ContainsKey("Content-Security-Policy"))
            {
                headers["Content-Security-Policy"] = ContentSecurityPolicy;
            }

            await next();
        });
    }
}
