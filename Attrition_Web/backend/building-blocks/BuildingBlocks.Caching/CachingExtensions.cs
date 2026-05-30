using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BuildingBlocks.Caching;

public static class CachingExtensions
{
    /// <summary>
    /// Registers <see cref="ICacheService"/> backed by Redis. The connection is made lazily and
    /// abortlessly (AbortOnConnectFail=false) so the service still boots when Redis is down; the
    /// cache then behaves as a no-op until Redis comes back. Set Redis:Connection in config
    /// (compose injects Redis__Connection). When unset, a no-op cache is registered.
    /// </summary>
    public static IServiceCollection AddAttritionCache(this IServiceCollection services,
        IConfiguration config, string keyPrefix)
    {
        var connString = config["Redis:Connection"];
        var ttlMinutes = int.TryParse(config["Redis:DefaultTtlMinutes"], out var m) ? m : 10;
        var defaultTtl = TimeSpan.FromMinutes(ttlMinutes);

        if (string.IsNullOrWhiteSpace(connString))
        {
            // No Redis configured → no-op cache (every read is a miss, writes dropped).
            services.AddSingleton<ICacheService>(sp =>
                new RedisCacheService(null, sp.GetRequiredService<ILogger<RedisCacheService>>(), keyPrefix, defaultTtl));
            return services;
        }

        IConnectionMultiplexer? mux = null;
        try
        {
            var options = ConfigurationOptions.Parse(connString);
            options.AbortOnConnectFail = false;     // don't throw at startup if Redis is unreachable
            options.ConnectRetry = 3;
            options.ConnectTimeout = 2000;
            mux = ConnectionMultiplexer.Connect(options);
        }
        catch
        {
            // Swallow — a null multiplexer makes RedisCacheService a no-op until restarted.
        }

        services.AddSingleton<ICacheService>(sp =>
            new RedisCacheService(mux, sp.GetRequiredService<ILogger<RedisCacheService>>(), keyPrefix, defaultTtl));
        return services;
    }
}
