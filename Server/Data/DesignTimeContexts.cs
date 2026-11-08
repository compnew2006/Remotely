using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.FileProviders;

namespace Remotely.Server.Data;

public class SqliteDbContextDesignTime : IDesignTimeDbContextFactory<SqliteDbContext>
{
    public SqliteDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeContextConfiguration.Create("SQLite", "SQLite");

        return new SqliteDbContext(configuration, new DesignTimeWebHostEnvironment());
    }

}
public class SqlServerDbContextDesignTime : IDesignTimeDbContextFactory<SqlServerDbContext>
{
    public SqlServerDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeContextConfiguration.Create("SQLServer", "SQLServer");

        return new SqlServerDbContext(configuration, new DesignTimeWebHostEnvironment());
    }
}

public class PostgreSqlDbContextDesignTime : IDesignTimeDbContextFactory<PostgreSqlDbContext>
{
    public PostgreSqlDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeContextConfiguration.Create("PostgreSQL", "PostgreSQL");

        return new PostgreSqlDbContext(configuration, new DesignTimeWebHostEnvironment());
    }
}

internal static class DesignTimeContextConfiguration
{
    public static IConfiguration Create(string provider, string connectionStringName)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables("Remotely_")
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationOptions:DbProvider"] = provider
            })
            .Build();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionStringName)))
        {
            throw new InvalidOperationException(
                $"Set the Remotely_ConnectionStrings__{connectionStringName} environment variable.");
        }

        return configuration;
    }
}


public class DesignTimeWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = default!;
    public string ApplicationName { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = default!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Development";
}
