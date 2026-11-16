#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.API;
using Remotely.Server.Data;
using Remotely.Server.HealthChecks;
using Remotely.Server.Observability;

namespace Remotely.Server.Tests;

/// <summary>
/// The readiness probe of the server.  It has to report a database that can
/// be reached as healthy, and a database that cannot be reached as unhealthy,
/// so that the container health check and the operator both see the truth.
/// </summary>
[TestClass]
public class HealthCheckTests
{
    [TestMethod]
    public async Task DatabaseHealthCheck_IsHealthy_ForAMigratedDatabase()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-health-{Guid.NewGuid():N}.db");

        try
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SQLite"] = $"Data Source={databasePath}"
            });

            await using (var migration = new SqliteDbContext(
                configuration,
                new TestWebHostEnvironment()))
            {
                await migration.Database.MigrateAsync();
            }

            var check = new DatabaseHealthCheck(
                new AppDbFactory(configuration, new TestWebHostEnvironment()),
                new ServerMetrics());

            var result = await check.CheckHealthAsync(
                new HealthCheckContext(),
                CancellationToken.None);

            Assert.AreEqual(HealthStatus.Healthy, result.Status, result.Description);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [TestMethod]
    public async Task DatabaseHealthCheck_IsUnhealthy_WhenTheDatabaseCannotBeOpened()
    {
        // SQLite does not create missing directories, so this path cannot open.
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-missing-{Guid.NewGuid():N}",
            "health.db");

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SQLite"] = $"Data Source={databasePath}"
        });

        var check = new DatabaseHealthCheck(
            new AppDbFactory(configuration, new TestWebHostEnvironment()),
            new ServerMetrics());

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(databasePath)));
    }

    [TestMethod]
    public async Task HealthCheckController_ReturnsSuccess_ForAHealthyDatabase()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-health-ctrl-{Guid.NewGuid():N}.db");

        try
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SQLite"] = $"Data Source={databasePath}"
            });

            await using (var migration = new SqliteDbContext(
                configuration,
                new TestWebHostEnvironment()))
            {
                await migration.Database.MigrateAsync();
            }

            var controller = new HealthCheckController(CreateHealthCheckService(configuration));

            var result = await controller.Get(CancellationToken.None);

            Assert.IsInstanceOfType(result, typeof(OkResult));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [TestMethod]
    public async Task HealthCheckController_ReturnsFailure_WhenTheDatabaseIsUnreachable()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"remotely-health-ctrl-missing-{Guid.NewGuid():N}",
            "health.db");

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SQLite"] = $"Data Source={databasePath}"
        });

        var controller = new HealthCheckController(CreateHealthCheckService(configuration));

        var result = await controller.Get(CancellationToken.None);

        Assert.IsInstanceOfType(result, typeof(StatusCodeResult));
        Assert.AreEqual(
            StatusCodes.Status503ServiceUnavailable,
            ((StatusCodeResult)result).StatusCode);
    }

    private static HealthCheckService CreateHealthCheckService(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ServerMetrics>();
        services.AddSingleton<IAppDbFactory>(
            new AppDbFactory(configuration, new TestWebHostEnvironment()));
        services
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name);

        return services.BuildServiceProvider().GetRequiredService<HealthCheckService>();
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Remotely.Server.Tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
