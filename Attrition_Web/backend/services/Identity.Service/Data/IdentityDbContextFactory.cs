using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Service.Data;

public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=attrition;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(conn, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options;

        return new IdentityDbContext(options);
    }
}
