using Remotely.Server.Options;

namespace Remotely.Server.Data;

public interface IAppDbFactory
{
    AppDb GetContext();
}


public class AppDbFactory : IAppDbFactory
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnv;

    public AppDbFactory(
        IConfiguration configuration,
        IWebHostEnvironment hostEnv)
    {
        _configuration = configuration;
        _hostEnv = hostEnv;
    }

    public AppDb GetContext()
    {
        // Uses the same normalization as the composition root, so a missing
        // section resolves to the documented default instead of throwing here
        // while Program accepted the default.
        var dbProvider = ApplicationOptions.NormalizeDbProvider(
            _configuration["ApplicationOptions:DbProvider"]);

        return dbProvider switch
        {
            "sqlite" => new SqliteDbContext(_configuration, _hostEnv),
            "sqlserver" => new SqlServerDbContext(_configuration, _hostEnv),
            "postgresql" => new PostgreSqlDbContext(_configuration, _hostEnv),
            // Test-only provider; a real host rejects it at startup.
            "inmemory" => new TestingDbContext(_hostEnv),
            _ => throw new ArgumentException(
                $"Unknown DB provider '{dbProvider}'."),
        };
    }
}