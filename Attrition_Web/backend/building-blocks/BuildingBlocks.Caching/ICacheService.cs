namespace BuildingBlocks.Caching;

/// <summary>
/// Cache abstraction used across services. Implementations MUST degrade gracefully:
/// if the cache backend is unavailable, reads fall through to the factory and writes
/// are silently dropped — a cache outage must never take a service down.
/// </summary>
public interface ICacheService
{
    /// <summary>Cache-aside: return the cached value, or run <paramref name="factory"/>, cache it, and return it.</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Get a cached value, or default if missing/unavailable.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Set a value with an optional TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Remove a single key.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Remove every key matching a glob pattern (e.g. "wiki:articles:*"). Used to invalidate a group on write.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Write-behind counter: atomically increment a Redis counter and return the new total.
    /// Lets hot counters (play counts, views) absorb writes in Redis instead of hammering Postgres
    /// on every hit. Returns null when the cache is unavailable so the caller can fall back to the DB.
    /// </summary>
    Task<long?> IncrementAsync(string key, long by = 1, TimeSpan? ttl = null, CancellationToken ct = default);
}
