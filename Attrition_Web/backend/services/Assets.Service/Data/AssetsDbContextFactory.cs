using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Assets.Service.Data;

public class AssetsDbContextFactory : IDesignTimeDbContextFactory<AssetsDbContext>
{
    public AssetsDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=attrition;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AssetsDbContext>()
            .UseNpgsql(conn, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "assets"))
            .Options;

        return new AssetsDbContext(options);
    }
}
