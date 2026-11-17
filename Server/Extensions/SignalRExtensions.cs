using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Filters;
using Remotely.Server.Hubs;

namespace Remotely.Server.Extensions;

/// <summary>
/// The SignalR configuration of the server.
/// </summary>
public static class SignalRExtensions
{
    /// <summary>
    /// Registers the hub protocols and the filters that protect the hubs.
    /// <para>
    /// The viewer hub needs a filter of its own: [ServiceFilter] is an MVC
    /// attribute, and SignalR does not run the MVC filter pipeline, so the
    /// filter that protected the viewer page never protected /hubs/viewer.
    /// A hub filter is the supported place to enforce it.
    /// </para>
    /// <para>
    /// The configuration lives in one method so that a test can assert that
    /// the filter is really registered, and not only that the filter works
    /// when it is called by hand.
    /// </para>
    /// </summary>
    public static ISignalRServerBuilder AddRemotelyHubs(this ISignalRServerBuilder builder)
    {
        return builder
            .AddMessagePackProtocol()
            .AddHubOptions<ViewerHub>(options =>
            {
                options.AddFilter<ViewerHubAuthorizationFilter>();
            });
    }
}
