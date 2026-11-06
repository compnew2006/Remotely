using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Primitives;

namespace Remotely.Shared.Tests;

/// <summary>
/// The desktop client runs in the interactive session of the target machine.  These tests
/// cover the policy that decides when it is started again after it was terminated, so
/// that a killed client cannot take the remote control session down with it, and a client
/// that cannot start cannot be relaunched forever.
/// </summary>
[TestClass]
public class DesktopProcessRecoveryPolicyTests
{
    [TestMethod]
    public void CleanExit_IsNeverRecovered()
    {
        var policy = new DesktopProcessRecoveryPolicy();

        Assert.IsTrue(policy.IsCleanExit(0));
        Assert.IsFalse(policy.ShouldRecover(0, DateTimeOffset.Now, Array.Empty<DateTimeOffset>()));
    }

    [TestMethod]
    public void UnexpectedExit_IsRecovered()
    {
        var policy = new DesktopProcessRecoveryPolicy();

        Assert.IsFalse(policy.IsCleanExit(1));
        Assert.IsTrue(policy.ShouldRecover(1, DateTimeOffset.Now, Array.Empty<DateTimeOffset>()));
        Assert.IsTrue(policy.ShouldRecover(-1073741510, DateTimeOffset.Now, Array.Empty<DateTimeOffset>()));
    }

    [TestMethod]
    public void AttemptsInsideTheWindow_AreCounted()
    {
        var policy = new DesktopProcessRecoveryPolicy
        {
            MaxAttempts = 3,
            AttemptWindow = TimeSpan.FromMinutes(1)
        };

        var now = DateTimeOffset.Now;
        var attempts = new List<DateTimeOffset> { now.AddSeconds(-20), now.AddSeconds(-10) };

        Assert.IsTrue(policy.ShouldRecover(1, now, attempts));

        attempts.Add(now);

        Assert.IsFalse(policy.ShouldRecover(1, now, attempts));
    }

    [TestMethod]
    public void AttemptsOutsideTheWindow_AreNotCounted()
    {
        var policy = new DesktopProcessRecoveryPolicy
        {
            MaxAttempts = 2,
            AttemptWindow = TimeSpan.FromMinutes(1)
        };

        var now = DateTimeOffset.Now;
        var attempts = new List<DateTimeOffset> { now.AddMinutes(-5), now.AddMinutes(-10) };

        Assert.IsTrue(policy.ShouldRecover(1, now, attempts));
    }

    [TestMethod]
    public void Delay_GrowsExponentiallyUpToTheMaximum()
    {
        var policy = new DesktopProcessRecoveryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(30)
        };

        Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(1));
        Assert.AreEqual(TimeSpan.FromSeconds(4), policy.GetDelay(2));
        Assert.AreEqual(TimeSpan.FromSeconds(8), policy.GetDelay(3));
        Assert.AreEqual(TimeSpan.FromSeconds(30), policy.GetDelay(10));
    }

    [TestMethod]
    public void Delay_IsNeverLessThanTheInitialDelay()
    {
        var policy = new DesktopProcessRecoveryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(30)
        };

        Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(0));
        Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(-5));
    }
}
