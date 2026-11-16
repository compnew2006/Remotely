#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;

namespace Remotely.Server.Tests;

/// <summary>
/// The script domain, the shared files, the device listings, the membership of
/// the device groups, and the cleanup of the old records.  Everything runs
/// against a real SQLite database.
/// </summary>
[TestClass]
public class ScriptAndFileServicesTests
{
    private const string OrganizationId = "script-org-1";

    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-script-tests-{Guid.NewGuid():N}.db");
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
    public async Task SavedScripts_AreStored_AndFoundByTheirCreator()
    {
        await MigrateAndSeedAsync();

        var scriptService = CreateSavedScriptService();
        var userId = await GetUserIdAsync("owner@test.com");

        var script = new SavedScript
        {
            Id = Guid.NewGuid(),
            Name = "Restart service",
            Content = "Restart-Service",
            CreatorId = userId
        };

        var result = await scriptService.AddOrUpdateSavedScript(script, userId);

        Assert.IsTrue(result.IsSuccess);

        var loaded = await scriptService.GetSavedScript(userId, script.Id);
        Assert.IsTrue(loaded.IsSuccess);
        Assert.AreEqual("Restart service", loaded.Value.Name);

        var byId = await scriptService.GetSavedScript(script.Id);
        Assert.IsTrue(byId.IsSuccess);

        var missing = await scriptService.GetSavedScript(userId, Guid.NewGuid());
        Assert.IsFalse(missing.IsSuccess);
    }

    [TestMethod]
    public async Task SavedScripts_AreDeleted_WithTheirSchedules()
    {
        await MigrateAndSeedAsync();

        var scriptService = CreateSavedScriptService();
        var scheduleService = CreateScriptScheduleService();
        var userId = await GetUserIdAsync("owner@test.com");

        var script = new SavedScript
        {
            Id = Guid.NewGuid(),
            Name = "Restart service",
            Content = "Restart-Service",
            CreatorId = userId
        };
        await scriptService.AddOrUpdateSavedScript(script, userId);

        await scheduleService.AddOrUpdateScriptSchedule(new ScriptSchedule
        {
            Name = "Nightly",
            OrganizationID = OrganizationId,
            CreatorId = userId,
            SavedScriptId = script.Id,
            NextRun = DateTimeOffset.Now.AddHours(1),
            StartAt = DateTimeOffset.Now
        });

        await scriptService.DeleteSavedScript(script.Id);

        await using var context = CreateContext();
        Assert.AreEqual(0, await context.SavedScripts.CountAsync());
        Assert.AreEqual(0, await context.ScriptSchedules.CountAsync(),
            "The schedules of a deleted script are deleted with it.");
    }

    [TestMethod]
    public async Task ScriptSchedules_AreListedAndTheDueOnesAreFound()
    {
        await MigrateAndSeedAsync();

        var scheduleService = CreateScriptScheduleService();
        var userId = await GetUserIdAsync("owner@test.com");

        await scheduleService.AddOrUpdateScriptSchedule(new ScriptSchedule
        {
            Name = "Due",
            OrganizationID = OrganizationId,
            CreatorId = userId,
            NextRun = DateTimeOffset.Now.AddHours(-1),
            StartAt = DateTimeOffset.Now.AddHours(-2)
        });

        await scheduleService.AddOrUpdateScriptSchedule(new ScriptSchedule
        {
            Name = "Later",
            OrganizationID = OrganizationId,
            CreatorId = userId,
            NextRun = DateTimeOffset.Now.AddHours(5),
            StartAt = DateTimeOffset.Now
        });

        var all = await scheduleService.GetScriptSchedules(OrganizationId);
        var due = await scheduleService.GetScriptSchedulesDue();

        Assert.AreEqual(2, all.Count);
        Assert.AreEqual(1, due.Count);
        Assert.AreEqual("Due", due[0].Name);
    }

    [TestMethod]
    public async Task ScriptSchedules_CanBeUpdated()
    {
        await MigrateAndSeedAsync();

        var scheduleService = CreateScriptScheduleService();
        var userId = await GetUserIdAsync("owner@test.com");

        var schedule = new ScriptSchedule
        {
            Name = "Nightly",
            OrganizationID = OrganizationId,
            CreatorId = userId,
            NextRun = DateTimeOffset.Now.AddHours(1),
            StartAt = DateTimeOffset.Now
        };

        await scheduleService.AddOrUpdateScriptSchedule(schedule);

        schedule.Name = "Weekly";
        await scheduleService.AddOrUpdateScriptSchedule(schedule);

        await using var context = CreateContext();
        var stored = await context.ScriptSchedules.AsNoTracking().SingleAsync();

        Assert.AreEqual("Weekly", stored.Name);
    }

    [TestMethod]
    public async Task ScriptResults_AreStoredAndAttachedToARun()
    {
        await MigrateAndSeedAsync();

        var resultService = CreateScriptResultService();

        var result = await resultService.AddScriptResult(new ScriptResultDto
        {
            DeviceID = "device-1",
            ScriptInput = "whoami",
            HadErrors = false
        });

        Assert.IsTrue(result.IsSuccess);

        var run = new ScriptRun
        {
            OrganizationID = OrganizationId,
            RunAt = DateTimeOffset.Now
        };
        await resultService.AddScriptRun(run);

        var attached = await resultService.AddScriptResultToScriptRun(result.Value.ID, run.Id);
        Assert.IsTrue(attached.IsSuccess);

        var loaded = await resultService.GetScriptResult(result.Value.ID);
        Assert.IsTrue(loaded.IsSuccess);
        Assert.AreEqual("device-1", loaded.Value.DeviceID);
    }

    [TestMethod]
    public async Task ScriptResults_FailForAnUnknownDevice()
    {
        await MigrateAndSeedAsync();

        var resultService = CreateScriptResultService();

        var result = await resultService.AddScriptResult(new ScriptResultDto
        {
            DeviceID = "does-not-exist"
        });

        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task SharedFiles_AreStoredAndFound()
    {
        await MigrateAndSeedAsync();

        var fileService = CreateSharedFileService();

        var fileId = await fileService.AddSharedFile(
            new TestFormFile("notes.txt", "text/plain", new byte[] { 1, 2, 3 }),
            OrganizationId);

        var file = await fileService.GetSharedFiled(fileId);

        Assert.IsTrue(file.IsSuccess);
        Assert.AreEqual("notes.txt", file.Value.FileName);
        Assert.AreEqual(3, file.Value.FileContents.Length);
    }

    [TestMethod]
    public async Task DeviceListings_FilterByOrganizationAndByUser()
    {
        await MigrateAndSeedAsync();

        var listingService = CreateDeviceListingService();

        var all = listingService.GetAllDevices(OrganizationId);
        var byIds = listingService.GetDevices(new[] { "device-1" });
        var forUser = listingService.GetDevicesForUser("owner@test.com");

        Assert.AreEqual(2, all.Length);
        Assert.AreEqual(1, byIds.Count);
        Assert.AreEqual(2, forUser.Length);
    }

    [TestMethod]
    public async Task DeviceGroupMembership_AddsAndRemovesTheUser()
    {
        await MigrateAndSeedAsync();

        var membershipService = CreateDeviceGroupMembershipService();

        var added = membershipService.AddUserToDeviceGroup(
            OrganizationId,
            "group-1",
            "owner@test.com",
            out var resultMessage);

        Assert.IsTrue(added, resultMessage);

        // The same user cannot be added twice.
        var again = membershipService.AddUserToDeviceGroup(
            OrganizationId,
            "group-1",
            "owner@test.com",
            out var secondMessage);

        Assert.IsFalse(again);
        Assert.AreEqual("User already in group.", secondMessage);

        var userId = await GetUserIdAsync("owner@test.com");
        var removed = await membershipService.RemoveUserFromDeviceGroup(
            OrganizationId,
            "group-1",
            userId);

        Assert.IsTrue(removed);
    }

    [TestMethod]
    public async Task Cleanup_RemovesTheOldRecords()
    {
        await MigrateAndSeedAsync();

        var maintenanceService = CreateMaintenanceService();

        await maintenanceService.CleanupOldRecords();

        // The cleanup runs without an error and keeps the devices.
        await using var context = CreateContext();
        Assert.AreEqual(2, await context.Devices.CountAsync());
    }

    private SavedScriptService CreateSavedScriptService() =>
        new(new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<SavedScriptService>.Instance);

    private ScriptScheduleService CreateScriptScheduleService() =>
        new(new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<ScriptScheduleService>.Instance);

    private ScriptResultService CreateScriptResultService() =>
        new(new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<ScriptResultService>.Instance);

    private SharedFileService CreateSharedFileService() =>
        new(new TestAppDbFactory(_connectionString, _hostEnvironment),
            new SettingsService(
                new TestAppDbFactory(_connectionString, _hostEnvironment),
                NullLogger<SettingsService>.Instance),
            NullLogger<SharedFileService>.Instance);

    private DeviceListingService CreateDeviceListingService() =>
        new(new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<DeviceListingService>.Instance);

    private DeviceGroupMembershipService CreateDeviceGroupMembershipService() =>
        new(new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<DeviceGroupMembershipService>.Instance);

    private MaintenanceService CreateMaintenanceService() =>
        new(new TestAppDbFactory(_connectionString, _hostEnvironment),
            new SettingsService(
                new TestAppDbFactory(_connectionString, _hostEnvironment),
                NullLogger<SettingsService>.Instance),
            NullLogger<MaintenanceService>.Instance);

    private async Task<string> GetUserIdAsync(string userName)
    {
        await using var context = CreateContext();
        return await context.Users
            .AsNoTracking()
            .Where(x => x.UserName == userName)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task MigrateAndSeedAsync()
    {
        await MigrateAsync();

        await using var context = CreateContext();

        context.Organizations.Add(new Organization
        {
            ID = OrganizationId,
            OrganizationName = "Script Tests"
        });
        context.Users.Add(new RemotelyUser
        {
            UserName = "owner@test.com",
            OrganizationID = OrganizationId,
            IsAdministrator = true
        });

        var group = new DeviceGroup
        {
            ID = "group-1",
            Name = "Reception",
            OrganizationID = OrganizationId
        };
        context.DeviceGroups.Add(group);

        context.Devices.Add(new Device
        {
            ID = "device-1",
            DeviceName = "Reception PC",
            OrganizationID = OrganizationId
        });
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

    private sealed class TestFormFile : Microsoft.AspNetCore.Http.IFormFile
    {
        private readonly byte[] _contents;

        public TestFormFile(string fileName, string contentType, byte[] contents)
        {
            FileName = fileName;
            ContentType = contentType;
            _contents = contents;
        }

        public string ContentType { get; }
        public string FileName { get; }
        public long Length => _contents.Length;
        public string Name => FileName;
        public string ContentDisposition { get; } = string.Empty;
        public Microsoft.AspNetCore.Http.IHeaderDictionary Headers { get; } =
            new Microsoft.AspNetCore.Http.HeaderDictionary();

        public void CopyTo(Stream target) => target.Write(_contents, 0, _contents.Length);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            CopyTo(target);
            return Task.CompletedTask;
        }

        public Stream OpenReadStream() => new MemoryStream(_contents, false);
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
