using Remotely.Server.Observability;

namespace Remotely.Server.Services;

/// <summary>
/// Renews the instance lease while the server runs, and keeps the conflict
/// state visible in the metrics.
/// </summary>
public sealed class InstanceLeaseHostedService : BackgroundService
{
    private static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(30);

    private readonly IInstanceLeaseService _leaseService;
    private readonly ServerMetrics _metrics;
    private readonly ILogger<InstanceLeaseHostedService> _logger;

    public InstanceLeaseHostedService(
        IInstanceLeaseService leaseService,
        ServerMetrics metrics,
        ILogger<InstanceLeaseHostedService> logger)
    {
        _leaseService = leaseService;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            await _leaseService.AcquireAsync(stoppingToken);
            _metrics.RecordInstanceConflict(_leaseService.HasConflict);

            using var timer = new PeriodicTimer(RenewalInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _leaseService.RenewAsync(stoppingToken);
                _metrics.RecordInstanceConflict(_leaseService.HasConflict);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down.
        }
        catch (Exception ex)
        {
            // The lease is a diagnostic signal, not a reason to stop the
            // server.
            _logger.LogError(ex, "Error while renewing the instance lease.");
        }
    }
}
