using System.Collections.Concurrent;
using BuildingBlocks.Caching;
using Search.Service.Clients;
using Search.Service.DTOs;

namespace Search.Service.Services;

public interface ISearchService
{
    Task<GlobalSearchResponse> GlobalSearchAsync(string query, int limit, bool includeUsers, CancellationToken ct);
}

public class SearchService : ISearchService
{
    private readonly WikiSearchClient _wiki;
    private readonly ForumSearchClient _forum;
    private readonly IdentitySearchClient _identity;
    private readonly EnemySearchClient _enemy;
    private readonly ICacheService _cache;
    private readonly ILogger<SearchService> _logger;

    public SearchService(WikiSearchClient wiki, ForumSearchClient forum, IdentitySearchClient identity,
        EnemySearchClient enemy, ICacheService cache, ILogger<SearchService> logger)
    {
        _wiki = wiki;
        _forum = forum;
        _identity = identity;
        _enemy = enemy;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GlobalSearchResponse> GlobalSearchAsync(string query, int limit, bool includeUsers, CancellationToken ct)
    {
        var (scope, term) = ParseScope(query);
        if (string.IsNullOrWhiteSpace(term))
            return new GlobalSearchResponse(
                new List<SearchWikiResultDto>(), new List<SearchUserResultDto>(),
                new List<SearchPostResultDto>(), new List<SearchEnemyResultDto>(), new List<string>());

        // Repeated identical queries (autocomplete typing the same term) are common; cache briefly.
        // Skip caching when any source degraded so a transient failure isn't cached as "no results".
        var cacheKey = $"{scope ?? "all"}:{includeUsers}:{limit}:{term.ToLowerInvariant()}";
        var cached = await _cache.GetAsync<GlobalSearchResponse>(cacheKey, ct);
        if (cached != null) return cached;

        var result = await RunSearchAsync(scope, term, limit, includeUsers, ct);
        if (result.DegradedSources.Count == 0)
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(60), ct);
        return result;
    }

    private async Task<GlobalSearchResponse> RunSearchAsync(string? scope, string term, int limit, bool includeUsers, CancellationToken ct)
    {
        var degraded = new ConcurrentBag<string>();

        var runWiki = scope is null or "wiki";
        var runForum = scope is null or "forum";
        var runEnemy = scope is null or "enemy";
        var runUsers = (scope is null or "user") && includeUsers;

        var wikiTask = runWiki ? Safe("wiki", () => _wiki.SearchAsync(term, limit, ct), degraded) : Empty<SearchWikiResultDto>();
        var userTask = runUsers ? Safe("identity", () => _identity.SearchAsync(term, limit, ct), degraded) : Empty<SearchUserResultDto>();
        var postTask = runForum ? Safe("forum", () => _forum.SearchAsync(term, limit, ct), degraded) : Empty<SearchPostResultDto>();
        var enemyTask = runEnemy ? Safe("enemy", () => _enemy.SearchAsync(term, limit, ct), degraded) : Empty<SearchEnemyResultDto>();

        await Task.WhenAll(wikiTask, userTask, postTask, enemyTask);

        return new GlobalSearchResponse(
            wikiTask.Result, userTask.Result, postTask.Result, enemyTask.Result,
            degraded.ToList());
    }

    private static Task<List<T>> Empty<T>() => Task.FromResult(new List<T>());

    /// <summary>Splits a "scope:term" query. Returns (null, query) when no known prefix is present.</summary>
    private static (string? Scope, string Term) ParseScope(string query)
    {
        var trimmed = (query ?? string.Empty).Trim();
        var colon = trimmed.IndexOf(':');
        if (colon <= 0) return (null, trimmed);

        var prefix = trimmed[..colon].Trim().ToLowerInvariant();
        var rest = trimmed[(colon + 1)..].Trim();
        return prefix switch
        {
            "wiki" => ("wiki", rest),
            "enemy" or "bestiary" => ("enemy", rest),
            "forum" or "post" or "thread" => ("forum", rest),
            "user" or "users" or "member" => ("user", rest),
            _ => (null, trimmed),   // unknown prefix → treat the whole thing as a literal search
        };
    }

    /// <summary>Runs a downstream call; on any failure returns an empty list and records the source as degraded.</summary>
    private async Task<List<T>> Safe<T>(string source, Func<Task<List<T>>> call, ConcurrentBag<string> degraded)
    {
        try
        {
            return await call();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search downstream '{Source}' failed; degrading to empty results", source);
            degraded.Add(source);
            return new List<T>();
        }
    }
}
