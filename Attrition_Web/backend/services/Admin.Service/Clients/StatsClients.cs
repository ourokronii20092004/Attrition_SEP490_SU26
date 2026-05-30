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
    private readonly ILogger _logger;

    public StatsClient(HttpClient http, IConfiguration config, ILogger logger)
    {
        _http = http;
        _logger = logger;
        var key = config["Internal:ApiKey"];
        if (!string.IsNullOrEmpty(key))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Internal-Key", key);
    }

    public async Task<int> GetCountAsync(string path, CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<InternalEnvelope<int>>(path, JsonOpts, ct);
        if (envelope is null)
            _logger.LogWarning("Stats endpoint {Path} returned a null/unparseable body; treating count as 0", path);
        return envelope?.Data ?? 0;
    }

    public async Task<JsonElement> GetObjectAsync(string path, CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<InternalEnvelope<JsonElement>>(path, JsonOpts, ct);
        if (envelope is null)
            _logger.LogWarning("Stats endpoint {Path} returned a null/unparseable body", path);
        return envelope?.Data ?? default;
    }
}

public sealed class IdentityStatsClient : StatsClient
{ public IdentityStatsClient(HttpClient http, IConfiguration config, ILogger<IdentityStatsClient> logger) : base(http, config, logger) { } }
public sealed class WikiStatsClient : StatsClient
{ public WikiStatsClient(HttpClient http, IConfiguration config, ILogger<WikiStatsClient> logger) : base(http, config, logger) { } }
public sealed class ForumStatsClient : StatsClient
{ public ForumStatsClient(HttpClient http, IConfiguration config, ILogger<ForumStatsClient> logger) : base(http, config, logger) { } }
public sealed class EnemyStatsClient : StatsClient
{ public EnemyStatsClient(HttpClient http, IConfiguration config, ILogger<EnemyStatsClient> logger) : base(http, config, logger) { } }
public sealed class AssetsStatsClient : StatsClient
{ public AssetsStatsClient(HttpClient http, IConfiguration config, ILogger<AssetsStatsClient> logger) : base(http, config, logger) { } }
public sealed class MusicStatsClient : StatsClient
{ public MusicStatsClient(HttpClient http, IConfiguration config, ILogger<MusicStatsClient> logger) : base(http, config, logger) { } }
