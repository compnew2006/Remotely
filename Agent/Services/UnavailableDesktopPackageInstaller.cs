using Remotely.Shared.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Remotely.Agent.Services;

/// <summary>
/// Used on the platforms that have no desktop client that the agent can
/// start.  It reports the same failure that the launcher used to report when
/// the executable was missing from the package.
/// </summary>
public class UnavailableDesktopPackageInstaller : IDesktopPackageInstaller
{
    public Task<DesktopPackageResult> EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DesktopPackageResult.Fail(
            DesktopPackageFailure.ManifestUnavailable,
            "This platform does not have a desktop client."));
    }
}
