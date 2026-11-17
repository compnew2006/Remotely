#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Extensions;
using Remotely.Server.Filters;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Server.Tests.Mocks;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using Remotely.Shared.Models.Dtos;

namespace Remotely.Server.Tests;

/// <summary>
/// The viewer hub over a real transport.
/// <para>
/// The filter that protects the viewer page was an MVC attribute on the hub,
/// and SignalR does not run the MVC filter pipeline, so /hubs/viewer was
/// open to anybody who knew the protocol.  The unit tests of the filter prove
/// that the filter works when it is called by hand; these tests prove that
/// the hub really refuses a connection that carries no credential, over a
/// real WebSocket, with the real registration of the filter.
/// </para>
/// <para>
/// They also cover the registration itself: a hub that refused nobody would
/// satisfy the first test, so the second test is what says that the refusal
/// is the work of the admission and not of a broken hub.
/// </para>
/// </summary>
[TestClass]
public class ViewerHubAdmissionIntegrationTests
{
    [TestMethod]
    [Description(
        "A connection without a credential cannot invoke a method of the viewer hub.  This is " +
        "the case that was open: the page was protected and the hub was not.")]
    public async Task AnUnauthenticatedConnection_CannotInvokeAHubMethod()
    {
        await using var server = await TestHubServer.StartAsync();

        var connection = new HubConnectionBuilder()
            .WithUrl(server.Url)
            .Build();

        try
        {
            RemoteControlViewerOptions? options = null;
            Exception? failure = null;

            try
            {
                await connection.StartAsync();
                options = await connection.InvokeAsync<RemoteControlViewerOptions>("GetViewerOptions");
            }
            catch (Exception ex)
            {
                // The connection was refused, either when it was opened or
                // when the method was called.  Which of the two happens
                // depends on when the refusal reaches the client.
                failure = ex;
            }

            Assert.IsNull(
                options,
                "The hub must not answer a connection that carries no credential.");
            Assert.IsNotNull(
                failure,
                "A connection that carries no credential must fail instead of being answered.");
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [TestMethod]
    [Description(
        "A connection that carries an admission ticket may invoke a method.  Without this the " +
        "refusal above would also be satisfied by a hub that refuses everybody.")]
    public async Task AConnectionWithAnAdmissionTicket_CanInvokeAHubMethod()
    {
        await using var server = await TestHubServer.StartAsync(ticket: "the-ticket");

        var connection = new HubConnectionBuilder()
            .WithUrl(server.Url, options =>
                options.Headers["Cookie"] = "remotely-viewer-admission=the-ticket")
            .Build();

        try
        {
            await connection.StartAsync();

            var options = await connection.InvokeAsync<RemoteControlViewerOptions>("GetViewerOptions");

            Assert.IsNotNull(options);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// A server that hosts the viewer hub the way the real one does: the same
    /// registration of the hubs, and the same filter.
    /// </summary>
    private sealed class TestHubServer : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private TestHubServer(WebApplication app, string url)
        {
            _app = app;
            Url = url;
        }

        public string Url { get; }

        public static async Task<TestHubServer> StartAsync(string? ticket = null)
        {
            var settingsService = new Mock<ISettingsService>();
            settingsService
                .Setup(x => x.GetSettings())
                .ReturnsAsync(new SettingsModel { RemoteControlRequiresAuthentication = true });

            var otpProvider = new Mock<IOtpProvider>();
            otpProvider
                .Setup(x => x.HasAdmission(ticket ?? string.Empty))
                .Returns(ticket is not null);

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            builder.Services.AddSignalR().AddRemotelyHubs();
            builder.Services.AddScoped<ViewerHubAuthorizationFilter>();
            builder.Services.AddSingleton(settingsService.Object);
            builder.Services.AddSingleton(otpProvider.Object);
            builder.Services.AddSingleton(new Mock<IRemoteControlSessionCache>().Object);
            builder.Services.AddSingleton(new Mock<IDesktopStreamCache>().Object);
            builder.Services.AddSingleton(new Mock<ISessionRecordingSink>().Object);
            builder.Services.AddSingleton(new Mock<IUserService>().Object);
            builder.Services.AddSingleton(new HubContextFixture<AgentHub, IAgentHubClient>().HubContextMock.Object);
            builder.Services.AddSingleton(new HubContextFixture<DesktopHub, IDesktopHubClient>().HubContextMock.Object);

            var app = builder.Build();
            app.MapHub<ViewerHub>("/hubs/viewer");

            await app.StartAsync();

            var address = app
                .Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .First();

            return new TestHubServer(app, $"{address.TrimEnd('/')}/hubs/viewer");
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
