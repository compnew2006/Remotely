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
using Remotely.Server.Options;

namespace Remotely.Server.Tests;

[TestClass]
public class ApplicationOptionsValidationTests
{
    private readonly ApplicationOptionsValidator _validator = new();

    [TestMethod]
    public void Defaults_AreValid()
    {
        var result = _validator.Validate(null, new ApplicationOptions());

        Assert.IsTrue(result.Succeeded, result.FailureMessage);
    }

    [TestMethod]
    [DataRow("SQLite")]
    [DataRow("sqlserver")]
    [DataRow(" PostgreSQL ")]
    public void SupportedProviders_AreAccepted(string provider)
    {
        var result = _validator.Validate(
            null,
            new ApplicationOptions { DbProvider = provider });

        Assert.IsTrue(result.Succeeded, result.FailureMessage);
    }

    [TestMethod]
    public void UnknownProvider_IsRejected()
    {
        var result = _validator.Validate(
            null,
            new ApplicationOptions { DbProvider = "mysql" });

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.FailureMessage, "sqlite");
        StringAssert.Contains(result.FailureMessage, "sqlserver");
        StringAssert.Contains(result.FailureMessage, "postgresql");
    }

    [TestMethod]
    [DataRow("172.28.0.1", true)]
    [DataRow("::1", true)]
    [DataRow("", true)]
    [DataRow("not-an-ip", false)]
    public void DockerGatewayIp_IsValidated(string gatewayIp, bool expected)
    {
        var result = _validator.Validate(
            null,
            new ApplicationOptions { DockerGatewayIp = gatewayIp });

        Assert.AreEqual(expected, result.Succeeded, result.FailureMessage);
    }

    [TestMethod]
    public void AppDbFactory_UsesTheDefaultProvider_WhenTheSectionIsMissing()
    {
        var factory = new AppDbFactory(
            BuildConfiguration(new Dictionary<string, string?>()),
            new TestWebHostEnvironment());

        using var context = factory.GetContext();

        Assert.IsInstanceOfType(context, typeof(SqliteDbContext));
    }

    [TestMethod]
    public void AppDbFactory_HonorsTheTestProvider()
    {
        var factory = new AppDbFactory(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ApplicationOptions:DbProvider"] = "inmemory"
            }),
            new TestWebHostEnvironment());

        using var context = factory.GetContext();

        Assert.IsInstanceOfType(context, typeof(TestingDbContext));
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
