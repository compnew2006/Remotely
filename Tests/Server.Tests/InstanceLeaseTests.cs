#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
using Remotely.Shared.Services;

namespace Remotely.Server.Tests;

/// <summary>
/// The instance lease tells the operator that another server instance is
/// using the same database.  It is stored in the KeyValueRecords table, so
/// the tests use a real SQLite database with the migrations applied.
/// </summary>
[TestClass]
public class InstanceLeaseTests
{
    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-lease-tests-{Guid.NewGuid():N}.db");
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
    public async Task TheFirstInstance_TakesTheLease_WithoutAConflict()
    {
        await MigrateAsync();

        var lease = CreateLease();

        var holder = await lease.AcquireAsync();

        Assert.IsNull(holder);
        Assert.IsFalse(lease.HasConflict);

        await using var context = CreateContext();
        var record = await context.KeyValueRecords
            .AsNoTracking()
            .SingleAsync(x => x.Key == InstanceLease.LeaseKey);

        Assert.IsFalse(string.IsNullOrWhiteSpace(record.Value));
    }

    [TestMethod]
    public async Task ASecondInstance_ReportsAConflict()
    {
        await MigrateAsync();

        var first = CreateLease();
        var second = CreateLease();

        await first.AcquireAsync();

        var holder = await second.AcquireAsync();

        Assert.IsNotNull(holder);
        Assert.IsTrue(second.HasConflict);
    }

    [TestMethod]
    public async Task AnExpiredLease_IsTakenOver_WithoutAConflict()
    {
        await MigrateAsync();

        var systemTime = new SystemTime();
        var first = CreateLease(systemTime);
        var second = CreateLease(systemTime);

        await first.AcquireAsync();

        // The first instance stopped renewing the lease.
        systemTime.Offset(InstanceLeaseService.LeaseDuration + TimeSpan.FromSeconds(5));

        var holder = await second.AcquireAsync();

        Assert.IsNull(holder);
        Assert.IsFalse(second.HasConflict);
    }

    [TestMethod]
    public async Task ADetectedInstance_DoesNotStealTheLease()
    {
        await MigrateAsync();

        var first = CreateLease();
        var second = CreateLease();

        await first.AcquireAsync();
        await second.AcquireAsync();

        // The owner of a fresh lease keeps it, and the second instance only
        // reports the conflict.
        var renewed = await first.RenewAsync();

        Assert.IsTrue(renewed);
        Assert.IsFalse(first.HasConflict);
        Assert.IsTrue(second.HasConflict);
    }

    [TestMethod]
    public async Task Renewing_Fails_AfterAnotherInstanceTookOverTheExpiredLease()
    {
        await MigrateAsync();

        var systemTime = new SystemTime();
        var first = CreateLease(systemTime);
        var second = CreateLease(systemTime);

        await first.AcquireAsync();

        // The first instance stopped renewing the lease, so the second one
        // takes it over.
        systemTime.Offset(InstanceLeaseService.LeaseDuration + TimeSpan.FromSeconds(5));

        await second.AcquireAsync();

        var renewed = await first.RenewAsync();

        Assert.IsFalse(renewed);
        Assert.IsTrue(first.HasConflict);
    }

    [TestMethod]
    public async Task Renewing_Succeeds_WhileTheLeaseIsOwned()
    {
        await MigrateAsync();

        var lease = CreateLease();

        await lease.AcquireAsync();
        var renewed = await lease.RenewAsync();

        Assert.IsTrue(renewed);
        Assert.IsFalse(lease.HasConflict);
    }

    private InstanceLeaseService CreateLease(ISystemTime? systemTime = null)
    {
        var factory = new TestAppDbFactory(_connectionString, _hostEnvironment);

        return new InstanceLeaseService(
            factory,
            systemTime ?? new SystemTime(),
            NullLogger<InstanceLeaseService>.Instance);
    }

    private async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private SqliteDbContext CreateContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SQLite"] = _connectionString
            })
            .Build();

        return new SqliteDbContext(configuration, _hostEnvironment);
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
