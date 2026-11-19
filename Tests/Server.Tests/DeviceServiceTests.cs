#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Helpers;

namespace Remotely.Server.Tests;

/// <summary>
/// The device domain, exercised against a real SQLite database so the
/// queries and the constraints are the ones the server issues.
/// </summary>
[TestClass]
public class DeviceServiceTests
{
    private const string OrganizationId = "device-org-1";

    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-device-tests-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_databasePath}";
        _hostEnvironment = new TestHostEnvironment();
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [TestMethod]
    public async Task AddOrUpdateDevice_CreatesTheDevice()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();

        var result = await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-1",
            OrganizationID = OrganizationId,
            DeviceName = "Workstation"
        });

        Assert.IsTrue(result.IsSuccess);

        await using var context = CreateContext();
        var stored = await context.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-1");

        Assert.AreEqual("Workstation", stored.DeviceName);
        Assert.AreEqual(OrganizationId, stored.OrganizationID);
        Assert.IsTrue(stored.IsOnline);
    }

    [TestMethod]
    public async Task AddOrUpdateDevice_RejectsAnUnknownOrganization()
    {
        await MigrateAsync();

        var deviceService = CreateDeviceService();

        var result = await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-2",
            OrganizationID = "missing-org"
        });

        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task GetDevice_FindsTheDevice_ByIdAndByOrganization()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-3",
            OrganizationID = OrganizationId,
            DeviceName = "Laptop"
        });

        var byId = await deviceService.GetDevice("device-3");
        Assert.IsTrue(byId.IsSuccess);
        Assert.AreEqual("Laptop", byId.Value.DeviceName);

        var byOrganization = await deviceService.GetDevice(OrganizationId, "device-3");
        Assert.IsTrue(byOrganization.IsSuccess);

        var wrongOrganization = await deviceService.GetDevice("other-org", "device-3");
        Assert.IsFalse(wrongOrganization.IsSuccess);
    }

    [TestMethod]
    public async Task UpdateDevice_ChangesTheAlias()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-4",
            OrganizationID = OrganizationId,
            DeviceName = "Desktop"
        });

        var result = await deviceService.UpdateDevice(
            new Shared.Models.DeviceSetupOptions
            {
                DeviceID = "device-4",
                OrganizationID = OrganizationId,
                DeviceAlias = "Reception"
            },
            OrganizationId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Reception", result.Value.Alias);
    }

    [TestMethod]
    public async Task UpdateScreenshotSettings_StoresTheDirectoryAndTheRetention()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-5",
            OrganizationID = OrganizationId
        });

        var directory = Path.Combine(Path.GetTempPath(), "remotely-screenshots");

        var result = await deviceService.UpdateScreenshotSettings(
            "device-5",
            directory,
            42,
            12);

        Assert.IsTrue(result.IsSuccess);

        await using var context = CreateContext();
        var stored = await context.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-5");

        Assert.AreEqual(directory, stored.ScreenshotDirectory);
        Assert.AreEqual(42, stored.ScreenshotRetentionCount);
        Assert.AreEqual(12, stored.ScreenshotRetentionHours);
    }

    [TestMethod]
    public async Task SetAllDevicesNotOnline_MarksEveryDeviceOffline()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-6",
            OrganizationID = OrganizationId
        });

        await deviceService.SetAllDevicesNotOnline();

        await using var context = CreateContext();
        Assert.AreEqual(0, await context.Devices.CountAsync(x => x.IsOnline));
    }

    [TestMethod]
    public async Task DeviceCounts_ReflectTheStoredDevices()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();

        Assert.AreEqual(0, deviceService.GetDeviceCount());
        Assert.AreEqual(0, deviceService.GetTotalDevices());

        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-7",
            OrganizationID = OrganizationId
        });

        Assert.AreEqual(1, deviceService.GetDeviceCount());
        Assert.AreEqual(1, deviceService.GetTotalDevices());
    }

    [TestMethod]
    public async Task VerifyDeviceVerificationToken_AllowsAnUnknownDevice()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();

        Assert.IsTrue(await deviceService.VerifyDeviceVerificationToken("device-new", null));
        Assert.IsTrue(await deviceService.VerifyDeviceVerificationToken("device-new", "any-token"));
    }

    [TestMethod]
    public async Task VerifyDeviceVerificationToken_RequiresTheBoundToken()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-token",
            OrganizationID = OrganizationId
        });
        deviceService.SetServerVerificationToken("device-token", "bound-token");

        Assert.IsTrue(await deviceService.VerifyDeviceVerificationToken("device-token", "bound-token"));
        Assert.IsFalse(await deviceService.VerifyDeviceVerificationToken("device-token", "wrong-token"));
        Assert.IsFalse(await deviceService.VerifyDeviceVerificationToken("device-token", ""));
        Assert.IsFalse(await deviceService.VerifyDeviceVerificationToken("device-token", null));
    }

    [TestMethod]
    public async Task VerifyDeviceVerificationToken_BindsTheTokenPresentedForAnUnboundDevice()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-unbound",
            OrganizationID = OrganizationId
        });

        // The record predates token binding; the token the agent already holds
        // becomes the identity that later registrations must present.
        Assert.IsTrue(await deviceService.VerifyDeviceVerificationToken("device-unbound", "agent-token"));

        await using var context = CreateContext();
        var stored = await context.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-unbound");

        // Only the fingerprint of the credential is kept.  The credential
        // itself never reaches the database, so a copy of it cannot be used to
        // impersonate the device.
        Assert.AreEqual(
            DeviceCredential.Fingerprint("agent-token"),
            stored.ServerVerificationToken);
        Assert.AreNotEqual("agent-token", stored.ServerVerificationToken);

        Assert.IsTrue(await deviceService.VerifyDeviceVerificationToken("device-unbound", "agent-token"));
        Assert.IsFalse(await deviceService.VerifyDeviceVerificationToken("device-unbound", "other-token"));
    }

    [TestMethod]
    public async Task VerifyDeviceVerificationToken_StoresOnlyTheFingerprintOfTheCredential()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-fingerprint",
            OrganizationID = OrganizationId
        });

        deviceService.SetServerVerificationToken("device-fingerprint", "the-secret");

        await using var context = CreateContext();
        var stored = await context.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-fingerprint");

        Assert.AreEqual(
            DeviceCredential.Fingerprint("the-secret"),
            stored.ServerVerificationToken);
        Assert.IsFalse(
            stored.ServerVerificationToken!.Contains("the-secret"),
            "The credential must not be readable in the database.");
    }

    [TestMethod]
    public async Task VerifyDeviceVerificationToken_AcceptsAndUpgradesALegacyCredentialInTheClear()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-legacy",
            OrganizationID = OrganizationId
        });

        // A record that was written before the fingerprinting holds the
        // credential itself.  It has to be accepted, and replaced.
        await using (var context = CreateContext())
        {
            var device = await context.Devices.SingleAsync(x => x.ID == "device-legacy");
            device.ServerVerificationToken = "legacy-secret";
            await context.SaveChangesAsync();
        }

        Assert.IsTrue(
            await deviceService.VerifyDeviceVerificationToken("device-legacy", "legacy-secret"),
            "A record that predates the fingerprinting must still be accepted.");
        Assert.IsFalse(
            await deviceService.VerifyDeviceVerificationToken("device-legacy", "other-secret"));

        await using var contextAfter = CreateContext();
        var stored = await contextAfter.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-legacy");

        Assert.AreEqual(
            DeviceCredential.Fingerprint("legacy-secret"),
            stored.ServerVerificationToken,
            "The credential in the clear is replaced by its fingerprint.");
    }

    [TestMethod]
    public async Task VerifyDeviceVerificationToken_AcceptsAnAgentThatPresentsNoCredentialWhenTheRecordIsLegacy()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-legacy-agent",
            OrganizationID = OrganizationId
        });

        // An agent that predates the credential header sends nothing.  Its
        // record holds the GUID the server hands back to it, so the record has
        // to stay in the clear or the agent stops verifying.
        await using (var context = CreateContext())
        {
            var device = await context.Devices.SingleAsync(x => x.ID == "device-legacy-agent");
            device.ServerVerificationToken = "1d7edc2a-0000-0000-0000-000000000000";
            await context.SaveChangesAsync();
        }

        Assert.IsTrue(
            await deviceService.VerifyDeviceVerificationToken("device-legacy-agent", null),
            "A legacy agent presents no credential and must still register.");
        Assert.IsTrue(
            await deviceService.VerifyDeviceVerificationToken("device-legacy-agent", ""),
            "A legacy agent presents no credential and must still register.");
        Assert.IsFalse(
            await deviceService.VerifyDeviceVerificationToken("device-legacy-agent", "another-token"),
            "A presented credential still has to match.");

        await using var contextAfter = CreateContext();
        var stored = await contextAfter.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-legacy-agent");

        Assert.AreEqual(
            "1d7edc2a-0000-0000-0000-000000000000",
            stored.ServerVerificationToken,
            "The legacy record keeps the value the agent compares, or the agent locks itself out.");
    }

    [TestMethod]
    public async Task SetLegacyServerVerificationToken_KeepsTheTokenInTheClear()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-legacy-rotate",
            OrganizationID = OrganizationId
        });

        deviceService.SetLegacyServerVerificationToken("device-legacy-rotate", "a-legacy-guid");

        await using var context = CreateContext();
        var stored = await context.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-legacy-rotate");

        Assert.AreEqual("a-legacy-guid", stored.ServerVerificationToken);
    }

    [TestMethod]
    public async Task GetDevicesWithoutCredentialCountAsync_CountsTheUnboundAndLegacyDevices()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-unbound-count",
            OrganizationID = OrganizationId
        });
        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-bound-count",
            OrganizationID = OrganizationId
        });
        deviceService.SetServerVerificationToken("device-bound-count", "a-secret");

        await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "device-legacy-count",
            OrganizationID = OrganizationId
        });
        await using (var context = CreateContext())
        {
            var device = await context.Devices.SingleAsync(x => x.ID == "device-legacy-count");
            device.ServerVerificationToken = "a-legacy-guid";
            await context.SaveChangesAsync();
        }

        // The legacy record is an identity in the clear, which is no identity
        // at all, so it is counted with the unbound devices.
        Assert.AreEqual(2, await deviceService.GetDevicesWithoutCredentialCountAsync());
        Assert.AreEqual(
            2,
            await deviceService.GetDevicesWithoutCredentialCountAsync(OrganizationId));
        Assert.AreEqual(
            0,
            await deviceService.GetDevicesWithoutCredentialCountAsync("another-organization"));
    }

    private DeviceService CreateDeviceService()
    {
        return new DeviceService(
            _hostEnvironment,
            new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<DeviceService>.Instance);
    }

    private async Task MigrateAndSeedOrganizationAsync()
    {
        await MigrateAsync();

        await using var context = CreateContext();
        context.Organizations.Add(new Organization
        {
            ID = OrganizationId,
            OrganizationName = "Device Tests"
        });
        await context.SaveChangesAsync();
    }

    private async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private SqliteDbContext CreateContext()
    {
        return new SqliteDbContext(
            BuildConfiguration(),
            _hostEnvironment);
    }

    private IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SQLite"] = _connectionString
            })
            .Build();
    }

    private sealed class TestAppDbFactory : IAppDbFactory
    {
        private readonly string _connectionString;
        private readonly TestHostEnvironment _hostEnvironment;

        public TestAppDbFactory(string connectionString, TestHostEnvironment hostEnvironment)
        {
            _connectionString = connectionString;
            _hostEnvironment = hostEnvironment;
        }

        public AppDb GetContext()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SQLite"] = _connectionString
                })
                .Build();

            return new SqliteDbContext(configuration, _hostEnvironment);
        }
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Remotely.Server.Tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
