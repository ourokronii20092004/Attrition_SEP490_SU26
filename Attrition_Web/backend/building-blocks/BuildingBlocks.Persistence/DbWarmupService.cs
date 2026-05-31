using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Warms the data path right after startup so the FIRST real user request isn't slow.
/// On boot a service pays: EF model materialization, connection-pool priming, and JIT of the
/// query path. Doing a trivial query here (in the background, not blocking startup) moves that
/// cost off the user's first click — the reported "first load takes 10-20s, then fine" symptom.
/// Generic: resolves the base DbContext that every data service registers in DI.
/// </summary>
public sealed class DbWarmupService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DbWarmupService> _logger;

    public DbWarmupService(IServiceProvider sp, ILogger<DbWarmupService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            _ = db.Model;                                      // force EF model materialization
            await db.Database.CanConnectAsync(stoppingToken);  // prime the connection pool
            _logger.LogInformation("DB warmup complete.");
        }
        catch (Exception ex)
        {
            // Non-fatal: a failed warmup just means the first request pays the cost as before.
            _logger.LogWarning(ex, "DB warmup skipped (non-fatal).");
        }
    }
}

public static class DbWarmupExtensions
{
    public static IServiceCollection AddDbWarmup(this IServiceCollection services)
    {
        services.AddHostedService<DbWarmupService>();
        return services;
    }
}
