using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Remotely.Server.Auth;
using Remotely.Server.HealthChecks;

namespace Remotely.Server.API;

/// <summary>
/// Can only be accessed from the local machine.  The sole purpose
/// is to provide a healthcheck endpoint for Docker that exercises
/// the database connection.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[ServiceFilter(typeof(LocalOnlyFilter))]
public class HealthCheckController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthCheckController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(
            registration => registration.Name == DatabaseHealthCheck.Name,
            cancellationToken);

        // The container healthcheck runs "curl -f", so a database that cannot
        // be reached has to answer with a failing status code; otherwise the
        // container stays "healthy" while the server cannot serve anything.
        return report.Status == HealthStatus.Healthy
            ? Ok()
            : StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
