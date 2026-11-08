using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Shared.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Remotely.Agent.Services;

public interface IScriptingShellFactory
{
    Task<IExternalScriptingShell> GetOrCreateExternalShell(ScriptingShell shell, string senderConnectionId);
    IPsCoreShell GetOrCreatePsCoreShell(string senderConnectionId);
}

internal class ScriptingShellFactory : IScriptingShellFactory, IDisposable
{
    private static readonly TimeSpan ShellIdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, ShellCacheEntry> _sessionCache = new();
    private readonly ConcurrentDictionary<string, Lazy<ShellCacheEntry>> _psCoreShellCreations = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<ShellCacheEntry>>> _externalShellCreations = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScriptingShellFactory> _logger;
    private readonly Timer _cleanupTimer;
    private int _cleanupRunning;

    public ScriptingShellFactory(
        IServiceProvider serviceProvider,
        ILogger<ScriptingShellFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _cleanupTimer = new Timer(
            _ => CleanupIdleShells(),
            null,
            CleanupInterval,
            CleanupInterval);
    }

    public IPsCoreShell GetOrCreatePsCoreShell(string senderConnectionId)
    {
        var cacheKey = $"PSCore-{senderConnectionId}";

        while (true)
        {
            if (TryGetPsCoreShell(cacheKey, out var shell))
            {
                return shell;
            }

            var creation = _psCoreShellCreations.GetOrAdd(
                cacheKey,
                _ => new Lazy<ShellCacheEntry>(
                    () => CreatePsCoreShell(cacheKey, senderConnectionId),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                if (creation.Value.TryGet<IPsCoreShell>(out var createdShell))
                {
                    return createdShell;
                }
            }
            finally
            {
                TryRemoveExact(_psCoreShellCreations, cacheKey, creation);
            }
        }
    }

    public async Task<IExternalScriptingShell> GetOrCreateExternalShell(
        ScriptingShell shell,
        string senderConnectionId)
    {
        var cacheKey = $"{shell}-{senderConnectionId}";

        while (true)
        {
            if (TryGetExternalShell(cacheKey, out var scriptingShell))
            {
                return scriptingShell;
            }

            var creation = _externalShellCreations.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<ShellCacheEntry>>(
                    () => CreateExternalShell(cacheKey, shell, senderConnectionId),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                var entry = await creation.Value.ConfigureAwait(false);
                if (entry.TryGet<IExternalScriptingShell>(out var createdShell))
                {
                    return createdShell;
                }
            }
            finally
            {
                TryRemoveExact(_externalShellCreations, cacheKey, creation);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();

        foreach (var pair in _sessionCache)
        {
            if (TryRemoveExact(_sessionCache, pair.Key, pair.Value))
            {
                DisposeShell(pair.Value);
            }
        }
    }

    private ShellCacheEntry CreatePsCoreShell(string cacheKey, string senderConnectionId)
    {
        if (_sessionCache.TryGetValue(cacheKey, out var existingEntry) &&
            existingEntry.TryGet<IPsCoreShell>(out _))
        {
            return existingEntry;
        }

        var shell = _serviceProvider.GetRequiredService<IPsCoreShell>();
        shell.SenderConnectionId = senderConnectionId;

        var entry = new ShellCacheEntry(shell);
        _sessionCache[cacheKey] = entry;
        return entry;
    }

    private async Task<ShellCacheEntry> CreateExternalShell(
        string cacheKey,
        ScriptingShell shell,
        string senderConnectionId)
    {
        if (_sessionCache.TryGetValue(cacheKey, out var existingEntry) &&
            existingEntry.TryGet<IExternalScriptingShell>(out _))
        {
            return existingEntry;
        }

        var session = _serviceProvider.GetRequiredService<IExternalScriptingShell>();

        try
        {
            switch (shell)
            {
                case ScriptingShell.WinPS:
                    await session.Init(shell, "powershell.exe", "\r\n", senderConnectionId).ConfigureAwait(false);
                    break;
                case ScriptingShell.Bash:
                    await session.Init(shell, "bash", "\n", senderConnectionId).ConfigureAwait(false);
                    break;
                case ScriptingShell.CMD:
                    await session.Init(shell, "cmd.exe", "\r\n", senderConnectionId).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentException($"Unknown external scripting shell type: {shell}");
            }

            var entry = new ShellCacheEntry(session);
            _sessionCache[cacheKey] = entry;
            return entry;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private bool TryGetPsCoreShell(string cacheKey, out IPsCoreShell shell)
    {
        if (_sessionCache.TryGetValue(cacheKey, out var entry) &&
            entry.TryGet(out shell))
        {
            return true;
        }

        shell = null!;
        return false;
    }

    private bool TryGetExternalShell(string cacheKey, out IExternalScriptingShell shell)
    {
        if (_sessionCache.TryGetValue(cacheKey, out var entry) &&
            entry.TryGet(out shell))
        {
            return true;
        }

        shell = null!;
        return false;
    }

    private void CleanupIdleShells()
    {
        if (Interlocked.Exchange(ref _cleanupRunning, 1) != 0)
        {
            return;
        }

        try
        {
            var idleBefore = DateTimeOffset.UtcNow - ShellIdleTimeout;

            foreach (var pair in _sessionCache)
            {
                if (pair.Value.TryEvictIfIdle(
                    idleBefore,
                    () => TryRemoveExact(_sessionCache, pair.Key, pair.Value)))
                {
                    DisposeShell(pair.Value);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _cleanupRunning, 0);
        }
    }

    private void DisposeShell(ShellCacheEntry entry)
    {
        try
        {
            entry.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing an idle scripting shell.");
        }
    }

    private static bool TryRemoveExact<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value)
        where TKey : notnull
    {
        return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary)
            .Remove(new KeyValuePair<TKey, TValue>(key, value));
    }

    private sealed class ShellCacheEntry : IDisposable
    {
        private readonly object _sync = new();
        private DateTimeOffset _lastAccess = DateTimeOffset.UtcNow;
        private bool _evicted;

        public ShellCacheEntry(IScriptingShell shell)
        {
            Shell = shell;
        }

        private IScriptingShell Shell { get; }

        public bool TryGet<TShell>(out TShell shell)
            where TShell : class, IScriptingShell
        {
            lock (_sync)
            {
                if (!_evicted &&
                    !Shell.IsDisposed &&
                    Shell is TShell typedShell)
                {
                    _lastAccess = DateTimeOffset.UtcNow;
                    shell = typedShell;
                    return true;
                }
            }

            shell = null!;
            return false;
        }

        public bool TryEvictIfIdle(DateTimeOffset idleBefore, Func<bool> remove)
        {
            lock (_sync)
            {
                if (_evicted)
                {
                    return false;
                }

                if (Shell.IsBusy)
                {
                    _lastAccess = DateTimeOffset.UtcNow;
                    return false;
                }

                if (!Shell.IsDisposed && _lastAccess >= idleBefore)
                {
                    return false;
                }

                if (remove())
                {
                    _evicted = true;
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _evicted = true;
            }

            Shell.Dispose();
        }
    }
}
