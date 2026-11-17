using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Auth;
using Remotely.Server.Services;

namespace Remotely.Server.Filters;

/// <summary>
/// Applies <see cref="ViewerAdmissionPolicy"/> to the ViewerHub connection.
/// <para>
/// [ServiceFilter] does not run on SignalR hubs, so the authorization that
/// protects /Viewer never protected /hubs/viewer.  A hub filter is the
/// supported place to enforce it: a refused connection is closed before any
/// hub method can run, and every invocation re-checks that the connection was
/// admitted.
/// </para>
/// </summary>
internal sealed class ViewerHubAuthorizationFilter(
    ISettingsService _settingsService,
    IOtpProvider _otpProvider) : IHubFilter
{
    private const string AdmissionGrantedKey = "ViewerHubAdmissionGranted";

    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        var httpContext = context.Context.GetHttpContext();

        if (httpContext is null ||
            !await ViewerAdmissionPolicy.AuthorizeHubConnection(
                httpContext,
                _settingsService,
                _otpProvider))
        {
            throw new HubException("Authentication is required to open a viewer connection.");
        }

        context.Context.Items[AdmissionGrantedKey] = true;

        await next(context);
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!invocationContext.Context.Items.ContainsKey(AdmissionGrantedKey))
        {
            throw new HubException("Authentication is required to open a viewer connection.");
        }

        return await next(invocationContext);
    }
}
