#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Data;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;

namespace Remotely.Server.Tests;

/// <summary>
/// The rest of the suite runs on the EF in-memory provider, which does not
/// enforce relational constraints and cannot validate migrations.  These tests
/// use a real SQLite database file so the schema, the migration chain, and the
/// queries that production actually issues are exercised together.
/// </summary>
[TestClass]
public class SqliteIntegrationTests
{
    private const string OrganizationId = "sqlite-org-1";

    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-sqlite-tests-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_databasePath}";
        _hostEnvironment = new TestHostEnvironment();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Pooled connections keep the file open, so the file cannot be deleted
        // until the pool is cleared.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [TestMethod]
    public async Task Migrations_ApplyCleanly_ToAnEmptyDatabase()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        Assert.AreEqual(0, (await context.Database.GetPendingMigrationsAsync()).Count(),
            "Every migration should have been applied.");
        Assert.IsTrue((await context.Database.GetAppliedMigrationsAsync()).Any(),
            "The SQLite migration chain must not be empty.");

        // The identity and application tables must exist and be queryable.
        Assert.AreEqual(0, await context.Organizations.CountAsync());
        Assert.AreEqual(0, await context.Devices.CountAsync());
    }

    [TestMethod]
    public async Task Migrations_AreIdempotent()
    {
        await using (var first = CreateContext())
        {
            await first.Database.MigrateAsync();
        }

        await using var second = CreateContext();
        await second.Database.MigrateAsync();

        Assert.AreEqual(0, (await second.Database.GetPendingMigrationsAsync()).Count());
    }

    [TestMethod]
    public async Task SettingsService_RoundTripsTheSingleSettingsRow()
    {
        await MigrateAsync();

        var factory = new TestAppDbFactory(_connectionString, _hostEnvironment);
        var settingsService = new SettingsService(factory, NullLogger<SettingsService>.Instance);

        var settings = new SettingsModel
        {
            MaxOrganizationCount = 7,
            EnableRemoteControlRecording = true
        };

        await settingsService.SaveSettings(settings);
        await settingsService.SaveSettings(settings);

        var loaded = await settingsService.GetSettings();

        Assert.AreEqual(7, loaded.MaxOrganizationCount);
        Assert.IsTrue(loaded.EnableRemoteControlRecording);

        await using var context = CreateContext();
        Assert.AreEqual(1, await context.KeyValueRecords.CountAsync(),
            "Saving twice must update the single settings row, not add a second one.");
    }

    [TestMethod]
    public async Task GetSettings_LazilyCreatesTheDefaultRowOnce()
    {
        await MigrateAsync();

        var factory = new TestAppDbFactory(_connectionString, _hostEnvironment);
        var settingsService = new SettingsService(factory, NullLogger<SettingsService>.Instance);

        var first = await settingsService.GetSettings();
        var second = await settingsService.GetSettings();

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);

        await using var context = CreateContext();
        Assert.AreEqual(1, await context.KeyValueRecords.CountAsync(),
            "Reading settings seeds the single defaults row and must not add another.");
    }

    [TestMethod]
    public void EachProvider_HasDiscoverableMigrations()
    {
        // MIGRATION-1: a provider whose migrations land in the wrong folder or
        // namespace silently has no chain at all, and the server would start
        // against an empty schema.  EF can enumerate migrations without a live
        // database, so this is checked for all three providers here.
        using var sqlite = CreateContext();
        using var sqlServer = new SqlServerDbContext(
            BuildConfiguration("SQLServer", "Server=localhost;Database=test;Trusted_Connection=True"),
            _hostEnvironment);
        using var postgres = new PostgreSqlDbContext(
            BuildConfiguration("PostgreSQL", "Host=localhost;Database=test"),
            _hostEnvironment);

        Assert.IsTrue(sqlite.Database.GetMigrations().Any(), "SQLite has no migrations.");
        Assert.IsTrue(sqlServer.Database.GetMigrations().Any(), "SQL Server has no migrations.");
        Assert.IsTrue(postgres.Database.GetMigrations().Any(), "PostgreSQL has no migrations.");
    }

    [TestMethod]
    public async Task AddOrUpdateDevice_RoundTripsThroughSqlite()
    {
        await MigrateAndSeedOrganizationAsync();

        var deviceService = CreateDeviceService();

        var result = await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "sqlite-device-1",
            OrganizationID = OrganizationId,
            DeviceName = "Integration Device",
            AgentVersion = "1.0.0"
        });

        Assert.IsTrue(result.IsSuccess);

        await using (var verification = CreateContext())
        {
            var stored = await verification.Devices
                .AsNoTracking()
                .SingleAsync(x => x.ID == "sqlite-device-1");

            Assert.AreEqual("Integration Device", stored.DeviceName);
            Assert.AreEqual("1.0.0", stored.AgentVersion);
            Assert.IsTrue(stored.IsOnline);
        }

        // An update must not create a duplicate row.
        var update = await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "sqlite-device-1",
            OrganizationID = OrganizationId,
            DeviceName = "Renamed Device",
            AgentVersion = "1.0.1"
        });

        Assert.IsTrue(update.IsSuccess);

        await using var finalCheck = CreateContext();
        var devices = await finalCheck.Devices
            .AsNoTracking()
            .Where(x => x.ID == "sqlite-device-1")
            .ToListAsync();

        Assert.AreEqual(1, devices.Count, "The device must be updated, not duplicated.");
        Assert.AreEqual("Renamed Device", devices[0].DeviceName);
    }

    [TestMethod]
    public async Task AddOrUpdateDevice_RejectsUnknownOrganization()
    {
        await MigrateAsync();

        var deviceService = CreateDeviceService();

        var result = await deviceService.AddOrUpdateDevice(new DeviceClientDto
        {
            ID = "orphan-device",
            OrganizationID = "missing-org",
            DeviceName = "Orphan"
        });

        Assert.IsFalse(result.IsSuccess);

        await using var context = CreateContext();
        Assert.AreEqual(0, await context.Devices.CountAsync());
    }

    [TestMethod]
    public async Task GetAllDevices_FiltersByOrganization_OnSqlite()
    {
        await MigrateAndSeedOrganizationAsync();

        await using (var seeding = CreateContext())
        {
            seeding.Organizations.Add(new Organization
            {
                ID = "sqlite-org-2",
                OrganizationName = "Other Org"
            });
            await seeding.SaveChangesAsync();
        }

        var deviceService = CreateDeviceService();
        await deviceService.AddOrUpdateDevice(Device("device-1", OrganizationId));
        await deviceService.AddOrUpdateDevice(Device("device-2", OrganizationId));
        await deviceService.AddOrUpdateDevice(Device("device-3", "sqlite-org-2"));

        var org1Devices = CreateDeviceListingService().GetAllDevices(OrganizationId);

        Assert.AreEqual(2, org1Devices.Length);
        Assert.IsTrue(org1Devices.All(x => x.OrganizationID == OrganizationId));
    }

    private static DeviceClientDto Device(string id, string organizationId)
    {
        return new DeviceClientDto
        {
            ID = id,
            OrganizationID = organizationId,
            DeviceName = id
        };
    }

    private async Task MigrateAndSeedOrganizationAsync()
    {
        await MigrateAsync();

        await using var context = CreateContext();
        context.Organizations.Add(new Organization
        {
            ID = OrganizationId,
            OrganizationName = "SQLite Tests"
        });
        await context.SaveChangesAsync();
    }

    private async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private IDeviceService CreateDeviceService()
    {
        var factory = new TestAppDbFactory(_connectionString, _hostEnvironment);
        return new DeviceService(
            _hostEnvironment,
            factory,
            NullLogger<DeviceService>.Instance);
    }

    private IDeviceListingService CreateDeviceListingService()
    {
        var factory = new TestAppDbFactory(_connectionString, _hostEnvironment);
        return new DeviceListingService(factory, NullLogger<DeviceListingService>.Instance);
    }

    private SqliteDbContext CreateContext()
    {
        return new SqliteDbContext(
            BuildConfiguration("SQLite", _connectionString),
            _hostEnvironment);
    }

    private static IConfiguration BuildConfiguration(string name, string connectionString)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{name}"] = connectionString
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
            return new SqliteDbContext(
                BuildConfiguration("SQLite", _connectionString),
                _hostEnvironment);
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
