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
using Remotely.Shared.ViewModels;

namespace Remotely.Server.Tests;

/// <summary>
/// The users, the invitations, and the device groups, exercised against a
/// real SQLite database.
/// </summary>
[TestClass]
public class UserAndGroupServicesTests
{
    private const string OrganizationId = "user-org-1";

    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-user-tests-{Guid.NewGuid():N}.db");
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
    public async Task CreateUser_CreatesTheUserInTheOrganization()
    {
        await MigrateAndSeedOrganizationAsync();

        var userService = CreateUserService();

        var result = await userService.CreateUser("New.User@Test.com", true, OrganizationId);

        Assert.IsTrue(result.IsSuccess);

        await using var context = CreateContext();
        var user = await context.Users.AsNoTracking().SingleAsync();

        Assert.AreEqual("new.user@test.com", user.UserName);
        Assert.AreEqual(OrganizationId, user.OrganizationID);
        Assert.IsTrue(user.IsAdministrator);
    }

    [TestMethod]
    public async Task CreateUser_FailsForAnUnknownOrganization()
    {
        await MigrateAndSeedOrganizationAsync();

        var userService = CreateUserService();

        var result = await userService.CreateUser("new.user@test.com", false, "missing-org");

        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task GetUserByName_IgnoresTheCaseAndTheSpaces()
    {
        await MigrateAndSeedAsync();

        var userService = CreateUserService();

        var result = await userService.GetUserByName("  ADMIN@TEST.COM  ");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("admin@test.com", result.Value.UserName);

        var missing = await userService.GetUserByName("missing@test.com");
        Assert.IsFalse(missing.IsSuccess);
    }

    [TestMethod]
    public async Task DoesUserHaveAccessToDevice_FollowsTheDeviceGroup()
    {
        await MigrateAndSeedAsync();

        var userService = CreateUserService();

        var admin = await userService.GetUserByName("admin@test.com");
        var member = await userService.GetUserByName("member@test.com");
        var stranger = await userService.GetUserByName("stranger@test.com");

        Assert.IsTrue(userService.DoesUserHaveAccessToDevice("device-1", admin.Value!),
            "An administrator may see every device.");
        Assert.IsTrue(userService.DoesUserHaveAccessToDevice("device-1", member.Value!),
            "A member of the device group may see its device.");
        Assert.IsFalse(userService.DoesUserHaveAccessToDevice("device-1", stranger.Value!),
            "A user outside the device group may not see the device.");
        Assert.IsFalse(userService.DoesUserHaveAccessToDevice("device-2", stranger.Value!),
            "A device without a group belongs to the administrators.");
        Assert.IsTrue(userService.DoesUserHaveAccessToDevice("device-2", admin.Value!),
            "An administrator may see a device without a group.");
    }

    [TestMethod]
    public async Task DeleteUser_RemovesTheUser()
    {
        await MigrateAndSeedAsync();

        var userService = CreateUserService();

        var user = await userService.GetUserByName("stranger@test.com");

        var result = await userService.DeleteUser(OrganizationId, user.Value!.Id);

        Assert.IsTrue(result.IsSuccess);

        await using var context = CreateContext();
        Assert.IsFalse(await context.Users.AnyAsync(x => x.UserName == "stranger@test.com"));
    }

    [TestMethod]
    public async Task AddInvite_CreatesTheInvite_AndJoinMovesTheUser()
    {
        await MigrateAndSeedAsync();

        var inviteService = CreateInviteService();

        var invite = await inviteService.AddInvite(OrganizationId, new InviteViewModel
        {
            InvitedUser = "Newcomer@Test.com",
            IsAdmin = false
        });

        Assert.IsTrue(invite.IsSuccess);
        Assert.AreEqual("newcomer@test.com", invite.Value.InvitedUser);

        await using (var context = CreateContext())
        {
            context.Users.Add(new RemotelyUser
            {
                UserName = "newcomer@test.com",
                OrganizationID = OrganizationId
            });
            await context.SaveChangesAsync();
        }

        var joined = await inviteService.JoinViaInvitation("newcomer@test.com", invite.Value.ID);

        Assert.IsTrue(joined.IsSuccess);

        await using var finalContext = CreateContext();
        var user = await finalContext.Users.AsNoTracking().SingleAsync(x => x.UserName == "newcomer@test.com");
        Assert.AreEqual(OrganizationId, user.OrganizationID);
        Assert.AreEqual(0, await finalContext.InviteLinks.CountAsync(),
            "The invitation is consumed when the user joins.");
    }

    [TestMethod]
    public async Task AddInvite_FailsForAnUnknownOrganization()
    {
        await MigrateAndSeedOrganizationAsync();

        var inviteService = CreateInviteService();

        var result = await inviteService.AddInvite("missing-org", new InviteViewModel
        {
            InvitedUser = "newcomer@test.com"
        });

        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task AddDeviceGroup_CreatesTheGroup_AndRefusesADuplicate()
    {
        await MigrateAndSeedOrganizationAsync();

        var groupService = CreateDeviceGroupService();

        var result = await groupService.AddDeviceGroup(OrganizationId, new DeviceGroup
        {
            Name = "Reception"
        });

        Assert.IsTrue(result.IsSuccess);

        var duplicate = await groupService.AddDeviceGroup(OrganizationId, new DeviceGroup
        {
            Name = "reception"
        });

        Assert.IsFalse(duplicate.IsSuccess);
    }

    [TestMethod]
    public async Task AddDeviceToGroup_MovesTheDeviceIntoTheGroup()
    {
        await MigrateAndSeedAsync();

        var groupService = CreateDeviceGroupService();

        var result = await groupService.AddDeviceToGroup("device-2", "group-1");

        Assert.IsTrue(result.IsSuccess);

        await using var context = CreateContext();
        var device = await context.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-2");
        Assert.AreEqual("group-1", device.DeviceGroupID);
    }

    [TestMethod]
    public async Task GetDeviceGroups_ReturnsTheGroupsOfTheUser()
    {
        await MigrateAndSeedAsync();

        var groupService = CreateDeviceGroupService();

        var memberGroups = groupService.GetDeviceGroups("member@test.com");
        var strangerGroups = groupService.GetDeviceGroups("stranger@test.com");
        var adminGroups = groupService.GetDeviceGroups("admin@test.com");

        Assert.AreEqual(1, memberGroups.Length);
        Assert.AreEqual(0, strangerGroups.Length);
        Assert.AreEqual(1, adminGroups.Length);
    }

    [TestMethod]
    public async Task DeleteDeviceGroup_DetachesTheDevicesAndTheUsers()
    {
        await MigrateAndSeedAsync();

        var groupService = CreateDeviceGroupService();

        var result = await groupService.DeleteDeviceGroup(OrganizationId, "group-1");

        Assert.IsTrue(result.IsSuccess);

        await using var context = CreateContext();
        var device = await context.Devices.AsNoTracking().SingleAsync(x => x.ID == "device-1");
        Assert.IsNull(device.DeviceGroupID);
    }

    private UserService CreateUserService()
    {
        return new UserService(
            new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<UserService>.Instance);
    }

    private InviteService CreateInviteService()
    {
        return new InviteService(
            new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<InviteService>.Instance);
    }

    private DeviceGroupService CreateDeviceGroupService()
    {
        return new DeviceGroupService(
            new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<DeviceGroupService>.Instance);
    }

    private async Task MigrateAndSeedAsync()
    {
        await MigrateAndSeedOrganizationAsync();

        await using var context = CreateContext();

        context.Users.AddRange(
            new RemotelyUser
            {
                UserName = "admin@test.com",
                OrganizationID = OrganizationId,
                IsAdministrator = true
            },
            new RemotelyUser
            {
                UserName = "member@test.com",
                OrganizationID = OrganizationId
            },
            new RemotelyUser
            {
                UserName = "stranger@test.com",
                OrganizationID = OrganizationId
            });

        var group = new DeviceGroup
        {
            ID = "group-1",
            Name = "Reception",
            OrganizationID = OrganizationId
        };
        group.Users.Add(context.Users.Local.First(x => x.UserName == "member@test.com"));
        context.DeviceGroups.Add(group);

        context.Devices.Add(new Device
        {
            ID = "device-1",
            DeviceName = "Reception PC",
            OrganizationID = OrganizationId,
            DeviceGroupID = group.ID
        });

        context.Devices.Add(new Device
        {
            ID = "device-2",
            DeviceName = "Server room PC",
            OrganizationID = OrganizationId
        });

        await context.SaveChangesAsync();
    }

    private async Task MigrateAndSeedOrganizationAsync()
    {
        await MigrateAsync();

        await using var context = CreateContext();
        context.Organizations.Add(new Organization
        {
            ID = OrganizationId,
            OrganizationName = "User Tests"
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
