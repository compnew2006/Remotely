using Microsoft.Extensions.Hosting;

namespace Remotely.Server.Services;

/// <summary>
/// Signs the client packages that are published by the server, so a device
/// can verify them and update itself.  The signature is written next to the
/// package, and the agents pick it up on their next update check.
/// </summary>
public class UpdatePackageSigningService : BackgroundService
{
    private static readonly string[] _platforms = ["x64", "x86"];

    private readonly IUpdateSigningService _signingService;
    private readonly ILogger<UpdatePackageSigningService> _logger;

    public UpdatePackageSigningService(
        IUpdateSigningService signingService,
        ILogger<UpdatePackageSigningService> logger)
    {
        _signingService = signingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first pass signs the packages that are already published.
        await SignAllAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SignAllAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The server is stopping.
        }
    }

    private async Task SignAllAsync(CancellationToken cancellationToken)
    {
        foreach (var platform in _platforms)
        {
            try
            {
                await _signingService.SignIfChangedAsync(platform, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while signing the client package for {platform}.", platform);
            }
        }
    }
}
