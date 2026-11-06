using Remotely.Server.Enums;
using Remotely.Server.Filters;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using Remotely.Shared.Models.Dtos;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Remotely.Server.Hubs;

[ServiceFilter(typeof(ViewerAuthorizationFilter))]
public class ViewerHub : Hub<IViewerHubClient>
{
    private readonly IHubContext<DesktopHub, IDesktopHubClient> _desktopHub;
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub;
    private readonly IDataService _dataService;
    private readonly ISessionRecordingSink _sessionRecordingSink;
    private readonly IRemoteControlSessionCache _desktopSessionCache;
    private readonly ILogger<ViewerHub> _logger;
    private readonly IDesktopStreamCache _streamCache;

    public ViewerHub(
        IRemoteControlSessionCache desktopSessionCache,
        IDesktopStreamCache streamCache,
        IHubContext<AgentHub, IAgentHubClient> agentHub,
        IHubContext<DesktopHub, IDesktopHubClient> desktopHub,
        ISessionRecordingSink sessionRecordingSink,
        IDataService dataService,
        ILogger<ViewerHub> logger)
    {
        _desktopSessionCache = desktopSessionCache;
        _streamCache = streamCache;
        _desktopHub = desktopHub;
        _agentHub = agentHub;
        _dataService = dataService;
        _sessionRecordingSink = sessionRecordingSink;
        _logger = logger;
    }

    private string RequesterDisplayName
    {
        get
        {
            if (Context.Items.TryGetValue(nameof(RequesterDisplayName), out var result) &&
                result is string requesterName)
            {
                return requesterName;
            }
            return string.Empty;
        }
        set
        {
            Context.Items[nameof(RequesterDisplayName)] = value;
        }
    }

    private RemoteControlSession SessionInfo
    {
        get
        {
            if (Context.Items.TryGetValue(nameof(SessionInfo), out var result) &&
                result is RemoteControlSession session)
            {
                return session;
            }

            var newSession = new RemoteControlSession();
            Context.Items[nameof(SessionInfo)] = newSession;
            return newSession;
        }
        set
        {
            Context.Items[nameof(SessionInfo)] = value;
        }
    }
    public async Task<Result> ChangeWindowsSession(int targetWindowsSession)
    {
        try
        {
            if (SessionInfo.Mode != RemoteControlMode.Unattended)
            {
                return Result.Fail("Only available in unattended mode.");
            }

            // SPEC-001 / FR-010, FR-011: changing the target Windows session is a
            // control action and is refused for View Only sessions.
            if (SessionInfo.ViewOnly)
            {
                LogPrivilegeRefused(nameof(ChangeWindowsSession));
                await Clients.Caller.ShowMessage("Not permitted: session is View Only.");
                return Result.Fail("Not permitted: session is View Only.");
            }

            SessionInfo.ViewerList.Remove(Context.ConnectionId);
            await _desktopHub.Clients
                .Client(SessionInfo.DesktopConnectionId)
                .ViewerDisconnected(Context.ConnectionId);

            var previousSessionId = SessionInfo.UnattendedSessionId;
            var previousMode = SessionInfo.AccessMode;
            SessionInfo = SessionInfo.CreateNew();

            // SPEC-001 / FR-014: a clone carries its own recorded authorization
            // decision. The mode itself is preserved by CreateNew (FR-013) and is
            // never widened here.
            _logger.LogInformation(
                "Privilege decision: session cloned.  " +
                "Session ID: {sessionId}.  " +
                "Access Mode: {accessMode}.  " +
                "Previous Session ID: {previousSessionId}.  " +
                "Previous Access Mode: {previousAccessMode}.  " +
                "Identity: {identity}.",
                SessionInfo.UnattendedSessionId,
                SessionInfo.AccessMode,
                previousSessionId,
                previousMode,
                Context.User?.Identity?.Name ?? "unauthenticated");

            _desktopSessionCache.AddOrUpdate($"{SessionInfo.UnattendedSessionId}", SessionInfo);

            await _agentHub.Clients
                .Client(SessionInfo.AgentConnectionId)
                .ChangeWindowsSession(
                    Context.ConnectionId,
                    $"{SessionInfo.UnattendedSessionId}",
                    SessionInfo.AccessKey,
                    SessionInfo.UserConnectionId,
                    SessionInfo.RequesterUserName,
                    SessionInfo.OrganizationName,
                    SessionInfo.OrganizationId,
                    targetWindowsSession);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while changing Windows session.");
            return Result.Fail("An error occurred while changing Windows session.");
        }
    }

    public async IAsyncEnumerable<byte[]> GetDesktopStream()
    {
        var sessionResult = await _streamCache.WaitForStreamSession(
            SessionInfo.StreamId,
            Context.ConnectionId,
            TimeSpan.FromSeconds(30));

        if (!sessionResult.IsSuccess)
        {
            _logger.LogError("Timed out while waiting for desktop stream.");
            await Clients.Caller.ShowMessage("Request timed out");
            yield break;
        }

        var signaler = sessionResult.Value;

        if (signaler.Stream is null)
        {
            _logger.LogError("Stream was null.");
            yield break;
        }

        SessionInfo.StreamerState = StreamerState.Connected;

        try
        {
            await foreach (var chunk in signaler.Stream)
            {
                yield return chunk;
            }
        }
        finally
        {
            signaler.EndSignal.Release();
            _logger.LogInformation("Streaming session ended for {sessionId}.", SessionInfo.StreamId);
        }
    }

    public async Task<RemoteControlViewerOptions> GetViewerOptions()
    {
        var settings = await _dataService.GetSettings();

        // SPEC-001 / FR-002, FR-007: the viewer UI receives the session's
        // server-owned access mode. The client fetches this after the session
        // attach request, so SessionInfo is the server-owned session record.
        // The viewer's link and URL parameters are never consulted.
        return new RemoteControlViewerOptions()
        {
            ShouldRecordSession = settings.EnableRemoteControlRecording,
            ViewOnly = SessionInfo.ViewOnly
        };
    }

    public async Task InvokeCtrlAltDel()
    {
        try
        {
            // SPEC-001 / FR-010, FR-011: Ctrl+Alt+Del is a control action.
            if (SessionInfo.ViewOnly)
            {
                LogPrivilegeRefused(nameof(InvokeCtrlAltDel));
                await Clients.Caller.ShowMessage("Not permitted: session is View Only.");
                return;
            }

            await _agentHub.Clients.Client(SessionInfo.AgentConnectionId).InvokeCtrlAltDel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while invoking Ctrl+Alt+Del.");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (!string.IsNullOrWhiteSpace(SessionInfo.DesktopConnectionId))
        {
            await _desktopHub.Clients
                .Client(SessionInfo.DesktopConnectionId)
                .ViewerDisconnected(Context.ConnectionId);
        }

        SessionInfo.ViewerList.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendDtoToClient(byte[] dtoWrapper)
    {
        if (string.IsNullOrWhiteSpace(SessionInfo.DesktopConnectionId))
        {
            return;
        }

        // SPEC-001 / FR-010, FR-011, FR-012: every remote-control message is
        // authorized against the server-owned access mode of the session before
        // it is forwarded. FullControl sessions are forwarded untouched (no
        // parsing overhead); View Only sessions are inspected and any
        // device-modifying message is refused here, at the server, and recorded.
        if (SessionInfo.ViewOnly)
        {
            if (!TryGetDtoType(dtoWrapper, out var dtoType))
            {
                // A malformed message cannot be classified, so it cannot be
                // proven to be view-only traffic. Fail closed (FR-009).
                LogPrivilegeRefused("UnparsableDto");
                return;
            }

            if (dtoType.IsControlMessage())
            {
                LogPrivilegeRefused(dtoType.ToString());

                // FR-011: make the refusal observable to the viewer/administrator
                // UI, but only once per connection so input-frequency messages do
                // not flood the channel.
                if (!_viewOnlyRefusalNotified)
                {
                    _viewOnlyRefusalNotified = true;
                    await Clients.Caller.ShowMessage("Input blocked: session is View Only.");
                }

                return;
            }
        }

        await _desktopHub.Clients
            .Client(SessionInfo.DesktopConnectionId)
            .SendDtoToClient(dtoWrapper, Context.ConnectionId);
    }

    private bool _viewOnlyRefusalNotified;

    /// <summary>
    /// SPEC-001 / FR-008, FR-009, FR-029: record each refused privilege
    /// decision with the session, the action, and the identity. Access keys and
    /// other session secrets are never logged.
    /// </summary>
    private void LogPrivilegeRefused(string action)
    {
        _logger.LogWarning(
            "Privilege decision: REFUSED.  " +
            "Action: {action}.  " +
            "Session ID: {sessionId}.  " +
            "Access Mode: {accessMode}.  " +
            "Identity: {identity}.  " +
            "Connection ID: {connectionId}.",
            action,
            SessionInfo.UnattendedSessionId != Guid.Empty
                ? SessionInfo.UnattendedSessionId.ToString()
                : SessionInfo.AttendedSessionId,
            SessionInfo.AccessMode,
            Context.User?.Identity?.Name ?? "unauthenticated",
            Context.ConnectionId);
    }

    private static bool TryGetDtoType(byte[] dtoWrapper, out DtoType dtoType)
    {
        dtoType = default;
        try
        {
            var wrapper = MessagePackSerializer.Deserialize<DtoWrapper>(dtoWrapper);
            dtoType = wrapper.DtoType;
            return true;
        }
        catch (MessagePackSerializationException)
        {
            // A malformed message cannot be classified, so it cannot be proven
            // to be view-only traffic. Fail closed (FR-009).
            return false;
        }
    }

    public async Task<Result> SendScreenCastRequestToDevice(string sessionId, string accessKey, string requesterName)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result.Fail("Session ID cannot be empty.");
        }

        if (!_desktopSessionCache.TryGetValue(sessionId, out var session))
        {
            return Result.Fail("Session ID not found.");
        }

        if (session.Mode == RemoteControlMode.Unattended &&
            accessKey != session.AccessKey)
        {
            _logger.LogError("Access key does not match for unattended session.  " +
                "Session ID: {sessionId}.  " +
                "Requester Name: {requesterName}.  " +
                "Requester Connection ID: {connectionId}",
                sessionId,
                requesterName,
                Context.ConnectionId);
            return Result.Fail("Authorization failed.");
        }

        SessionInfo = session;
        SessionInfo.ViewerList.Add(Context.ConnectionId);
        SessionInfo.StreamId = Guid.NewGuid();
        RequesterDisplayName = requesterName;

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            SessionInfo.RequesterUserName = Context.User.Identity.Name ?? string.Empty;
        }

        var logMessage = $"Remote control session requested.  " +
                            $"Login ID (if logged in): {Context.User?.Identity?.Name}.  " +
                            $"Machine Name: {SessionInfo.MachineName}.  " +
                            $"Stream ID: {SessionInfo.StreamId}.  " +
                            $"Requester Name (if specified): {RequesterDisplayName}.  " +
                            $"Connection ID: {Context.ConnectionId}. User ID: {Context.UserIdentifier}.  " +
                            $"Screen Caster Connection ID: {SessionInfo.DesktopConnectionId}.  " +
                            $"Mode: {SessionInfo.Mode}.  " +
                            $"Requester IP Address: {Context.GetHttpContext()?.Connection?.RemoteIpAddress}";

        _logger.LogInformation("{msg}", logMessage);

        if (SessionInfo.Mode == RemoteControlMode.Unattended)
        {
            if (SessionInfo.RequireConsent)
            {
                var request = new RemoteControlAccessRequest(
                    Context.ConnectionId,
                    RequesterDisplayName,
                    SessionInfo.OrganizationName);

                var result = await _desktopHub.Clients
                    .Client(SessionInfo.DesktopConnectionId)
                    .PromptForAccess(request);

                if (result != Shared.Enums.PromptForAccessResult.Accepted)
                {
                    return Result.Fail($"Access request failed.  Reason: {result}");
                }
            }

            SessionInfo.RequireConsent = false;

            await _desktopHub.Clients
                .Client(SessionInfo.DesktopConnectionId)
                .GetScreenCast(
                    Context.ConnectionId,
                    RequesterDisplayName,
                    SessionInfo.NotifyUserOnStart,
                    SessionInfo.StreamId,
                    SessionInfo.ViewOnly);
        }
        else
        {
            SessionInfo.Mode = RemoteControlMode.Attended;

            // SPEC-001: attended sessions are consent-gated on the device (the
            // employee shares the session ID and accepts the connection prompt),
            // which is the existing consent mechanism the constitution and the
            // specification reuse. That consent is the authority basis for
            // FullControl here; the decision is recorded (FR-008). This preserves
            // the attended support workflow (Principle II: preserve function).
            SessionInfo.AccessMode = Shared.Enums.RemoteAccessMode.FullControl;
            _logger.LogInformation(
                "Privilege decision: GRANTED.  " +
                "Action: AttendedSessionAttach.  " +
                "Applied Mode: {accessMode}.  " +
                "Basis: attended consent on device.  " +
                "Session ID: {sessionId}.  " +
                "Identity: {identity}.",
                SessionInfo.AccessMode,
                SessionInfo.AttendedSessionId,
                Context.User?.Identity?.Name ?? RequesterDisplayName);

            await _desktopHub.Clients
                .Client(SessionInfo.DesktopConnectionId)
                .RequestScreenCast(
                    Context.ConnectionId,
                    RequesterDisplayName,
                    SessionInfo.NotifyUserOnStart,
                    SessionInfo.StreamId,
                    SessionInfo.ViewOnly);
        }

        return Result.Ok();
    }

    public async Task StoreSessionRecording(IAsyncEnumerable<byte[]> webmStream)
    {
        try
        {
            await _sessionRecordingSink.SinkWebmStream(webmStream, SessionInfo);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Session recording stopped for stream {streamId}.", SessionInfo.StreamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while storing session recording for stream {streamId}.", SessionInfo.StreamId);
        }
    }

}
