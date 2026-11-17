using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.Tests;

/// <summary>
/// The session cache is where the agent hub records which connection owns a
/// device.  Registration replaces that ownership, and deregistration may only
/// remove it when the connection asking is still the owner.  The hub relies on
/// that distinction to avoid marking a live device offline when an old,
/// replaced connection drops.
/// </summary>
[TestClass]
public class AgentHubSessionCacheTests
{
    [TestMethod]
    [Description("Registering a connection publishes the device as online under that connection.")]
    public void AddOrUpdateByConnectionId_PublishesTheDeviceAsOnline()
    {
        var cache = new AgentHubSessionCache();
        var device = NewDevice();

        cache.AddOrUpdateByConnectionId("connection-1", device);

        Assert.IsTrue(cache.TryGetConnectionId(device.ID, out var connectionId));
        Assert.AreEqual("connection-1", connectionId);
        Assert.IsTrue(cache.TryGetByDeviceId(device.ID, out var onlineDevice));
        Assert.AreEqual(device.ID, onlineDevice.ID);
    }

    [TestMethod]
    [Description("A newer connection for the same device replaces the previous owner.")]
    public void AddOrUpdateByConnectionId_NewConnectionReplacesTheOwner()
    {
        var cache = new AgentHubSessionCache();

        cache.AddOrUpdateByConnectionId("connection-1", NewDevice());
        cache.AddOrUpdateByConnectionId("connection-2", NewDevice());

        Assert.IsTrue(cache.TryGetConnectionId("device-1", out var connectionId));
        Assert.AreEqual("connection-2", connectionId);
    }

    [TestMethod]
    [Description(
        "A replaced connection's deregistration is refused and must not remove " +
        "the newer owner's mapping, so the live device stays online.")]
    public void TryRemoveByConnectionId_ReplacedConnection_KeepsTheNewOwner()
    {
        var cache = new AgentHubSessionCache();

        cache.AddOrUpdateByConnectionId("connection-1", NewDevice());
        cache.AddOrUpdateByConnectionId("connection-2", NewDevice());

        Assert.IsFalse(
            cache.TryRemoveByConnectionId("connection-1", out var removed),
            "The replaced connection must not report itself as the device owner.");

        Assert.IsNull(removed);
        Assert.IsTrue(cache.TryGetConnectionId("device-1", out var connectionId));
        Assert.AreEqual("connection-2", connectionId);
    }

    [TestMethod]
    [Description("The owner's deregistration removes the mapping so the device is no longer online.")]
    public void TryRemoveByConnectionId_CurrentConnection_RemovesTheMapping()
    {
        var cache = new AgentHubSessionCache();
        var device = NewDevice();

        cache.AddOrUpdateByConnectionId("connection-1", device);

        Assert.IsTrue(cache.TryRemoveByConnectionId("connection-1", out var removed));
        Assert.AreEqual(device.ID, removed.ID);
        Assert.IsFalse(cache.TryGetConnectionId(device.ID, out _));
        Assert.IsFalse(cache.TryGetByDeviceId(device.ID, out _));
    }

    [TestMethod]
    [Description("An unknown connection cannot deregister a device it never owned.")]
    public void TryRemoveByConnectionId_UnknownConnection_IsRefused()
    {
        var cache = new AgentHubSessionCache();

        cache.AddOrUpdateByConnectionId("connection-1", NewDevice());

        Assert.IsFalse(cache.TryRemoveByConnectionId("connection-unknown", out var removed));
        Assert.IsNull(removed);
        Assert.IsTrue(cache.TryGetConnectionId("device-1", out var connectionId));
        Assert.AreEqual("connection-1", connectionId);
    }

    private static Device NewDevice()
    {
        return new Device
        {
            ID = "device-1",
            DeviceName = "Test Device"
        };
    }
}
