using System.Text.RegularExpressions;
using Serilog.Context;

namespace Remotely.Server.Observability;

/// <summary>
/// Gives every request a correlation id and puts it into the logging context,
/// so one request can be followed through the log file.
/// <para>
/// The id is taken from the caller when it is safe to do so, and generated
/// otherwise.  A caller-supplied value is written into a response header and
/// into the log, so it is limited to characters that cannot forge a log line
/// or split a header.
/// </para>
/// </summary>
public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private const int MaxLength = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeCorrelationId();

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadOrCreate(context);

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ReadOrCreate(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(supplied) &&
            supplied.Length <= MaxLength &&
            SafeCorrelationId().IsMatch(supplied))
        {
            return supplied;
        }

        return Guid.NewGuid().ToString("N");
    }
}
