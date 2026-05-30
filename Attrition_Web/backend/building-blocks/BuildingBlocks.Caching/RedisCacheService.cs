using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BuildingBlocks.Caching;

/// <summary>
/// Redis-backed <see cref="ICacheService"/>. Every operation is wrapped so a Redis
/// outage degrades to "cache miss" (reads run the factory, writes are dropped) rather
/// than throwing — keeping the owning service alive when Redis is down.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string _prefix;
    private readonly TimeSpan _defaultTtl;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public RedisCacheService(IConnectionMultiplexer? redis, ILogger<RedisCacheService> logger, string keyPrefix, TimeSpan defaultTtl)
    {
        _redis = redis;
        _logger = logger;
        _prefix = keyPrefix;
        _defaultTtl = defaultTtl;
    }

    private IDatabase? Db()
    {
        try { return _redis?.IsConnected == true ? _redis.GetDatabase() : null; }
        catch { return null; }
    }

    private string K(string key) => $"{_prefix}:{key}";

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory();
        if (value is not null) await SetAsync(key, value, ttl, ct);
        return value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var db = Db();
        if (db == null) return default;
        try
        {
            var val = await db.StringGetAsync(K(key));
            return val.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>((string)val!, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cache GET failed for {Key}; treating as miss", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var db = Db();
        if (db == null) return;
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOpts);
            await db.StringSetAsync(K(key), json, ttl ?? _defaultTtl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cache SET failed for {Key}; dropping", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        var db = Db();
        if (db == null) return;
        try { await db.KeyDeleteAsync(K(key)); }
        catch (Exception ex) { _logger.LogDebug(ex, "Cache DEL failed for {Key}", key); }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (_redis == null) return;
        try
        {
            var pattern = $"{K(prefix)}*";
            foreach (var ep in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(ep);
                if (!server.IsConnected || server.IsReplica) continue;
                var db = _redis.GetDatabase();
                foreach (var redisKey in server.Keys(database: db.Database, pattern: pattern, pageSize: 200))
                    await db.KeyDeleteAsync(redisKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cache prefix-invalidate failed for {Prefix}", prefix);
        }
    }

    public async Task<long?> IncrementAsync(string key, long by = 1, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var db = Db();
        if (db == null) return null;
        try
        {
            var total = await db.StringIncrementAsync(K(key), by);
            if (ttl.HasValue) await db.KeyExpireAsync(K(key), ttl.Value);
            return total;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cache INCR failed for {Key}", key);
            return null;
        }
    }
}
