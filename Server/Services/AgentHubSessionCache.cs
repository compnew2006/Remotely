using Remotely.Shared.Entities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Remotely.Server.Services;

public interface IAgentHubSessionCache
{
    Task<IDisposable> AcquireDeviceLockAsync(string deviceId);
    void AddOrUpdateByConnectionId(string connectionId, Device device);
    IEnumerable<string> FilterDevicesByOnlineStatus(IEnumerable<string> deviceIds, bool isOnline);

    ICollection<Device> GetAllDevices();
    IEnumerable<string> GetConnectionIdsByDeviceIds(IEnumerable<string> deviceIds);
    bool TryGetByDeviceId(string deviceId, [NotNullWhen(true)] out Device? device);
    bool TryGetConnectionId(string deviceId, [NotNullWhen(true)] out string? serviceConnectionId);
    bool TryRemoveByConnectionId(string connectionId, [NotNullWhen(true)] out Device? device);
}

public class AgentHubSessionCache : IAgentHubSessionCache
{
    private readonly ConcurrentDictionary<string, Device> _connectionIdToDeviceLookup = new();
    private readonly ConcurrentDictionary<string, string> _deviceIdToConnectionIdLookup = new();
    private readonly object _deviceLockEntriesLock = new();
    private readonly Dictionary<string, DeviceLockEntry> _deviceLockEntries = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireDeviceLockAsync(string deviceId)
    {
        DeviceLockEntry entry;

        lock (_deviceLockEntriesLock)
        {
            if (_deviceLockEntries.TryGetValue(deviceId, out var existingEntry))
            {
                entry = existingEntry;
            }
            else
            {
                entry = new DeviceLockEntry();
                _deviceLockEntries.Add(deviceId, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync();
            return new DeviceLockLease(this, deviceId, entry);
        }
        catch
        {
            ReleaseDeviceLockReference(deviceId, entry);
            throw;
        }
    }

    public void AddOrUpdateByConnectionId(string connectionId, Device device)
    {
        _connectionIdToDeviceLookup.AddOrUpdate(connectionId, device, (k, v) => device);
        _deviceIdToConnectionIdLookup.AddOrUpdate(device.ID, connectionId, (k, v) => connectionId);
    }

    public IEnumerable<string> FilterDevicesByOnlineStatus(IEnumerable<string> deviceIds, bool isOnline)
    {
        foreach (var deviceId in deviceIds)
        {
            var result = TryGetConnectionId(deviceId, out _);
            if (result == isOnline)
            {
                yield return deviceId;
            }
        }
    }

    public ICollection<Device> GetAllDevices() => _connectionIdToDeviceLookup.Values;

    public IEnumerable<string> GetConnectionIdsByDeviceIds(IEnumerable<string> deviceIds)
    {
        foreach (var deviceId in deviceIds)
        {
            if (TryGetConnectionId(deviceId, out var connectionId))
            {
                yield return connectionId;
            }
        }
    }

    public bool TryGetByDeviceId(string deviceId, [NotNullWhen(true)] out Device? device)
    {
        if (_deviceIdToConnectionIdLookup.TryGetValue(deviceId, out var connectionId) &&
            _connectionIdToDeviceLookup.TryGetValue(connectionId, out device))
        {
            return true;
        }
        device = default;
        return false;
    }

    public bool TryGetConnectionId(string deviceId, [NotNullWhen(true)] out string? serviceConnectionId)
    {
        return _deviceIdToConnectionIdLookup.TryGetValue(deviceId, out serviceConnectionId);
    }

    public bool TryRemoveByConnectionId(string connectionId, [NotNullWhen(true)] out Device? device)
    {
        if (_connectionIdToDeviceLookup.TryRemove(connectionId, out var lookupResult))
        {
            // Remove the device mapping only if it still points at the
            // connection being removed. A newer connection may have replaced
            // this one since the old connection was registered.
            var removedCurrentMapping =
                ((ICollection<KeyValuePair<string, string>>)_deviceIdToConnectionIdLookup)
                    .Remove(new KeyValuePair<string, string>(lookupResult.ID, connectionId));

            if (removedCurrentMapping)
            {
                device = lookupResult;
                return true;
            }
        }

        device = null;
        return false;
    }

    private void ReleaseDeviceLock(string deviceId, DeviceLockEntry entry)
    {
        lock (_deviceLockEntriesLock)
        {
            entry.Semaphore.Release();
            ReleaseDeviceLockReferenceUnderLock(deviceId, entry);
        }
    }

    private void ReleaseDeviceLockReference(string deviceId, DeviceLockEntry entry)
    {
        lock (_deviceLockEntriesLock)
        {
            ReleaseDeviceLockReferenceUnderLock(deviceId, entry);
        }
    }

    private void ReleaseDeviceLockReferenceUnderLock(string deviceId, DeviceLockEntry entry)
    {
        entry.ReferenceCount--;

        if (entry.ReferenceCount == 0 &&
            _deviceLockEntries.TryGetValue(deviceId, out var currentEntry) &&
            ReferenceEquals(currentEntry, entry))
        {
            _deviceLockEntries.Remove(deviceId);
            entry.Semaphore.Dispose();
        }
    }

    private sealed class DeviceLockEntry
    {
        public System.Threading.SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class DeviceLockLease : IDisposable
    {
        private AgentHubSessionCache? _owner;
        private readonly string _deviceId;
        private readonly DeviceLockEntry _entry;

        public DeviceLockLease(AgentHubSessionCache owner, string deviceId, DeviceLockEntry entry)
        {
            _owner = owner;
            _deviceId = deviceId;
            _entry = entry;
        }

        public void Dispose()
        {
            System.Threading.Interlocked.Exchange(ref _owner, null)?
                .ReleaseDeviceLock(_deviceId, _entry);
        }
    }
}
