using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Extensions;
using Remotely.Server.Options;

namespace Remotely.Server.Tests;

/// <summary>
/// The console previously shipped with no framing, sniffing, or script policy
/// headers at all.  These tests pin the middleware behaviour that closes that
/// gap, including the escape hatch and the "do not stomp on an endpoint's own
/// header" rule.
/// </summary>
[TestClass]
public class SecurityHeadersTests
{
    [TestMethod]
    public async Task BaselineHeaders_AreAlwaysPresent()
    {
        var context = await RunPipelineAsync();

        Assert.AreEqual("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.AreEqual("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.AreEqual("no-referrer", context.Response.Headers["Referrer-Policy"]);
        StringAssert.Contains(
            context.Response.Headers["Permissions-Policy"].ToString(),
            "microphone=()");
        Assert.AreEqual("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"]);
    }

    [TestMethod]
    public async Task ContentSecurityPolicy_IsEmittedByDefault()
    {
        var context = await RunPipelineAsync();

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();

        Assert.IsFalse(string.IsNullOrWhiteSpace(csp), "A CSP must be emitted by default.");
        StringAssert.Contains(csp, "script-src 'self'");
        StringAssert.Contains(csp, "object-src 'none'");
        StringAssert.Contains(csp, "frame-ancestors 'none'");

        // The policy is only meaningful if scripts cannot be inlined or eval'd.
        var scriptDirective = csp
            .Split(';')
            .Select(part => part.Trim())
            .First(part => part.StartsWith("script-src"));

        Assert.IsFalse(scriptDirective.Contains("unsafe-inline"));
        Assert.IsFalse(scriptDirective.Contains("unsafe-eval"));
    }

    [TestMethod]
    public async Task ContentSecurityPolicy_AllowsTheThemeFonts_AndNothingElse()
    {
        var context = await RunPipelineAsync();

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();

        // The Bootswatch themes import their fonts from Google Fonts.  Without
        // these two origins the browser blocks the stylesheets and the console
        // loses its fonts.
        StringAssert.Contains(csp, "https://fonts.googleapis.com");
        StringAssert.Contains(csp, "https://fonts.gstatic.com");

        // No other third-party origin is allowed, and the scripts stay on this
        // origin.
        var thirdPartyOrigins = csp
            .Split(';')
            .Select(part => part.Trim())
            .SelectMany(part => part.Split(' '))
            .Where(value => value.Contains("://"))
            .Distinct()
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "https://fonts.googleapis.com", "https://fonts.gstatic.com" },
            thirdPartyOrigins);

        var scriptDirective = csp
            .Split(';')
            .Select(part => part.Trim())
            .First(part => part.StartsWith("script-src"));

        Assert.AreEqual("script-src 'self'", scriptDirective);
    }

    [TestMethod]
    public async Task ContentSecurityPolicy_CanBeDisabled_WithoutLosingBaselineHeaders()
    {
        var context = await RunPipelineAsync(new ApplicationOptions
        {
            EnableContentSecurityPolicy = false
        });

        Assert.IsFalse(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        Assert.AreEqual("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.AreEqual("DENY", context.Response.Headers["X-Frame-Options"]);
    }

    [TestMethod]
    public async Task ExistingHeaders_AreNotOverwritten()
    {
        var context = await RunPipelineAsync(configureContext: context =>
        {
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        });

        Assert.AreEqual("default-src 'none'", context.Response.Headers["Content-Security-Policy"]);
        Assert.AreEqual("SAMEORIGIN", context.Response.Headers["X-Frame-Options"]);
    }

    private static async Task<HttpContext> RunPipelineAsync(
        ApplicationOptions? options = null,
        Action<HttpContext>? configureContext = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UseSecurityHeaders(options);
        app.Run(_ => Task.CompletedTask);

        var pipeline = app.Build();
        var context = new DefaultHttpContext();

        configureContext?.Invoke(context);
        await pipeline(context);

        return context;
    }
}
