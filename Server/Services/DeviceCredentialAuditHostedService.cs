using Microsoft.Extensions.Logging;
using Remotely.Server.Observability;

namespace Remotely.Server.Services;

/// <summary>
/// Counts the devices that have no credential bound yet.
/// <para>
/// A device without a credential can still be claimed by the first caller
/// that registers it, so the count is what the operator needs to see to know
/// how much of the fleet is protected.  The devices themselves are a query
/// away; the query for every provider is in docs/OPERATIONS.md.
/// </para>
/// <para>
/// The count is a signal, not a reason to refuse anything.  A device binds its
/// credential at the first registration that presents one.
/// </para>
/// </summary>
public sealed class DeviceCredentialAuditHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IDeviceService _deviceService;
    private readonly ServerMetrics _metrics;
    private readonly ILogger<DeviceCredentialAuditHostedService> _logger;

    private int _lastReportedCount = -1;

    public DeviceCredentialAuditHostedService(
        IDeviceService deviceService,
        ServerMetrics metrics,
        ILogger<DeviceCredentialAuditHostedService> logger)
    {
        _deviceService = deviceService;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while counting the devices without a credential.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReportAsync()
    {
        var count = await _deviceService.GetDevicesWithoutCredentialCountAsync();

        _metrics.RecordDevicesWithoutCredential(count);

        if (count == _lastReportedCount)
        {
            return;
        }

        _lastReportedCount = count;

        if (count > 0)
        {
            _logger.LogWarning(
                "{count} device(s) have no credential bound yet.  A device without a credential can still be claimed by the first caller that registers it.",
                count);
        }
    }
}
