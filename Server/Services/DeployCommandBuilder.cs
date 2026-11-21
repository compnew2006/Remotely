namespace Remotely.Server.Services;

/// <summary>
/// Builds the copy-and-paste commands that the Deploy and Downloads pages
/// hand to admins.
/// </summary>
/// <remarks>
/// The commands have to work on a machine that has no Remotely account and no
/// API key, so they are built around the static installer files under
/// /Content. The /api/ClientDownloads routes cannot be used here: they sit
/// behind ApiAuthorizationFilter and answer anonymous requests with 401.
/// </remarks>
public static class DeployCommandBuilder
{
    public const string WindowsInstallerFileName = "Install-Remotely.ps1";

    public const string UbuntuInstallerFileName = "Install-Ubuntu-x64.sh";
    public const string ManjaroInstallerFileName = "Install-Manjaro-x64.sh";
    public const string MacOSInstallerFileName = "Install-MacOS-x64.sh";
    public const string MacOSArm64InstallerFileName = "Install-MacOS-arm64.sh";

    /// <summary>
    /// Builds the Windows install command. It downloads the static installer
    /// and runs it with the server url and organization id, because the
    /// installer refuses to run without them.
    /// </summary>
    public static string BuildWindowsCommand(string baseUri, string organizationId)
    {
        return BuildWindowsScript(baseUri, new[]
        {
            $"-ServerUrl '{NormalizeBaseUri(baseUri)}'",
            $"-OrganizationId '{organizationId}'"
        });
    }

    /// <summary>
    /// Builds the Windows install command that suppresses the console output.
    /// </summary>
    public static string BuildWindowsQuietInstallCommand(string baseUri, string organizationId)
    {
        return BuildWindowsScript(baseUri, new[]
        {
            $"-ServerUrl '{NormalizeBaseUri(baseUri)}'",
            $"-OrganizationId '{organizationId}'",
            "-Quiet"
        });
    }

    /// <summary>
    /// Builds the Windows install command for an agent zip that was already
    /// downloaded to the target machine.
    /// </summary>
    public static string BuildWindowsLocalInstallCommand(string baseUri, string organizationId, string zipPath)
    {
        return BuildWindowsScript(baseUri, new[]
        {
            $"-ServerUrl '{NormalizeBaseUri(baseUri)}'",
            $"-OrganizationId '{organizationId}'",
            $"-Path '{zipPath}'"
        });
    }

    /// <summary>
    /// Builds the Windows uninstall command.
    /// </summary>
    public static string BuildWindowsUninstallCommand(string baseUri)
    {
        return BuildWindowsScript(baseUri, new[] { "-Uninstall", "-Quiet" });
    }

    /// <summary>
    /// Builds the Windows install command with every override option the
    /// installer understands.
    /// </summary>
    public static string BuildWindowsOverrideCommand(
        string baseUri,
        string organizationId,
        string deviceAlias,
        string deviceGroup)
    {
        return BuildWindowsScript(baseUri, new[]
        {
            $"-ServerUrl '{NormalizeBaseUri(baseUri)}'",
            $"-OrganizationId '{organizationId}'",
            $"-DeviceAlias '{deviceAlias}'",
            $"-DeviceGroup '{deviceGroup}'",
            "-Quiet"
        });
    }

    /// <summary>
    /// Builds the Linux install command. The static bash installers ship with
    /// empty HostName and Organization placeholders that have to be filled in
    /// before the script can do anything.
    /// </summary>
    public static string BuildLinuxCommand(string baseUri, string organizationId, string installerFileName)
    {
        return BuildLinuxScript(baseUri, organizationId, installerFileName, null);
    }

    /// <summary>
    /// Builds the Linux install command for an agent zip that was already
    /// downloaded to the target machine.
    /// </summary>
    public static string BuildLinuxLocalInstallCommand(
        string baseUri,
        string organizationId,
        string installerFileName,
        string zipPath)
    {
        return BuildLinuxScript(baseUri, organizationId, installerFileName, $"--path '{zipPath}'");
    }

    /// <summary>
    /// Builds the Linux uninstall command.
    /// </summary>
    public static string BuildLinuxUninstallCommand(string baseUri, string organizationId, string installerFileName)
    {
        return BuildLinuxScript(baseUri, organizationId, installerFileName, "--uninstall");
    }

    private static string BuildWindowsScript(string baseUri, IEnumerable<string> arguments)
    {
        var root = NormalizeBaseUri(baseUri);

        return
            $"irm '{root}/Content/{WindowsInstallerFileName}' -OutFile \"$env:TEMP\\{WindowsInstallerFileName}\"; " +
            $"powershell -ExecutionPolicy Bypass -File \"$env:TEMP\\{WindowsInstallerFileName}\" " +
            string.Join(" ", arguments);
    }

    private static string BuildLinuxScript(
        string baseUri,
        string organizationId,
        string installerFileName,
        string? extraArguments)
    {
        var root = NormalizeBaseUri(baseUri);

        return
            $"sudo rm -f /tmp/Install-Remotely.sh && " +
            $"sudo wget -q -O /tmp/Install-Remotely.sh {root}/Content/{installerFileName} && " +
            $"sudo sed -i 's|^HostName=.*|HostName=\"{root}\"|; s|^Organization=.*|Organization=\"{organizationId}\"|' /tmp/Install-Remotely.sh && " +
            $"sudo chmod +x /tmp/Install-Remotely.sh && " +
            $"sudo /tmp/Install-Remotely.sh" +
            (string.IsNullOrWhiteSpace(extraArguments) ? string.Empty : $" {extraArguments}");
    }

    private static string NormalizeBaseUri(string baseUri)
    {
        return (baseUri ?? string.Empty).TrimEnd('/');
    }
}
