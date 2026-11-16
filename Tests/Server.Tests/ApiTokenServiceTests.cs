#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
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
/// The API token domain, exercised against a real SQLite database.
/// </summary>
[TestClass]
public class ApiTokenServiceTests
{
    private const string OrganizationId = "token-org-1";
    private const string UserName = "token-user@test.com";

    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-token-tests-{Guid.NewGuid():N}.db");
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
    public async Task CreateApiToken_StoresTheTokenForTheUsersOrganization()
    {
        await MigrateAndSeedUserAsync();

        var tokenService = CreateTokenService();

        var result = await tokenService.CreateApiToken(
            UserName,
            "automation",
            HashSecret("secret"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrganizationId, result.Value.OrganizationID);
        Assert.AreEqual("automation", result.Value.Name);

        await using var context = CreateContext();
        Assert.AreEqual(1, await context.ApiTokens.CountAsync());
    }

    [TestMethod]
    public async Task CreateApiToken_FailsForAnUnknownUser()
    {
        await MigrateAsync();

        var tokenService = CreateTokenService();

        var result = await tokenService.CreateApiToken(
            "missing@test.com",
            "automation",
            HashSecret("secret"));

        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task ValidateApiKey_AcceptsTheRightSecret_AndRecordsTheUse()
    {
        await MigrateAndSeedUserAsync();

        var tokenService = CreateTokenService();

        var created = await tokenService.CreateApiToken(
            UserName,
            "automation",
            HashSecret("secret"));

        var isValid = await tokenService.ValidateApiKey(
            created.Value!.ID,
            "secret",
            "/api/devices",
            "127.0.0.1");

        Assert.IsTrue(isValid);

        await using var context = CreateContext();
        var stored = await context.ApiTokens.AsNoTracking().SingleAsync();

        Assert.IsNotNull(stored.LastUsed);
    }

    [TestMethod]
    public async Task ValidateApiKey_RefusesAWrongSecret_WithoutWriting()
    {
        await MigrateAndSeedUserAsync();

        var tokenService = CreateTokenService();

        var created = await tokenService.CreateApiToken(
            UserName,
            "automation",
            HashSecret("secret"));

        var isValid = await tokenService.ValidateApiKey(
            created.Value!.ID,
            "wrong",
            "/api/devices",
            "127.0.0.1");

        Assert.IsFalse(isValid);

        await using var context = CreateContext();
        var stored = await context.ApiTokens.AsNoTracking().SingleAsync();

        Assert.IsNull(stored.LastUsed);
    }

    [TestMethod]
    public async Task RenameAndDelete_WorkOnTheTokensOfTheOrganization()
    {
        await MigrateAndSeedUserAsync();

        var tokenService = CreateTokenService();

        var created = await tokenService.CreateApiToken(
            UserName,
            "automation",
            HashSecret("secret"));

        var renamed = await tokenService.RenameApiToken(UserName, created.Value!.ID, "renamed");
        Assert.IsTrue(renamed.IsSuccess);

        await using (var context = CreateContext())
        {
            var stored = await context.ApiTokens.AsNoTracking().SingleAsync();
            Assert.AreEqual("renamed", stored.Name);
        }

        var deleted = await tokenService.DeleteApiToken(UserName, created.Value!.ID);
        Assert.IsTrue(deleted.IsSuccess);

        await using var finalContext = CreateContext();
        Assert.AreEqual(0, await finalContext.ApiTokens.CountAsync());
    }

    [TestMethod]
    public async Task GetAllApiTokens_ReturnsTheTokensOfTheOrganization()
    {
        await MigrateAndSeedUserAsync();

        var tokenService = CreateTokenService();

        await tokenService.CreateApiToken(UserName, "one", HashSecret("secret"));
        await tokenService.CreateApiToken(UserName, "two", HashSecret("secret"));

        string userId;
        await using (var context = CreateContext())
        {
            userId = await context.Users.AsNoTracking().Select(x => x.Id).SingleAsync();
        }

        var tokens = tokenService.GetAllApiTokens(userId);

        Assert.AreEqual(2, tokens.Length);
    }

    private static string HashSecret(string secret)
    {
        var hasher = new PasswordHasher<string>();
        return hasher.HashPassword(string.Empty, secret);
    }

    private ApiTokenService CreateTokenService()
    {
        return new ApiTokenService(
            new TestAppDbFactory(_connectionString, _hostEnvironment),
            NullLogger<ApiTokenService>.Instance);
    }

    private async Task MigrateAndSeedUserAsync()
    {
        await MigrateAsync();

        await using var context = CreateContext();
        context.Organizations.Add(new Organization
        {
            ID = OrganizationId,
            OrganizationName = "Token Tests"
        });
        context.Users.Add(new RemotelyUser
        {
            UserName = UserName,
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
