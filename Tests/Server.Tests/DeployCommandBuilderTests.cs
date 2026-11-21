using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;

namespace Remotely.Server.Tests;

[TestClass]
public class DeployCommandBuilderTests
{
    [TestMethod]
    [Description("The Windows command downloads the static installer instead of using the API route, because the API route answers anonymous requests with 401.")]
    public void BuildWindowsCommand_UsesStaticInstaller()
    {
        var command = DeployCommandBuilder.BuildWindowsCommand("http://host:5000/", "org-id");

        Assert.IsTrue(
            command.Contains("http://host:5000/Content/Install-Remotely.ps1"),
            command);
        Assert.IsFalse(command.Contains("/api/ClientDownloads"), command);
    }

    [TestMethod]
    [Description("The Windows command passes the server url without a trailing slash, because the agent appends '/hubs/service' to it.")]
    public void BuildWindowsCommand_ServerUrlHasNoTrailingSlash()
    {
        var command = DeployCommandBuilder.BuildWindowsCommand("http://host:5000/", "org-id");

        Assert.IsTrue(command.Contains("-ServerUrl 'http://host:5000'"), command);
        Assert.IsFalse(command.Contains("host:5000/'"), command);
    }

    [TestMethod]
    [Description("The Windows command bypasses the execution policy for that process only, because downloaded scripts are blocked by the default policy on Windows clients.")]
    public void BuildWindowsCommand_BypassesExecutionPolicy()
    {
        var command = DeployCommandBuilder.BuildWindowsCommand("http://host:5000/", "org-id");

        Assert.IsTrue(command.Contains("-ExecutionPolicy Bypass"), command);
    }

    [TestMethod]
    [Description("The Windows command passes the organization id to the installer, because the installer refuses to run without it.")]
    public void BuildWindowsCommand_PassesOrganizationId()
    {
        var command = DeployCommandBuilder.BuildWindowsCommand("http://host:5000/", "org-id");

        Assert.IsTrue(command.Contains("-OrganizationId 'org-id'"), command);
    }

    [TestMethod]
    [Description("The Windows command only uses parameters that the installer actually accepts.")]
    public void BuildWindowsCommand_UsesOnlyRealInstallerParameters()
    {
        var command = DeployCommandBuilder.BuildWindowsCommand("http://host:5000/", "org-id");

        foreach (var invalid in new[] { "-install", "-supportshortcut", "-deviceuuid" })
        {
            Assert.IsFalse(command.Contains(invalid), $"{invalid} is not a parameter of Install-Remotely.ps1: {command}");
        }
    }

    [TestMethod]
    [Description("The quiet install command adds the Quiet switch that the installer supports.")]
    public void BuildWindowsQuietInstallCommand_AddsQuietSwitch()
    {
        var command = DeployCommandBuilder.BuildWindowsQuietInstallCommand("http://host:5000/", "org-id");

        Assert.IsTrue(command.Contains("-Quiet"), command);
        Assert.IsTrue(command.Contains("-OrganizationId 'org-id'"), command);
    }

    [TestMethod]
    [Description("The local install command points the installer at an agent zip that was already downloaded.")]
    public void BuildWindowsLocalInstallCommand_PassesTheZipPath()
    {
        var command = DeployCommandBuilder.BuildWindowsLocalInstallCommand(
            "http://host:5000/",
            "org-id",
            "C:\\Temp\\Remotely-Win-x64.zip");

        Assert.IsTrue(command.Contains("-Path 'C:\\Temp\\Remotely-Win-x64.zip'"), command);
    }

    [TestMethod]
    [Description("The uninstall command uses the Uninstall switch and does not need the organization id.")]
    public void BuildWindowsUninstallCommand_UsesUninstallSwitch()
    {
        var command = DeployCommandBuilder.BuildWindowsUninstallCommand("http://host:5000/");

        Assert.IsTrue(command.Contains("-Uninstall"), command);
        Assert.IsFalse(command.Contains("-OrganizationId"), command);
    }

    [TestMethod]
    [Description("The override command uses the alias and group parameters the installer supports.")]
    public void BuildWindowsOverrideCommand_UsesAliasAndGroup()
    {
        var command = DeployCommandBuilder.BuildWindowsOverrideCommand(
            "http://host:5000/",
            "org-id",
            "Front Desk",
            "Customer Group 1");

        Assert.IsTrue(command.Contains("-DeviceAlias 'Front Desk'"), command);
        Assert.IsTrue(command.Contains("-DeviceGroup 'Customer Group 1'"), command);
    }

    [TestMethod]
    [Description("The Linux command downloads the static installer and fills in the HostName and Organization placeholders that the bash installer leaves empty.")]
    public void BuildLinuxCommand_FillsInPlaceholders()
    {
        var command = DeployCommandBuilder.BuildLinuxCommand(
            "http://host:5000/",
            "org-id",
            "Install-Ubuntu-x64.sh");

        Assert.IsTrue(
            command.Contains("http://host:5000/Content/Install-Ubuntu-x64.sh"),
            command);
        Assert.IsTrue(command.Contains("HostName=\"http://host:5000\""), command);
        Assert.IsTrue(command.Contains("Organization=\"org-id\""), command);
        Assert.IsFalse(command.Contains("/api/ClientDownloads"), command);
    }

    [TestMethod]
    [Description("The local Linux install command passes the downloaded agent zip to the installer.")]
    public void BuildLinuxLocalInstallCommand_PassesTheZipPath()
    {
        var command = DeployCommandBuilder.BuildLinuxLocalInstallCommand(
            "http://host:5000/",
            "org-id",
            "Install-Ubuntu-x64.sh",
            "/tmp/Remotely-Linux.zip");

        Assert.IsTrue(command.Contains("--path '/tmp/Remotely-Linux.zip'"), command);
    }

    [TestMethod]
    [Description("The Linux uninstall command uses the --uninstall argument that the bash installer supports.")]
    public void BuildLinuxUninstallCommand_UsesUninstallArgument()
    {
        var command = DeployCommandBuilder.BuildLinuxUninstallCommand(
            "http://host:5000/",
            "org-id",
            "Install-Ubuntu-x64.sh");

        Assert.IsTrue(command.EndsWith("/tmp/Install-Remotely.sh --uninstall"), command);
    }
}
