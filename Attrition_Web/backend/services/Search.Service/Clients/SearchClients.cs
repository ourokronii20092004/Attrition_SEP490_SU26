using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Contracts;
using Search.Service.DTOs;

namespace Search.Service.Clients;

public abstract class InternalClientBase
{
    protected readonly HttpClient Http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    protected InternalClientBase(HttpClient http, IConfiguration config)
    {
        Http = http;
        var key = config["Internal:ApiKey"];
        if (!string.IsNullOrEmpty(key))
            Http.DefaultRequestHeaders.TryAddWithoutValidation("X-Internal-Key", key);
    }

    protected async Task<List<T>> GetListAsync<T>(string url, CancellationToken ct)
    {
        var envelope = await Http.GetFromJsonAsync<InternalEnvelope<List<T>>>(url, JsonOpts, ct);
        return envelope?.Data ?? new List<T>();
    }
}

public sealed class WikiSearchClient : InternalClientBase
{
    public WikiSearchClient(HttpClient http, IConfiguration config) : base(http, config) { }
    public Task<List<SearchWikiResultDto>> SearchAsync(string q, int limit, CancellationToken ct)
        => GetListAsync<SearchWikiResultDto>($"api/internal/wiki/search?q={Uri.EscapeDataString(q)}&limit={limit}", ct);
}

public sealed class ForumSearchClient : InternalClientBase
{
    public ForumSearchClient(HttpClient http, IConfiguration config) : base(http, config) { }
    public Task<List<SearchPostResultDto>> SearchAsync(string q, int limit, CancellationToken ct)
        => GetListAsync<SearchPostResultDto>($"api/internal/forum/search?q={Uri.EscapeDataString(q)}&limit={limit}", ct);
}

public sealed class IdentitySearchClient : InternalClientBase
{
    public IdentitySearchClient(HttpClient http, IConfiguration config) : base(http, config) { }
    public Task<List<SearchUserResultDto>> SearchAsync(string q, int limit, CancellationToken ct)
        => GetListAsync<SearchUserResultDto>($"api/internal/users/search?q={Uri.EscapeDataString(q)}&limit={limit}", ct);
}

public sealed class EnemySearchClient : InternalClientBase
{
    public EnemySearchClient(HttpClient http, IConfiguration config) : base(http, config) { }
    public Task<List<SearchEnemyResultDto>> SearchAsync(string q, int limit, CancellationToken ct)
        => GetListAsync<SearchEnemyResultDto>($"api/internal/enemies/search?q={Uri.EscapeDataString(q)}&limit={limit}", ct);
}
