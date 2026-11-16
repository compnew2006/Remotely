using Microsoft.Extensions.Caching.Memory;

namespace Remotely.Server.Services;

/// <summary>
/// Holds the command id that the agent reported back for an API-initiated
/// script run, keyed by the request id the API generated.
/// <para>
/// This used to be a static <see cref="IMemoryCache"/> on the hub.  Static
/// state is shared by every circuit in the process, is never disposed with the
/// host, and cannot be injected in a test.  It is a registered singleton
/// instead, with a bounded size so a stream of requests cannot grow it without
/// limit.
/// </para>
/// </summary>
public interface IApiScriptResultCache
{
    bool Contains(string requestId);

    void Remove(string requestId);

    void Set(string requestId, string commandId);

    bool TryTake(string requestId, out string? commandId);
}

public sealed class ApiScriptResultCache : IApiScriptResultCache, IDisposable
{
    private const int MaxEntries = 10_000;

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = MaxEntries
    });

    public void Set(string requestId, string commandId)
    {
        _cache.Set(requestId, commandId, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            Size = 1
        });
    }

    public bool Contains(string requestId) => _cache.TryGetValue(requestId, out _);

    public bool TryTake(string requestId, out string? commandId)
    {
        if (_cache.TryGetValue(requestId, out commandId))
        {
            Remove(requestId);
            return true;
        }

        commandId = null;
        return false;
    }

    public void Remove(string requestId) => _cache.Remove(requestId);

    public void Dispose() => _cache.Dispose();
}
