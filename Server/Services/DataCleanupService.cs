using Remotely.Shared.Services;

namespace Remotely.Server.Services;

public class DataCleanupService : BackgroundService, IDisposable
{
    private readonly ILogger<DataCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemTime _systemTime;

    public DataCleanupService(
        IServiceScopeFactory scopeFactory,
        ISystemTime systemTime,
        ILogger<DataCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _systemTime = systemTime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        await PerformCleanup();

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _ = await timer.WaitForNextTickAsync(stoppingToken);
                await PerformCleanup();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Application is shutting down.  Stopping data cleanup service.");
            }

        }
    }

    private async Task PerformCleanup()
    {
        await RunCleanupStep("database records", RemoveExpiredDbRecords);
        await RunCleanupStep("session recordings", RemoveExpiredRecordings);
        await RunCleanupStep("media", RemoveExpiredMedia);
    }

    private async Task RunCleanupStep(string stepName, Func<Task> cleanup)
    {
        try
        {
            await cleanup();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {cleanupStep} cleanup.", stepName);
        }
    }

    private async Task RemoveExpiredDbRecords()
    {
        using var scope = _scopeFactory.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        await dataService.CleanupOldRecords();
    }

    private async Task RemoveExpiredMedia()
    {
        using var scope = _scopeFactory.CreateScope();
        var appDbFactory = scope.ServiceProvider.GetRequiredService<Remotely.Server.Data.IAppDbFactory>();
        var screenshotService = scope.ServiceProvider.GetRequiredService<IScreenshotService>();
        var cameraService = scope.ServiceProvider.GetRequiredService<ICameraService>();
        var audioService = scope.ServiceProvider.GetRequiredService<IAudioService>();

        Remotely.Shared.Entities.Device[] devices;
        using (var dbContext = appDbFactory.GetContext())
        {
            devices = dbContext.Devices.ToArray();
        }

        foreach (var device in devices)
        {
            await screenshotService.DeleteExpiredScreenshots(device);
            await cameraService.DeleteExpiredClips(device);
            await audioService.DeleteExpiredClips(device);
        }
    }

    private async Task RemoveExpiredRecordings()
    {
        using var scope = _scopeFactory.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var settings = await dataService.GetSettings();

        if (!Directory.Exists(SessionRecordingSink.RecordingsDirectory))
        {
            return;
        }

        var expirationDate = _systemTime.Now.UtcDateTime - TimeSpan.FromDays(settings.DataRetentionInDays);

        var files = Directory
            .GetFiles(
                SessionRecordingSink.RecordingsDirectory,
                "*.webm",
                SearchOption.AllDirectories)
            .Select(x => new FileInfo(x))
            .Where(x => x.CreationTimeUtc < expirationDate)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                file.Delete();
                _logger.LogInformation("Expired recording deleted: {file}", file.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting expired recording: {file}", file);
            }
        }
    }
}
