using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Music.Service.Data;

public class MusicDbContextFactory : IDesignTimeDbContextFactory<MusicDbContext>
{
    public MusicDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=attrition;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseNpgsql(conn, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "music"))
            .Options;

        return new MusicDbContext(options);
    }
}
