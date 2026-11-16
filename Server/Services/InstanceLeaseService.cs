using System.Text.Json;
using System.Text.Json.Serialization;
using Remotely.Server.Data;
using Remotely.Shared.Entities;
using Remotely.Shared.Services;

namespace Remotely.Server.Services;

/// <summary>
/// Keeps a lease in the database that tells the operator whether another
/// server instance is using the same database.
/// <para>
/// The server has no SignalR backplane, so a second instance would not receive
/// the sessions and the device connections of the first one.  The lease turns
/// that limitation into a visible signal instead of a silent, hard to debug
/// misconfiguration.  It never refuses to start: the operator decides.
/// </para>
/// </summary>
public interface IInstanceLeaseService
{
    /// <summary>
    /// True when another instance holds a fresh lease on this database.
    /// </summary>
    bool HasConflict { get; }

    /// <summary>
    /// Takes the lease, or reports the instance that already holds it.
    /// </summary>
    Task<InstanceLease?> AcquireAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews the lease.  Returns false when another instance took it.
    /// </summary>
    Task<bool> RenewAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The lease record.  It is stored in the KeyValueRecords table, so no
/// migration is needed for it.
/// </summary>
public sealed class InstanceLease
{
    /// <summary>
    /// The well-known key of the lease record.
    /// </summary>
    public static readonly Guid LeaseKey =
        Guid.Parse("6f5f5d3e-1c2b-4a5d-9e8f-7a6b5c4d3e2f");

    [JsonPropertyName("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [JsonPropertyName("machineName")]
    public string MachineName { get; set; } = string.Empty;

    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    [JsonPropertyName("renewedAt")]
    public DateTimeOffset RenewedAt { get; set; }
}

/// <summary>
/// Acquires and renews the instance lease.
/// </summary>
public sealed class InstanceLeaseService : IInstanceLeaseService
{
    /// <summary>
    /// How long a lease stays valid without a renewal.  A crashed instance
    /// releases the database this way.
    /// </summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(90);

    private readonly IAppDbFactory _appDbFactory;
    private readonly ISystemTime _systemTime;
    private readonly ILogger<InstanceLeaseService> _logger;
    private readonly string _instanceId;
    private readonly string _machineName;
    private readonly object _leaseLock = new();

    private bool _hasConflict;

    public InstanceLeaseService(
        IAppDbFactory appDbFactory,
        ISystemTime systemTime,
        ILogger<InstanceLeaseService> logger)
    {
        _appDbFactory = appDbFactory;
        _systemTime = systemTime;
        _logger = logger;
        _instanceId = Guid.NewGuid().ToString("N");
        _machineName = Environment.MachineName;
    }

    public bool HasConflict
    {
        get
        {
            lock (_leaseLock)
            {
                return _hasConflict;
            }
        }
    }

    public async Task<InstanceLease?> AcquireAsync(CancellationToken cancellationToken = default)
    {
        lock (_leaseLock)
        {
            using var dbContext = _appDbFactory.GetContext();

            var stored = ReadLease(dbContext);
            var now = _systemTime.Now;

            if (stored is not null &&
                !string.IsNullOrEmpty(stored.InstanceId) &&
                stored.InstanceId != _instanceId &&
                now - stored.RenewedAt < LeaseDuration)
            {
                _hasConflict = true;

                _logger.LogWarning(
                    "Another server instance is using this database.  " +
                    "Instance {instanceId} on {machineName} renewed the lease at {renewedAt}.  " +
                    "The server has no SignalR backplane, so a second instance does not " +
                    "receive the sessions of the first one.",
                    stored.InstanceId,
                    stored.MachineName,
                    stored.RenewedAt);

                return stored;
            }

            _hasConflict = false;

            WriteLease(dbContext, now);

            return null;
        }
    }

    public async Task<bool> RenewAsync(CancellationToken cancellationToken = default)
    {
        lock (_leaseLock)
        {
            using var dbContext = _appDbFactory.GetContext();

            var stored = ReadLease(dbContext);
            var now = _systemTime.Now;

            if (stored is not null &&
                !string.IsNullOrEmpty(stored.InstanceId) &&
                stored.InstanceId != _instanceId &&
                now - stored.RenewedAt < LeaseDuration)
            {
                _hasConflict = true;

                _logger.LogWarning(
                    "The instance lease was taken over by instance {instanceId} on {machineName}. " +
                    "This instance no longer owns the database.",
                    stored.InstanceId,
                    stored.MachineName);

                return false;
            }

            _hasConflict = false;

            WriteLease(dbContext, now);

            return true;
        }
    }

    private InstanceLease? ReadLease(AppDb dbContext)
    {
        var record = dbContext.KeyValueRecords.Find(InstanceLease.LeaseKey);

        if (record is null || string.IsNullOrWhiteSpace(record.Value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstanceLease>(record.Value);
        }
        catch (JsonException)
        {
            // A record that cannot be read is treated as no lease at all.
            return null;
        }
    }

    private void WriteLease(AppDb dbContext, DateTimeOffset now)
    {
        var record = dbContext.KeyValueRecords.Find(InstanceLease.LeaseKey);

        if (record is null)
        {
            record = new KeyValueRecord { Key = InstanceLease.LeaseKey };
            dbContext.KeyValueRecords.Add(record);
        }

        record.Value = JsonSerializer.Serialize(new InstanceLease
        {
            InstanceId = _instanceId,
            MachineName = _machineName,
            ProcessId = Environment.ProcessId,
            RenewedAt = now
        });

        dbContext.SaveChanges();
    }
}
