using BuildingBlocks.Caching;

namespace Enemy.Service.Tests;

internal sealed class TestCache : ICacheService
{
    private readonly Dictionary<string, object> _values = new();
    internal List<string> Removed { get; } = new();
    internal int FactoryCalls { get; private set; }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (_values.TryGetValue(key, out var value)) return (T)value;
        FactoryCalls++;
        var result = await factory();
        _values[key] = result!;
        return result;
    }

    internal void Seed<T>(string key, T value) => _values[key] = value!;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        return Task.FromResult(_values.TryGetValue(key, out var value) ? (T?)value : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        _values[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        Removed.Add(key);
        _values.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        Removed.Add(prefix);
        return Task.CompletedTask;
    }

    public Task<long?> IncrementAsync(string key, long by = 1, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        return Task.FromResult<long?>(null);
    }
}