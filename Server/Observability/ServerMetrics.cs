using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Remotely.Server.Observability;

/// <summary>
/// The counters that describe how the running server behaves, and the
/// Prometheus text rendering of them.
/// <para>
/// The values are kept in plain concurrent counters instead of
/// <c>System.Diagnostics.Metrics</c> instruments: the endpoint has to render
/// the current values itself, and a second copy of every number inside an
/// instrument would be a second source of truth that can drift.
/// </para>
/// </summary>
public sealed class ServerMetrics
{
    private readonly ConcurrentDictionary<(string Method, int StatusCode), long> _httpRequests = new();
    private readonly ConcurrentDictionary<string, long> _healthChecks = new();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private int _instanceConflict;
    private int _remoteControlAuthenticationRequired = 1;

    /// <summary>
    /// Counts one handled HTTP request.
    /// </summary>
    public void RecordHttpRequest(string method, int statusCode)
    {
        var key = (string.IsNullOrWhiteSpace(method) ? "UNKNOWN" : method, statusCode);
        _httpRequests.AddOrUpdate(key, 1, static (_, current) => current + 1);
    }

    /// <summary>
    /// Counts one readiness probe result.
    /// </summary>
    public void RecordHealthCheck(bool healthy)
    {
        var key = healthy ? "healthy" : "unhealthy";
        _healthChecks.AddOrUpdate(key, 1, static (_, current) => current + 1);
    }

    public double UptimeInSeconds => (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;

    /// <summary>
    /// True when another server instance holds the lease on the database.
    /// </summary>
    public bool InstanceConflict => Volatile.Read(ref _instanceConflict) == 1;

    /// <summary>
    /// Records the current instance conflict state.
    /// </summary>
    public void RecordInstanceConflict(bool conflict)
    {
        Volatile.Write(ref _instanceConflict, conflict ? 1 : 0);
    }

    /// <summary>
    /// Whether the server requires authentication for remote control.
    /// <para>
    /// A server that does not require it lets anyone who knows a session id
    /// or an access key watch and control a device.  The operator has to be
    /// able to see that from the outside, without reading the log.
    /// </para>
    /// </summary>
    public bool RemoteControlAuthenticationRequired =>
        Volatile.Read(ref _remoteControlAuthenticationRequired) == 1;

    /// <summary>
    /// Records whether the server requires authentication for remote control.
    /// </summary>
    public void RecordRemoteControlAuthenticationRequired(bool required)
    {
        Volatile.Write(ref _remoteControlAuthenticationRequired, required ? 1 : 0);
    }

    /// <summary>
    /// The version of the running server, taken from the entry assembly.
    /// </summary>
    public static string BuildVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    /// <summary>
    /// Renders the counters in the Prometheus text exposition format.
    /// </summary>
    public string RenderPrometheus()
    {
        var builder = new StringBuilder();

        builder.AppendLine("# HELP remotely_http_requests_total HTTP requests handled by the server.");
        builder.AppendLine("# TYPE remotely_http_requests_total counter");
        foreach (var (key, count) in _httpRequests.OrderBy(x => x.Key.Method).ThenBy(x => x.Key.StatusCode))
        {
            builder.Append("remotely_http_requests_total{method=\"")
                .Append(EscapeLabel(key.Method))
                .Append("\",status_code=\"")
                .Append(key.StatusCode.ToString(CultureInfo.InvariantCulture))
                .Append("\"} ")
                .Append(count.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        builder.AppendLine("# HELP remotely_health_checks_total Readiness probe results.");
        builder.AppendLine("# TYPE remotely_health_checks_total counter");
        foreach (var (result, count) in _healthChecks.OrderBy(x => x.Key))
        {
            builder.Append("remotely_health_checks_total{result=\"")
                .Append(EscapeLabel(result))
                .Append("\"} ")
                .Append(count.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        builder.AppendLine("# HELP remotely_uptime_seconds Seconds since the server started.");
        builder.AppendLine("# TYPE remotely_uptime_seconds gauge");
        builder.Append("remotely_uptime_seconds ")
            .Append(UptimeInSeconds.ToString("F3", CultureInfo.InvariantCulture))
            .AppendLine();

        builder.AppendLine("# HELP remotely_instance_conflict 1 when another server instance holds the database.");
        builder.AppendLine("# TYPE remotely_instance_conflict gauge");
        builder.Append("remotely_instance_conflict ")
            .Append(InstanceConflict ? "1" : "0")
            .AppendLine();

        builder.AppendLine("# HELP remotely_remote_control_auth_required 1 when remote control requires authentication.");
        builder.AppendLine("# TYPE remotely_remote_control_auth_required gauge");
        builder.Append("remotely_remote_control_auth_required ")
            .Append(RemoteControlAuthenticationRequired ? "1" : "0")
            .AppendLine();

        builder.AppendLine("# HELP remotely_build_info Version of the running server.");
        builder.AppendLine("# TYPE remotely_build_info gauge");
        builder.Append("remotely_build_info{version=\"")
            .Append(EscapeLabel(BuildVersion))
            .Append("\"} 1")
            .AppendLine();

        return builder.ToString();
    }

    private static string EscapeLabel(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
