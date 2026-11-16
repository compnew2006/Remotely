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
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.Tests;

/// <summary>
/// The alert domain, exercised against a real SQLite database.  The rule
/// that matters is who receives an alert: only the users that may see the
/// device.
/// </summary>
[TestClass]
public class AlertServiceTests
{
    private const string OrganizationId = "alert-org-1";

    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-alert-tests-{Guid.NewGuid():N}.db");
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
    public async Task AddAlert_ReachesEveryUserOfTheOrganization()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert(
            "device-2",
            OrganizationId,
            "Something happened",
            "details");

        await using var context = CreateContext();

        var alerts = await context.Alerts.AsNoTracking().ToListAsync();

        Assert.AreEqual(3, alerts.Count);
        Assert.AreEqual("Something happened", alerts[0].Message);
        Assert.AreEqual("details", alerts[0].Details);
    }

    [TestMethod]
    public async Task AddAlert_ReachesOnlyTheUsersThatMaySeeTheDevice()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert("device-1", OrganizationId, "Device alert");

        await using var context = CreateContext();

        var recipients = await context.Alerts
            .AsNoTracking()
            .Select(x => x.UserID)
            .ToListAsync();

        // The administrator and the member of the device group, but not the
        // user that is only in the organization.
        Assert.AreEqual(2, recipients.Count);
        Assert.IsFalse(recipients.Contains(await GetUserIdAsync("outsider@test.com")));
    }

    [TestMethod]
    public async Task AddAlert_RefusesAnUnknownDevice_WithoutRaising()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        // The alerts table has a foreign key to the devices, so an alert for a
        // device that does not exist cannot be stored.
        await alertService.AddAlert("does-not-exist", OrganizationId, "Device alert");

        await using var context = CreateContext();
        Assert.AreEqual(0, await context.Alerts.CountAsync());
    }

    [TestMethod]
    public async Task AddAlert_RefusesAnEmptyDevice_WithoutRaising()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert(string.Empty, OrganizationId, "Organization alert");

        await using var context = CreateContext();
        Assert.AreEqual(0, await context.Alerts.CountAsync());
    }

    [TestMethod]
    public async Task GetAlerts_ReturnsTheAlertsOfTheUser()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert("device-2", OrganizationId, "First");
        await alertService.AddAlert("device-2", OrganizationId, "Second");

        var userId = await GetUserIdAsync("member@test.com");

        var alerts = alertService.GetAlerts(userId);

        Assert.AreEqual(2, alerts.Length);
        Assert.AreEqual("Second", alerts[0].Message, "The newest alert comes first.");
    }

    [TestMethod]
    public async Task DeleteAllAlerts_RemovesTheAlertsOfTheOrganization()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert("device-2", OrganizationId, "First");

        await alertService.DeleteAllAlerts(OrganizationId);

        await using var context = CreateContext();
        Assert.AreEqual(0, await context.Alerts.CountAsync());
    }

    [TestMethod]
    public async Task DeleteAllAlertsForUser_RemovesOnlyTheAlertsOfThatUser()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        // A device without a group is visible to every user of the
        // organization, so every one of them gets an alert.
        await alertService.AddAlert("device-2", OrganizationId, "First");

        await using (var context = CreateContext())
        {
            Assert.AreEqual(3, await context.Alerts.CountAsync());
        }

        await alertService.DeleteAllAlertsForUser(OrganizationId, "admin@test.com");

        await using var contextAfterDelete = CreateContext();

        var remaining = await contextAfterDelete.Alerts.AsNoTracking().ToListAsync();
        var adminId = await GetUserIdAsync("admin@test.com");

        Assert.AreEqual(2, remaining.Count, "Only the alerts of the admin are removed.");
        Assert.IsFalse(
            remaining.Any(x => x.UserID == adminId),
            "The admin has no alerts left.");
    }

    [TestMethod]
    public async Task DeleteAllAlertsForUser_IgnoresTheCaseAndTheSurroundingWhitespace()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert("device-2", OrganizationId, "First");

        // The name is stored in lower case.  A name that differs in case or
        // carries whitespace still has to resolve to the same user, and it
        // must not depend on the collation of the database.
        await alertService.DeleteAllAlertsForUser(OrganizationId, "  ADMIN@TEST.COM  ");

        await using var context = CreateContext();

        var remaining = await context.Alerts.AsNoTracking().ToListAsync();
        var adminId = await GetUserIdAsync("admin@test.com");

        Assert.AreEqual(2, remaining.Count);
        Assert.IsFalse(
            remaining.Any(x => x.UserID == adminId),
            "The admin has no alerts left.");
    }

    [TestMethod]
    public async Task DeleteAllAlertsForUser_RemovesNothingWhenTheUserDoesNotExist()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert("device-2", OrganizationId, "First");

        // A name that does not resolve to a user must not fall back to the
        // organization: that would remove the alerts of every user.
        await alertService.DeleteAllAlertsForUser(OrganizationId, "nobody@test.com");

        await using var context = CreateContext();
        Assert.AreEqual(3, await context.Alerts.CountAsync());
    }

    [TestMethod]
    public async Task DeleteAllAlertsForUser_RemovesNothingWhenTheNameIsEmpty()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        await alertService.AddAlert("device-2", OrganizationId, "First");

        await alertService.DeleteAllAlertsForUser(OrganizationId, string.Empty);

        await using var context = CreateContext();
        Assert.AreEqual(3, await context.Alerts.CountAsync());
    }

    [TestMethod]
    public async Task GetAlert_FailsForAnUnknownAlert()
    {
        await MigrateAndSeedAsync();

        var alertService = CreateAlertService();

        var result = await alertService.GetAlert("does-not-exist");

        Assert.IsFalse(result.IsSuccess);
    }

    private async Task<string> GetUserIdAsync(string userName)
    {
        await using var context = CreateContext();
        return await context.Users
            .AsNoTracking()
            .Where(x => x.UserName == userName)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private AlertService CreateAlertService()
    {
        return new AlertService(
            new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<AlertService>.Instance);
    }

    private async Task MigrateAndSeedAsync()
    {
        await MigrateAsync();

        await using var context = CreateContext();

        var organization = new Organization
        {
            ID = OrganizationId,
            OrganizationName = "Alert Tests"
        };
        context.Organizations.Add(organization);

        var admin = new RemotelyUser
        {
            UserName = "admin@test.com",
            OrganizationID = OrganizationId,
            IsAdministrator = true
        };
        var member = new RemotelyUser
        {
            UserName = "member@test.com",
            OrganizationID = OrganizationId
        };
        var outsider = new RemotelyUser
        {
            UserName = "outsider@test.com",
            OrganizationID = OrganizationId
        };
        context.Users.AddRange(admin, member, outsider);

        var group = new DeviceGroup
        {
            ID = "group-1",
            Name = "Reception",
            OrganizationID = OrganizationId
        };
        group.Users.Add(member);
        context.DeviceGroups.Add(group);

        context.Devices.Add(new Device
        {
            ID = "device-1",
            DeviceName = "Reception PC",
            OrganizationID = OrganizationId,
            DeviceGroupID = group.ID
        });

        // A device without a group: every user of the organization may see it.
        context.Devices.Add(new Device
        {
            ID = "device-2",
            DeviceName = "Server room PC",
            OrganizationID = OrganizationId
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
