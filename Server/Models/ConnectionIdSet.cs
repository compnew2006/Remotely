using System.Collections;

namespace Remotely.Server.Models;

/// <summary>
/// The set of viewer SignalR connection ids attached to a session.
/// <para>
/// It replaces a bare <see cref="HashSet{T}"/> that was mutated from
/// <see cref="Hubs.ViewerHub"/>, <see cref="Hubs.DesktopHub"/> and
/// <see cref="Services.RemoteControlSessionReconnector"/> on different
/// threads.  A HashSet is not safe for concurrent writes, and enumerating one
/// while another hub added or removed a viewer could throw or corrupt the set.
/// Every member here takes the same lock, and enumeration reads a snapshot.
/// </para>
/// </summary>
public sealed class ConnectionIdSet : IReadOnlyList<string>
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _ids.Count;
            }
        }
    }

    public string this[int index] => Snapshot()[index];

    public bool Add(string connectionId)
    {
        lock (_lock)
        {
            return _ids.Add(connectionId);
        }
    }

    public bool Remove(string connectionId)
    {
        lock (_lock)
        {
            return _ids.Remove(connectionId);
        }
    }

    public bool Contains(string connectionId)
    {
        lock (_lock)
        {
            return _ids.Contains(connectionId);
        }
    }

    /// <summary>
    /// A stable copy of the ids.  Callers that broadcast to the whole set should
    /// take one snapshot per broadcast so a viewer that attaches mid-broadcast
    /// does not change the recipient list half way through.
    /// </summary>
    public string[] Snapshot()
    {
        lock (_lock)
        {
            return [.. _ids];
        }
    }

    public IEnumerator<string> GetEnumerator()
    {
        return ((IEnumerable<string>)Snapshot()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
