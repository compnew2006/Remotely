using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Enums;
using Remotely.Server.Models;

namespace Remotely.Server.Tests;

/// <summary>
/// <see cref="ConnectionIdSet"/> replaced a bare <c>HashSet&lt;string&gt;</c> that
/// three hubs mutated from different SignalR connections.  These tests cover
/// the behaviour the callers depend on: set semantics, snapshot enumeration,
/// and surviving concurrent mutation without throwing or losing ids.
/// </summary>
[TestClass]
public class ConnectionIdSetTests
{
    [TestMethod]
    public void Add_IsASet()
    {
        var set = new ConnectionIdSet();

        Assert.IsTrue(set.Add("viewer-1"));
        Assert.IsFalse(set.Add("viewer-1"), "Adding the same id twice must not duplicate it.");
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Contains("viewer-1"));
    }

    [TestMethod]
    public void Remove_ReportsWhetherTheIdWasPresent()
    {
        var set = new ConnectionIdSet();
        set.Add("viewer-1");

        Assert.IsTrue(set.Remove("viewer-1"));
        Assert.IsFalse(set.Remove("viewer-1"));
        Assert.IsFalse(set.Contains("viewer-1"));
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void Snapshot_DoesNotFollowLaterMutations()
    {
        var set = new ConnectionIdSet();
        set.Add("viewer-1");

        var snapshot = set.Snapshot();
        set.Add("viewer-2");
        set.Remove("viewer-1");

        CollectionAssert.AreEqual(new[] { "viewer-1" }, snapshot);
    }

    [TestMethod]
    public void Enumeration_IsASnapshot()
    {
        var set = new ConnectionIdSet();
        set.Add("viewer-1");
        set.Add("viewer-2");

        var seen = new List<string>();
        foreach (var id in set)
        {
            // A hub removing a viewer while another hub broadcasts is the exact
            // race this type exists to make safe.
            set.Remove("viewer-2");
            set.Add("viewer-3");
            seen.Add(id);
        }

        CollectionAssert.AreEquivalent(new[] { "viewer-1", "viewer-2" }, seen);
        Assert.AreEqual(2, set.Count);
    }

    [TestMethod]
    public void AClonedSession_DoesNotShareTheViewerSetWithItsOriginal()
    {
        // CreateNew() starts from a MemberwiseClone, which copies the reference
        // to the viewer set.  The clone has to get its own, or a viewer that
        // attaches to the new session would show up in the old one.
        var original = new RemoteControlSession
        {
            Mode = RemoteControlMode.Unattended
        };
        original.ViewerList.Add("viewer-1");

        var clone = original.CreateNew();

        clone.ViewerList.Add("viewer-2");

        CollectionAssert.AreEqual(
            new[] { "viewer-1" },
            original.ViewerList.ToArray(),
            "The original session must not see the clone's viewer.");
        CollectionAssert.AreEqual(
            new[] { "viewer-2" },
            clone.ViewerList.ToArray(),
            "The clone must start with an empty viewer set.");
    }

    [TestMethod]
    public async Task ConcurrentMutationAndEnumeration_DoesNotThrowOrLoseIds()
    {
        const int writers = 8;
        const int idsPerWriter = 500;

        var set = new ConnectionIdSet();
        var mutations = Enumerable.Range(0, writers)
            .Select(writer => Task.Run(() =>
            {
                for (var i = 0; i < idsPerWriter; i++)
                {
                    var id = $"viewer-{writer}-{i}";
                    set.Add(id);
                    set.Remove(id);
                    set.Add(id);
                }
            }))
            .ToArray();

        var reader = Task.Run(() =>
        {
            var observed = 0;
            while (!mutations.All(x => x.IsCompleted))
            {
                // Enumerating a HashSet while it is written to can throw
                // InvalidOperationException; a snapshot cannot.
                observed += set.Count;
                _ = set.Snapshot().Length;
            }

            return observed;
        });

        await Task.WhenAll(mutations);
        await reader;

        Assert.AreEqual(writers * idsPerWriter, set.Count);
    }
}
