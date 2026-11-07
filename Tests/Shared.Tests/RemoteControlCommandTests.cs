using System.CommandLine;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Desktop.Shared.Startup;

namespace Remotely.Shared.Tests;

/// <summary>
/// The desktop client is started by the agent with a command line.  A
/// session that was started for a capture has no viewers, so the list of
/// viewers can be empty.  An option without a value used to stop the
/// desktop client from starting at all, which broke the capture when the
/// agent reconnected to the server.
/// </summary>
[TestClass]
public class RemoteControlCommandTests
{
    private static Command CreateCommand()
    {
        return CommandProvider.CreateRemoteControlCommand(true, "The remote control client.");
    }

    private static Option ViewersOption(Command command)
    {
        return command.Options.First(x => x.Name == "viewers");
    }

    [TestMethod]
    public void ViewersOption_WithoutAValue_IsAccepted()
    {
        var result = CreateCommand().Parse(
            new[] { "--mode", "Unattended", "--host", "http://localhost:5000", "--viewers" });

        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void ViewersOption_WithAValue_IsAccepted()
    {
        var result = CreateCommand().Parse(
            new[] { "--mode", "Unattended", "--host", "http://localhost:5000", "--viewers", "abc,def" });

        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void ViewersOption_IsLeftOutWithoutAnError()
    {
        var result = CreateCommand().Parse(
            new[] { "--mode", "Unattended", "--host", "http://localhost:5000" });

        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void ViewersOption_AllowsTheValueToBeLeftOut()
    {
        var option = ViewersOption(CreateCommand());

        Assert.AreEqual(ArgumentArity.ZeroOrOne, option.Arity);
    }
}
