using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;

namespace Remotely.Server.Tests;

/// <summary>
/// The API script result handoff replaced a static process-wide cache.  The
/// contract that matters to <c>ScriptingController</c> is take-once: the agent's
/// command id is delivered to the first caller that consumes it, and a second
/// poll must not replay it.
/// </summary>
[TestClass]
public class ApiScriptResultCacheTests
{
    [TestMethod]
    public void Set_IsVisibleUntilTaken()
    {
        using var cache = new ApiScriptResultCache();

        cache.Set("request-1", "command-1");

        Assert.IsTrue(cache.Contains("request-1"));
        Assert.IsTrue(cache.TryTake("request-1", out var commandId));
        Assert.AreEqual("command-1", commandId);
        Assert.IsFalse(cache.Contains("request-1"), "A taken result must not be returned again.");
    }

    [TestMethod]
    public void TryTake_ReturnsFalseForUnknownRequest()
    {
        using var cache = new ApiScriptResultCache();

        Assert.IsFalse(cache.TryTake("missing", out var commandId));
        Assert.IsNull(commandId);
    }

    [TestMethod]
    public void Remove_DropsThePendingResult()
    {
        using var cache = new ApiScriptResultCache();
        cache.Set("request-1", "command-1");

        cache.Remove("request-1");

        Assert.IsFalse(cache.Contains("request-1"));
        Assert.IsFalse(cache.TryTake("request-1", out _));
    }
}
