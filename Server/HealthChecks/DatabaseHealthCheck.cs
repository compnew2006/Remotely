using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Remotely.Server.Data;
using Remotely.Server.Observability;

namespace Remotely.Server.HealthChecks;

/// <summary>
/// Readiness probe for the configured database.  It checks that the database
/// can be reached and that an application table is queryable, which also
/// catches a schema that was never migrated.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    public const string Name = "database";

    private readonly IAppDbFactory _appDbFactory;
    private readonly ServerMetrics _metrics;

    public DatabaseHealthCheck(IAppDbFactory appDbFactory, ServerMetrics metrics)
    {
        _appDbFactory = appDbFactory;
        _metrics = metrics;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = _appDbFactory.GetContext();

            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                _metrics.RecordHealthCheck(false);
                return HealthCheckResult.Unhealthy("The database cannot be reached.");
            }

            // CanConnectAsync only opens a connection.  A database file without
            // the migrated schema still connects, so touch a real table too.
            _ = await dbContext.Organizations
                .AsNoTracking()
                .Select(x => x.ID)
                .FirstOrDefaultAsync(cancellationToken);

            _metrics.RecordHealthCheck(true);
            return HealthCheckResult.Healthy("The database is reachable.");
        }
        catch (Exception ex)
        {
            // Provider exceptions can contain the connection string.  The
            // exception stays in the health report (and the logs); the HTTP
            // response only reports the status.
            _metrics.RecordHealthCheck(false);
            return HealthCheckResult.Unhealthy("The database check failed.", ex);
        }
    }
}
