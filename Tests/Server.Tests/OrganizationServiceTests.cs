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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.Tests;

/// <summary>
/// The organization domain, exercised against a real SQLite database.
/// </summary>
[TestClass]
public class OrganizationServiceTests
{
    private const string OrganizationId = "org-1";
    private const string OtherOrganizationId = "org-2";

    private string _databasePath = null!;
    private string _connectionString = null!;
    private TestHostEnvironment _hostEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-org-tests-{Guid.NewGuid():N}.db");
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
    public async Task GetOrganizationById_ReturnsTheOrganization()
    {
        await MigrateAndSeedAsync();

        var organizationService = CreateOrganizationService();

        var result = await organizationService.GetOrganizationById(OrganizationId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("First Org", result.Value.OrganizationName);

        var missing = await organizationService.GetOrganizationById("does-not-exist");
        Assert.IsFalse(missing.IsSuccess);
    }

    [TestMethod]
    public async Task GetOrganizationCount_CountsTheOrganizations()
    {
        await MigrateAndSeedAsync();

        var organizationService = CreateOrganizationService();

        Assert.AreEqual(2, organizationService.GetOrganizationCount());
        Assert.AreEqual(2, await organizationService.GetOrganizationCountAsync());
    }

    [TestMethod]
    public async Task SetIsDefaultOrganization_LeavesOnlyOneDefault()
    {
        await MigrateAndSeedAsync();

        var organizationService = CreateOrganizationService();

        await organizationService.SetIsDefaultOrganization(OtherOrganizationId, true);

        var defaultOrganization = await organizationService.GetDefaultOrganization();

        Assert.IsTrue(defaultOrganization.IsSuccess);
        Assert.AreEqual(OtherOrganizationId, defaultOrganization.Value.ID);

        await using var context = CreateContext();
        Assert.AreEqual(1, await context.Organizations.CountAsync(x => x.IsDefaultOrganization));
    }

    [TestMethod]
    public async Task UpdateOrganizationName_StoresTheName()
    {
        await MigrateAndSeedAsync();

        var organizationService = CreateOrganizationService();

        var result = await organizationService.UpdateOrganizationName(OrganizationId, "Renamed");

        Assert.IsTrue(result.IsSuccess);

        await using var context = CreateContext();
        var stored = await context.Organizations.AsNoTracking().SingleAsync(x => x.ID == OrganizationId);
        Assert.AreEqual("Renamed", stored.OrganizationName);
    }

    [TestMethod]
    public async Task GetBrandingInfo_CreatesTheRowOnce()
    {
        await MigrateAndSeedAsync();

        var organizationService = CreateOrganizationService();

        var first = await organizationService.GetBrandingInfo(OrganizationId);
        var second = await organizationService.GetBrandingInfo(OrganizationId);

        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(OrganizationId, first.Value.OrganizationId);

        await using var context = CreateContext();
        Assert.AreEqual(1, await context.BrandingInfos.CountAsync(),
            "Reading the branding must not create a second row.");
    }

    [TestMethod]
    public async Task UpdateBrandingInfo_StoresTheProductAndTheIcon()
    {
        await MigrateAndSeedAsync();

        var organizationService = CreateOrganizationService();

        await organizationService.UpdateBrandingInfo(
            OrganizationId,
            "Remotely",
            new byte[] { 1, 2, 3 });

        var branding = await organizationService.GetBrandingInfo(OrganizationId);

        Assert.IsTrue(branding.IsSuccess);
        Assert.AreEqual("Remotely", branding.Value.Product);
        Assert.AreEqual(3, branding.Value.Icon?.Length);
    }

    [TestMethod]
    public async Task ResetBranding_RestoresTheDefaults()
    {
        await MigrateAndSeedAsync();

        var organizationService = CreateOrganizationService();

        await organizationService.UpdateBrandingInfo(
            OrganizationId,
            "Remotely",
            new byte[] { 1, 2, 3 });

        await organizationService.ResetBranding(OrganizationId);

        await using var context = CreateContext();
        var stored = await context.BrandingInfos.AsNoTracking().SingleAsync();

        Assert.AreEqual(BrandingInfo.Default.Product, stored.Product);
        Assert.AreEqual(0, stored.Icon?.Length ?? 0, "The icon must be reset to the default.");
    }

    private OrganizationService CreateOrganizationService()
    {
        return new OrganizationService(
            new TestAppDbFactory(_connectionString, _hostEnvironment));
    }

    private async Task MigrateAndSeedAsync()
    {
        await MigrateAsync();

        await using var context = CreateContext();
        context.Organizations.AddRange(
            new Organization
            {
                ID = OrganizationId,
                OrganizationName = "First Org",
                IsDefaultOrganization = true
            },
            new Organization
            {
                ID = OtherOrganizationId,
                OrganizationName = "Second Org"
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
