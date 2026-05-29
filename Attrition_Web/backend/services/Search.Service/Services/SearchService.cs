using System.Collections.Concurrent;
using Search.Service.Clients;
using Search.Service.DTOs;

namespace Search.Service.Services;

public interface ISearchService
{
    Task<GlobalSearchResponse> GlobalSearchAsync(string query, int limit, CancellationToken ct);
}

public class SearchService : ISearchService
{
    private readonly WikiSearchClient _wiki;
    private readonly ForumSearchClient _forum;
    private readonly IdentitySearchClient _identity;
    private readonly EnemySearchClient _enemy;
    private readonly ILogger<SearchService> _logger;

    public SearchService(WikiSearchClient wiki, ForumSearchClient forum, IdentitySearchClient identity,
        EnemySearchClient enemy, ILogger<SearchService> logger)
    {
        _wiki = wiki;
        _forum = forum;
        _identity = identity;
        _enemy = enemy;
        _logger = logger;
    }

    public async Task<GlobalSearchResponse> GlobalSearchAsync(string query, int limit, CancellationToken ct)
    {
        var degraded = new ConcurrentBag<string>();

        var wikiTask = Safe("wiki", () => _wiki.SearchAsync(query, limit, ct), degraded);
        var userTask = Safe("identity", () => _identity.SearchAsync(query, limit, ct), degraded);
        var postTask = Safe("forum", () => _forum.SearchAsync(query, limit, ct), degraded);
        var enemyTask = Safe("enemy", () => _enemy.SearchAsync(query, limit, ct), degraded);

        await Task.WhenAll(wikiTask, userTask, postTask, enemyTask);

        return new GlobalSearchResponse(
            wikiTask.Result, userTask.Result, postTask.Result, enemyTask.Result,
            degraded.ToList());
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
