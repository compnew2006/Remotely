using System.Net;
using Microsoft.Extensions.Options;

namespace Remotely.Server.Options;

/// <summary>
/// Validates <see cref="ApplicationOptions"/> when the host starts, so a typo in
/// configuration fails the process with a clear message instead of surfacing
/// later as a silent misconfiguration (for example a Docker gateway address
/// that never makes it into the forwarded-headers allow list).
/// </summary>
public sealed class ApplicationOptionsValidator : IValidateOptions<ApplicationOptions>
{
    /// <summary>
    /// The providers the server's composition root can actually register.
    /// </summary>
    public static readonly string[] AllowedDbProviders =
    [
        "sqlite",
        "sqlserver",
        "postgresql"
    ];

    public ValidateOptionsResult Validate(string? name, ApplicationOptions options)
    {
        var failures = new List<string>();

        var provider = ApplicationOptions.NormalizeDbProvider(options.DbProvider);
        if (!AllowedDbProviders.Contains(provider))
        {
            failures.Add(
                $"{ApplicationOptions.SectionKey}:DbProvider '{options.DbProvider}' is not supported.  " +
                $"Use one of: {string.Join(", ", AllowedDbProviders)}.");
        }

        if (!string.IsNullOrWhiteSpace(options.DockerGatewayIp) &&
            !IPAddress.TryParse(options.DockerGatewayIp, out _))
        {
            failures.Add(
                $"{ApplicationOptions.SectionKey}:DockerGatewayIp '{options.DockerGatewayIp}' is not a valid IP address.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
