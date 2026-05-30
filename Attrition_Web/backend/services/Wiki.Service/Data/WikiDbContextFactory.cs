using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Wiki.Service.Data;

public class WikiDbContextFactory : IDesignTimeDbContextFactory<WikiDbContext>
{
    public WikiDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=attrition;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<WikiDbContext>()
            .UseNpgsql(conn, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "wiki"))
            .Options;

        return new WikiDbContext(options);
    }
}
