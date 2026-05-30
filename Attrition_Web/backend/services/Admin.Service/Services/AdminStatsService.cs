using System.Collections.Concurrent;
using System.Text.Json;
using Admin.Service.Clients;
using Admin.Service.DTOs;

namespace Admin.Service.Services;

public interface IAdminStatsService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken ct);
}

public class AdminStatsService : IAdminStatsService
{
    private readonly IdentityStatsClient _identity;
    private readonly WikiStatsClient _wiki;
    private readonly ForumStatsClient _forum;
    private readonly EnemyStatsClient _enemy;
    private readonly AssetsStatsClient _assets;
    private readonly MusicStatsClient _music;
    private readonly ILogger<AdminStatsService> _logger;

    public AdminStatsService(IdentityStatsClient identity, WikiStatsClient wiki, ForumStatsClient forum,
        EnemyStatsClient enemy, AssetsStatsClient assets, MusicStatsClient music, ILogger<AdminStatsService> logger)
    {
        _identity = identity;
        _wiki = wiki;
        _forum = forum;
        _enemy = enemy;
        _assets = assets;
        _music = music;
        _logger = logger;
    }

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var down = new ConcurrentBag<string>();

        var usersT = Safe("identity", () => _identity.GetCountAsync("api/internal/users/count", ct), down);
        var wikiT = Safe("wiki", () => _wiki.GetObjectAsync("api/internal/wiki/stats", ct), down);
        var forumT = Safe("forum", () => _forum.GetObjectAsync("api/internal/forum/stats", ct), down);
        var enemyT = Safe("enemy", () => _enemy.GetCountAsync("api/internal/enemies/count", ct), down);
        var assetsT = Safe("assets", () => _assets.GetCountAsync("api/internal/assets/count", ct), down);
        var musicT = Safe("music", () => _music.GetObjectAsync("api/internal/music/count", ct), down);

        await Task.WhenAll(usersT, wikiT, forumT, enemyT, assetsT, musicT);

        int? wikiArticles = GetInt(wikiT, "articles");
        int? pending = GetInt(wikiT, "pendingContributions");
        int? threads = GetInt(forumT, "threads");
        int? posts = GetInt(forumT, "posts");
        int? removed = GetInt(forumT, "removedPosts");
        int? albums = GetInt(musicT, "albums");
        int? tracks = GetInt(musicT, "tracks");

        return new AdminStatsDto(
            TotalUsers: usersT.Result,
            TotalWikiArticles: wikiArticles,
            PendingContributions: pending,
            TotalForumThreads: threads,
            TotalForumPosts: posts,
            RemovedPosts: removed,
            TotalEnemies: enemyT.Result,
            TotalAssets: assetsT.Result,
            TotalMusicAlbums: albums,
            TotalMusicTracks: tracks,
            UnavailableSources: down.Distinct().ToList());
    }

    private static int? GetInt(Task<JsonElement?> task, string property)
    {
        if (task.Result is not { } el || el.ValueKind != JsonValueKind.Object) return null;
        return el.TryGetProperty(property, out var v) && v.TryGetInt32(out var n) ? n : null;
    }

    private async Task<int?> Safe(string source, Func<Task<int>> call, ConcurrentBag<string> down)
    {
        try { return await call(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin stats source '{Source}' unavailable", source);
            down.Add(source);
            return null;
        }
    }

    private async Task<JsonElement?> Safe(string source, Func<Task<JsonElement>> call, ConcurrentBag<string> down)
    {
        try { return await call(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin stats source '{Source}' unavailable", source);
            down.Add(source);
            return null;
        }
    }
}
