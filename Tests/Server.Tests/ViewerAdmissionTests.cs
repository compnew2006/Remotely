#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Auth;
using Remotely.Server.Filters;
using Remotely.Server.Models;
using Remotely.Server.Services;

namespace Remotely.Server.Tests;

/// <summary>
/// The Viewer page and the ViewerHub connection must share one admission
/// condition.  The page filter alone did not protect /hubs/viewer, so a client
/// could skip the page and open the hub directly.
/// <para>
/// The page redeems a one-time password and hands the browser an admission
/// ticket in a cookie; the hub accepts the ticket.  A credential in a URL
/// would end up in the access log of every proxy on the way, and a hub
/// connection cannot read a query parameter on a WebSocket handshake.
/// </para>
/// </summary>
[TestClass]
public class ViewerAdmissionTests
{
    private const string Ticket = "admission-ticket";

    private Mock<ISettingsService> _settingsService = null!;
    private Mock<IOtpProvider> _otpProvider = null!;

    [TestInitialize]
    public void Init()
    {
        _settingsService = new Mock<ISettingsService>();
        _otpProvider = new Mock<IOtpProvider>();

        _settingsService
            .Setup(x => x.GetSettings())
            .ReturnsAsync(new SettingsModel { RemoteControlRequiresAuthentication = true });

        _otpProvider
            .Setup(x => x.RedeemOtp("valid-otp"))
            .Returns(Ticket);

        _otpProvider
            .Setup(x => x.HasAdmission(Ticket))
            .Returns(true);
    }

    [TestMethod]
    [Description("When authentication is not required, the viewer is admitted without a login or an one-time password.")]
    public async Task Policy_AllowsAnonymous_WhenAuthenticationIsNotRequired()
    {
        _settingsService
            .Setup(x => x.GetSettings())
            .ReturnsAsync(new SettingsModel { RemoteControlRequiresAuthentication = false });

        var allowed = await ViewerAdmissionPolicy.AuthorizePageRequest(
            CreateHttpContext(authenticated: false),
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsTrue(allowed);
    }

    [TestMethod]
    [Description("An authenticated viewer is admitted without an one-time password.")]
    public async Task Policy_AllowsAuthenticatedViewer()
    {
        var allowed = await ViewerAdmissionPolicy.AuthorizePageRequest(
            CreateHttpContext(authenticated: true),
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsTrue(allowed);
    }

    [TestMethod]
    [Description("An anonymous viewer without an one-time password is refused.")]
    public async Task Policy_RefusesAnonymousViewerWithoutOtp()
    {
        var allowed = await ViewerAdmissionPolicy.AuthorizePageRequest(
            CreateHttpContext(authenticated: false),
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    [Description("An anonymous viewer with a valid one-time password is admitted, matching the page behavior.")]
    public async Task Policy_AllowsAnonymousViewerWithValidOtp()
    {
        var allowed = await ViewerAdmissionPolicy.AuthorizePageRequest(
            CreateHttpContext(authenticated: false, queryString: "?otp=valid-otp"),
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsTrue(allowed);
    }

    [TestMethod]
    [Description("The redeemed one-time password becomes a cookie, because the hub connection cannot read the query string.")]
    public async Task Policy_WritesTheAdmissionTicketAsACookie()
    {
        var httpContext = CreateHttpContext(authenticated: false, queryString: "?otp=valid-otp");

        var allowed = await ViewerAdmissionPolicy.AuthorizePageRequest(
            httpContext,
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsTrue(allowed);

        // The wire format is what the browser sees, so that is what is checked.
        var setCookie = httpContext.Response.Headers.SetCookie.ToString();

        StringAssert.Contains(setCookie, ViewerAdmissionPolicy.AdmissionCookieName);
        StringAssert.Contains(setCookie, Ticket);
        StringAssert.Contains(setCookie, "httponly", "A credential must not be readable by the script of the page.");
        StringAssert.Contains(setCookie, "samesite=strict");
    }

    [TestMethod]
    [Description("A browser that already holds an admission ticket is admitted again, so a refresh of the page still works.")]
    public async Task Policy_AllowsViewerWithAnAdmissionTicket()
    {
        var httpContext = CreateHttpContext(authenticated: false);
        httpContext.Request.Headers["Cookie"] = $"{ViewerAdmissionPolicy.AdmissionCookieName}={Ticket}";

        var allowed = await ViewerAdmissionPolicy.AuthorizePageRequest(
            httpContext,
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsTrue(allowed);

        _otpProvider.Verify(
            x => x.RedeemOtp(It.IsAny<string>()),
            Times.Never,
            "A ticket is enough; the one-time password is not spent again.");
    }

    [TestMethod]
    [Description("The one-time password is consumed, so a leaked link cannot be replayed.")]
    public async Task Policy_ConsumesTheOtp()
    {
        _otpProvider
            .Setup(x => x.RedeemOtp("valid-otp"))
            .Returns(Ticket);

        var context = CreateHttpContext(authenticated: false, queryString: "?otp=valid-otp");

        await ViewerAdmissionPolicy.AuthorizePageRequest(
            context,
            _settingsService.Object,
            _otpProvider.Object);

        _otpProvider.Verify(x => x.RedeemOtp("valid-otp"), Times.Once);
    }

    [TestMethod]
    [Description("An unknown one-time password does not admit the viewer.")]
    public async Task Policy_RefusesAnonymousViewerWithUnknownOtp()
    {
        var allowed = await ViewerAdmissionPolicy.AuthorizePageRequest(
            CreateHttpContext(authenticated: false, queryString: "?otp=unknown"),
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    [Description("The hub connection is refused when it carries no credential.")]
    public async Task HubPolicy_RefusesAnonymousConnection()
    {
        var allowed = await ViewerAdmissionPolicy.AuthorizeHubConnection(
            CreateHttpContext(authenticated: false),
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    [Description("The hub connection of an authenticated browser is allowed.")]
    public async Task HubPolicy_AllowsAuthenticatedConnection()
    {
        var allowed = await ViewerAdmissionPolicy.AuthorizeHubConnection(
            CreateHttpContext(authenticated: true),
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsTrue(allowed);
    }

    [TestMethod]
    [Description("The hub connection is allowed when the browser presents the admission ticket.")]
    public async Task HubPolicy_AllowsConnectionWithAnAdmissionTicket()
    {
        var httpContext = CreateHttpContext(authenticated: false);
        httpContext.Request.Headers["Cookie"] = $"{ViewerAdmissionPolicy.AdmissionCookieName}={Ticket}";

        var allowed = await ViewerAdmissionPolicy.AuthorizeHubConnection(
            httpContext,
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsTrue(allowed);
    }

    [TestMethod]
    [Description("An unknown admission ticket does not admit the hub connection.")]
    public async Task HubPolicy_RefusesConnectionWithAnUnknownTicket()
    {
        var httpContext = CreateHttpContext(authenticated: false);
        httpContext.Request.Headers["Cookie"] = $"{ViewerAdmissionPolicy.AdmissionCookieName}=forged";

        var allowed = await ViewerAdmissionPolicy.AuthorizeHubConnection(
            httpContext,
            _settingsService.Object,
            _otpProvider.Object);

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    [Description("The hub connection is closed when the same admission rule refuses the caller, so the hub cannot be opened directly.")]
    public async Task HubFilter_RefusesUnauthorizedConnection()
    {
        var filter = CreateFilter();
        var context = CreateHubLifetimeContext(authenticated: false);

        await Assert.ThrowsExceptionAsync<HubException>(async () =>
            await filter.OnConnectedAsync(context, _ => Task.CompletedTask));
    }

    [TestMethod]
    [Description("An admitted hub connection is allowed through and may invoke hub methods.")]
    public async Task HubFilter_AllowsAuthorizedConnection()
    {
        var filter = CreateFilter();
        var context = CreateHubLifetimeContext(authenticated: true);

        var nextCalled = false;
        await filter.OnConnectedAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.IsTrue(nextCalled);
        Assert.IsTrue(context.Context.Items.ContainsKey("ViewerHubAdmissionGranted"));
    }

    [TestMethod]
    [Description("A connection that never passed the admission check cannot invoke any hub method on the viewer hub.")]
    public async Task HubFilter_RefusesHubInvocationOnUnadmittedConnection()
    {
        var filter = CreateFilter();
        var context = CreateHubLifetimeContext(authenticated: false);
        var invocation = new HubInvocationContext(
            context.Context,
            context.ServiceProvider,
            context.Hub,
            context.Hub.GetType().GetMethod(nameof(TestHub.Invoke))!,
            Array.Empty<object>());

        var nextCalled = false;

        await Assert.ThrowsExceptionAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(invocation, _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>(null);
            }));

        Assert.IsFalse(nextCalled);
    }

    private ViewerHubAuthorizationFilter CreateFilter()
    {
        return new ViewerHubAuthorizationFilter(
            _settingsService.Object,
            _otpProvider.Object);
    }

    private static DefaultHttpContext CreateHttpContext(
        bool authenticated,
        string? queryString = null)
    {
        var context = new DefaultHttpContext();

        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin@test.com")],
                "TestAuth"));
        }

        if (queryString is not null)
        {
            context.Request.QueryString = new QueryString(queryString);
        }

        return context;
    }

    private static HubLifetimeContext CreateHubLifetimeContext(bool authenticated)
    {
        var httpContext = CreateHttpContext(authenticated);

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new TestHttpContextFeature
        {
            HttpContext = httpContext
        });

        var callerContext = new Mock<HubCallerContext>();
        callerContext.SetupGet(x => x.Features).Returns(features);
        callerContext.SetupGet(x => x.Items).Returns(new Dictionary<object, object?>());

        return new HubLifetimeContext(
            callerContext.Object,
            new Mock<IServiceProvider>().Object,
            new TestHub());
    }

    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class TestHub : Hub
    {
        public void Invoke()
        {
        }
    }
}
