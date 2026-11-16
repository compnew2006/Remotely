using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Remotely.Server.Observability;

namespace Remotely.Server.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Assigns a correlation id to every request and adds it to the log
    /// context.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Counts the handled HTTP requests.
    /// </summary>
    public static IApplicationBuilder UseRequestMetrics(
        this IApplicationBuilder app,
        ServerMetrics metrics)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            finally
            {
                metrics.RecordHttpRequest(
                    context.Request.Method,
                    context.Response.StatusCode);
            }
        });
    }

    /// <summary>
    /// True when the request came from the machine the server runs on.
    /// </summary>
    public static bool IsLocalCaller(HttpContext context)
    {
        return context.Connection.RemoteIpAddress is { } remote &&
               IPAddress.IsLoopback(remote);
    }

    /// <summary>
    /// Exposes the counters in the Prometheus text format.
    /// <para>
    /// The endpoint runs a database query in its health check and answers with
    /// the counters of the process, so it is restricted to local callers, the
    /// same way the readiness probe is.
    /// </para>
    /// </summary>
    public static IEndpointRouteBuilder MapMetrics(
        this IEndpointRouteBuilder endpoints,
        ServerMetrics metrics)
    {
        endpoints.MapGet("/metrics", (HttpContext context) =>
        {
            if (!IsLocalCaller(context))
            {
                return Results.NotFound();
            }

            return Results.Text(
                metrics.RenderPrometheus(),
                "text/plain; version=0.0.4; charset=utf-8");
        }).AllowAnonymous();

        return endpoints;
    }
}
