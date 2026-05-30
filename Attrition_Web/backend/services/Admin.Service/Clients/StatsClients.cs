using System.Net.Http.Json;
using System.Text.Json;

namespace Admin.Service.Clients;

public record InternalEnvelope<T>(bool Success, T? Data, string? Error);

/// <summary>
/// Calls downstream internal stats/count endpoints with the shared internal key.
/// Throws on failure so the caller can mark the source unavailable.
/// </summary>
public class StatsClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public StatsClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var key = config["Internal:ApiKey"];
        if (!string.IsNullOrEmpty(key))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Internal-Key", key);
    }

    public async Task<int> GetCountAsync(string path, CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<InternalEnvelope<int>>(path, JsonOpts, ct);
        return envelope?.Data ?? 0;
    }

    public async Task<JsonElement> GetObjectAsync(string path, CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<InternalEnvelope<JsonElement>>(path, JsonOpts, ct);
        return envelope!.Data;
    }
}

public sealed class IdentityStatsClient : StatsClient
{ public IdentityStatsClient(HttpClient http, IConfiguration config) : base(http, config) { } }
public sealed class WikiStatsClient : StatsClient
{ public WikiStatsClient(HttpClient http, IConfiguration config) : base(http, config) { } }
public sealed class ForumStatsClient : StatsClient
{ public ForumStatsClient(HttpClient http, IConfiguration config) : base(http, config) { } }
public sealed class EnemyStatsClient : StatsClient
{ public EnemyStatsClient(HttpClient http, IConfiguration config) : base(http, config) { } }
public sealed class AssetsStatsClient : StatsClient
{ public AssetsStatsClient(HttpClient http, IConfiguration config) : base(http, config) { } }
public sealed class MusicStatsClient : StatsClient
{ public MusicStatsClient(HttpClient http, IConfiguration config) : base(http, config) { } }
