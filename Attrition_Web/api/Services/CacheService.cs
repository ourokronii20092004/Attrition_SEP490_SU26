using StackExchange.Redis;
using System.Text.Json;

namespace Attrition.API.Services;

public class CacheService : ICacheService
{
    private readonly IDatabase _db;

    public CacheService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        if (expiry.HasValue)
        {
            await _db.StringSetAsync(key, json, expiry.Value);
        }
        else
        {
            await _db.StringSetAsync(key, json);
        }
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        // Note: KEYS is not recommended for production with large datasets.
        // For this scale it's fine. Consider SCAN for larger deployments.
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern).ToArray();
        if (keys.Any()) await _db.KeyDeleteAsync(keys);
    }
}