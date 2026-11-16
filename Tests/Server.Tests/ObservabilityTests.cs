#nullable enable

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Extensions;
using Remotely.Server.Observability;

namespace Remotely.Server.Tests;

[TestClass]
public class ObservabilityTests
{
    [TestMethod]
    public async Task CorrelationId_UsesTheSuppliedId_WhenItIsSafe()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "req-123_ABC.1";

        await InvokeMiddlewareAsync(context);

        Assert.AreEqual("req-123_ABC.1", context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.AreEqual(
            "req-123_ABC.1",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [TestMethod]
    public async Task CorrelationId_ReplacesAnUnsafeId()
    {
        // A value with spaces or a line break would end up in a response
        // header and in the log file, so it must not be used.
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "bad id\r\nInjected: 1";

        await InvokeMiddlewareAsync(context);

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;

        Assert.IsNotNull(correlationId);
        Assert.AreNotEqual("bad id\r\nInjected: 1", correlationId);
        Assert.IsFalse(correlationId!.Contains('\n'));
        Assert.IsFalse(correlationId.Contains('\r'));
        Assert.IsTrue(correlationId.Length <= 64);
    }

    [TestMethod]
    public async Task CorrelationId_GeneratesAnId_WhenTheRequestHasNone()
    {
        var context = new DefaultHttpContext();

        await InvokeMiddlewareAsync(context);

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;

        Assert.IsNotNull(correlationId);
        Assert.IsTrue(Guid.TryParseExact(correlationId, "N", out _));
    }

    [TestMethod]
    public void ServerMetrics_RendersTheCountersInPrometheusFormat()
    {
        var metrics = new ServerMetrics();

        metrics.RecordHttpRequest("GET", 200);
        metrics.RecordHttpRequest("GET", 200);
        metrics.RecordHttpRequest("POST", 503);
        metrics.RecordHealthCheck(true);
        metrics.RecordHealthCheck(false);

        var rendered = metrics.RenderPrometheus();

        StringAssert.Contains(rendered, "remotely_http_requests_total{method=\"GET\",status_code=\"200\"} 2");
        StringAssert.Contains(rendered, "remotely_http_requests_total{method=\"POST\",status_code=\"503\"} 1");
        StringAssert.Contains(rendered, "remotely_health_checks_total{result=\"healthy\"} 1");
        StringAssert.Contains(rendered, "remotely_health_checks_total{result=\"unhealthy\"} 1");
        StringAssert.Contains(rendered, "# TYPE remotely_http_requests_total counter");
        StringAssert.Contains(rendered, "remotely_uptime_seconds ");
        StringAssert.Contains(rendered, "remotely_build_info{version=\"");
    }

    [TestMethod]
    public void MetricsEndpoint_IsOnlyReachableFromTheLocalMachine()
    {
        var local = new DefaultHttpContext();
        local.Connection.RemoteIpAddress = IPAddress.Loopback;

        var remote = new DefaultHttpContext();
        remote.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        Assert.IsTrue(ObservabilityExtensions.IsLocalCaller(local));
        Assert.IsFalse(ObservabilityExtensions.IsLocalCaller(remote));
    }

    private static async Task InvokeMiddlewareAsync(HttpContext context)
    {
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled, "The next middleware in the pipeline must run.");
    }
}
