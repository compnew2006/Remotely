using Remotely.Server.Enums;
using Remotely.Server.Services;
using Remotely.Shared.Enums;
using Remotely.Shared.Helpers;

namespace Remotely.Server.Models;

/// <summary>
/// Contains data related to a remote control session.  Consuming projects
/// are expected to derive a class from this and add project-specific properties.
/// They should add these derived classes to<see cref="IRemoteControlSessionCache"/>
/// when sessions are created.
/// </summary>
public class RemoteControlSession : IDisposable
{
    private readonly ManualResetEventSlim _sessionReadySignal = new(false);
    private bool _disposedValue;
    private StreamerState _streamerState;

    public RemoteControlSession()
    {
        Created = DateTimeOffset.Now;
    }
    public string AccessKey { get; internal set; } = string.Empty;
    public string AgentConnectionId { get; set; } = string.Empty;
    public string AttendedSessionId { get; set; } = string.Empty;
    public DateTimeOffset Created { get; internal set; }
    public string DesktopConnectionId { get; internal set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset LastStateChange { get; private set; } = DateTimeOffset.Now;

    /// <summary>
    /// The last time a viewer attached to or detached from this session.
    /// The session cache uses this - not the creation time - to decide
    /// whether an unattended session with no viewers is really abandoned, so
    /// a viewer that briefly drops its connection does not lose the session.
    /// </summary>
    public DateTimeOffset LastActivity { get; internal set; } = DateTimeOffset.Now;
    public string MachineName { get; internal set; } = string.Empty;
    public RemoteControlMode Mode { get; internal set; }

    /// <summary>
    /// The server-owned access authority of this session (SPEC-001 / FR-001, FR-002).
    /// Defaults to ViewOnly (FR-003). Only an explicit, authorized server-side
    /// decision may set FullControl; the browser, the viewer, the link, and the
    /// device cannot widen it.
    /// </summary>
    public RemoteAccessMode AccessMode { get; set; } = RemoteAccessMode.ViewOnly;

    /// <summary>
    /// Convenience view of <see cref="AccessMode"/>. Read-only by design:
    /// authority is changed through <see cref="AccessMode"/>, never through this flag.
    /// </summary>
    public bool ViewOnly => AccessMode == RemoteAccessMode.ViewOnly;
    /// <summary>
    /// Whether to notify the user via system notification when a remote
    /// control session has started.
    /// </summary>
    public bool NotifyUserOnStart { get; set; } = true;

    public string OrganizationId { get; set; } = string.Empty;
    public string OrganizationName { get; internal set; } = string.Empty;
    public string RelativeAccessUri =>
        $"/Viewer?mode=Unattended&sessionId={UnattendedSessionId}&accessKey={AccessKey}&viewonly={(ViewOnly ? "True" : "False")}";
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterUserName { get; internal set; } = string.Empty;
    /// <summary>
    /// Whether the remote control session requires the end user's consent
    /// before the view can connect.
    /// </summary>
    public bool RequireConsent { get; set; }

    /// <summary>
    /// Whether consent has been granted at least once for this session, either
    /// because the end user accepted the prompt or because attended access is
    /// not enforced.  Once this is true, the session survives a restart of the
    /// device's desktop process instead of being deleted from the cache.
    /// </summary>
    public bool ConsentGranted { get; set; }

    public DateTimeOffset StartTime { get; internal set; }

    /// <summary>
    /// Current state of the streamer (desktop process) that's associated
    /// with this session.
    /// </summary>
    public StreamerState StreamerState
    {
        get => _streamerState;
        set
        {
            _streamerState = value;
            LastStateChange = DateTimeOffset.Now;
        }
    }

    public Guid StreamId { get; internal set; }
    public Guid UnattendedSessionId { get; set; }
    public string UserConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Contains a collection of viewer SignalR connection IDs.
    /// </summary>
    public HashSet<string> ViewerList { get; private set; } = new();

    /// <summary>
    /// Creates a new session based off this existing one, but with
    /// a new UnattendedSessionId and AccessKey.
    /// </summary>
    /// <returns></returns>
    public RemoteControlSession CreateNew()
    {
        if (Mode != RemoteControlMode.Unattended)
        {
            throw new InvalidOperationException("Only available in unattended mode.");
        }

        var clone = (RemoteControlSession)MemberwiseClone();
        clone.Created = DateTimeOffset.Now;
        clone.UnattendedSessionId = Guid.NewGuid();
        clone.AccessKey = RandomGenerator.GenerateAccessKey();
        clone.ViewerList = new();

        // SPEC-001 / FR-013, FR-014: lifecycle events must never upgrade a
        // session from ViewOnly to FullControl. The clone keeps the server-owned
        // access mode it was derived from; its own privilege decision is recorded
        // by the caller that performs the clone.
        // Note: MemberwiseClone already copies AccessMode.
        return clone;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public Task<bool> WaitForSessionReady(TimeSpan waitTime)
    {
        return Task.Run(() => _sessionReadySignal.Wait(waitTime));
    }

    public Task<bool> WaitForSessionReady(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                _sessionReadySignal.Wait(cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    internal void SetSessionReadyState(bool isReady)
    {
        try
        {
            if (isReady)
            {
                _sessionReadySignal.Set();
            }
            else
            {
                _sessionReadySignal.Reset();
            }
        }
        catch (ObjectDisposedException)
        {
            // The session was removed from the cache and disposed while the
            // device was disconnecting.  There is nothing left to signal.
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _sessionReadySignal.Dispose();
            }
            _disposedValue = true;
        }
    }
}
