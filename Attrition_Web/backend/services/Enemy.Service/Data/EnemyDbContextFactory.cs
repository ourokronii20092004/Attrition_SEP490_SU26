using Enemy.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Enemy.Service.Data;

public class EnemyDbContextFactory : IDesignTimeDbContextFactory<EnemyDbContext>
{
    public EnemyDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=attrition;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<EnemyDbContext>()
            .UseNpgsql(conn, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "enemy"))
            .Options;

        return new EnemyDbContext(options);
    }
}
