using BuildingBlocks.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Search.Service.Clients;
using Search.Service.DTOs;
using Search.Service.Services;
using System.Net;
using System.Text;

namespace Search.Service.Tests;

public class SearchServiceTests
{
    [Fact]
    public async Task UTCID01_UnscopedQuery_MergesAllHealthySources()
    {
        var sut = Create();
        var r = await sut.GlobalSearchAsync("dragon", 10, true, default);
        Assert.Single(r.Wiki);
        Assert.Single(r.Users);
        Assert.Single(r.Posts);
        Assert.Single(r.Enemies);
        Assert.Single(r.Items);
        Assert.Single(r.Skills);
        Assert.Empty(r.DegradedSources);
    }

    [Fact]
    public async Task UTCID02_EmptyQuery_ReturnsEmptyWithoutDownstreams()
    {
        var h = new Handler();
        var r = await Create(h).GlobalSearchAsync("   ", 10, true, default);
        Assert.Empty(r.Wiki);
        Assert.Equal(0, h.Calls);
    }

    [Fact]
    public async Task UTCID03_WikiScope_CallsOnlyWiki()
    {
        var h = new Handler();
        var r = await Create(h).GlobalSearchAsync("wiki: flame", 10, true, default);
        Assert.Single(r.Wiki);
        Assert.Equal(1, h.Calls);
    }

    [Fact]
    public async Task UTCID04_IncludeUsersFalse_OmitsIdentity()
    {
        var h = new Handler();
        var r = await Create(h).GlobalSearchAsync("dragon", 10, false, default);
        Assert.Empty(r.Users);
        Assert.Equal(5, h.Calls);
    }

    [Fact]
    public async Task UTCID05_DownstreamFailure_DegradesOnlyThatSource()
    {
        var h = new Handler("forum");
        var r = await Create(h).GlobalSearchAsync("dragon", 10, true, default);
        Assert.Empty(r.Posts);
        Assert.Contains("forum", r.DegradedSources);
        Assert.Single(r.Wiki);
    }

    [Fact]
    public async Task UTCID06_CachedResponse_SkipsDownstreams()
    {
        var h = new Handler();
        var c = new Cache();
        var expected = GlobalSearchResponse.Empty();
        c.Value = expected;
        Assert.Same(expected, await Create(h, c).GlobalSearchAsync("dragon", 10, true, default));
        Assert.Equal(0, h.Calls);
    }

    [Fact]
    public async Task UTCID07_DegradedResponse_IsNotCached()
    {
        var c = new Cache();
        await Create(new Handler("wiki"), c).GlobalSearchAsync("dragon", 10, true, default);
        Assert.Equal(0, c.Sets);
    }

    [Fact]
    public async Task UTCID08_HealthyResponse_IsCachedForReuse()
    {
        var c = new Cache();
        await Create(new Handler(), c).GlobalSearchAsync("dragon", 10, true, default);
        Assert.Equal(1, c.Sets);
    }

    private static SearchService Create(Handler? handler = null, Cache? cache = null)
    {
        handler ??= new();
        cache ??= new();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Internal:ApiKey", "test" } }).Build();
        HttpClient Client()
        {
            return new(handler, false) { BaseAddress = new Uri("http://test/") };
        }
        return new(new WikiSearchClient(Client(), config), new ForumSearchClient(Client(), config), new IdentitySearchClient(Client(), config), new EnemySearchClient(Client(), config), new SkillSearchClient(Client(), config), cache, NullLogger<SearchService>.Instance);
    }
}

internal sealed class Handler(string? fail = null) : HttpMessageHandler
{
    internal int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        Calls++; var path = req.RequestUri!.AbsolutePath;
        if (fail != null && path.Contains(fail, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var data = path switch { "/api/internal/wiki/search" => "[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"title\":\"Flame\",\"slug\":\"flame\",\"categorySlug\":\"lore\"}]", "/api/internal/users/search" => "[{\"id\":\"00000000-0000-0000-0000-000000000002\",\"username\":\"player\"}]", "/api/internal/forum/search" => "[{\"id\":\"00000000-0000-0000-0000-000000000003\",\"threadId\":\"00000000-0000-0000-0000-000000000004\",\"threadTitle\":\"Dragon\",\"snippet\":\"x\"}]", "/api/internal/enemies/search" => "[{\"enemyId\":\"dragon\",\"name\":\"Dragon\",\"tier\":\"Boss\"}]", "/api/internal/enemies/items/search" => "[{\"itemId\":\"scale\",\"name\":\"Scale\",\"category\":\"Material\",\"rarity\":\"Rare\"}]", "/api/internal/skills/search" => "[{\"skillId\":\"fire\",\"name\":\"Fire\",\"element\":\"Fire\",\"rarity\":\"Rare\"}]", _ => "[]" };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"data\":{data}}}", Encoding.UTF8, "application/json") });
    }
}

internal sealed class Cache : ICacheService
{
    internal GlobalSearchResponse? Value; internal int Sets;

    public Task<T?> GetAsync<T>(string k, CancellationToken c = default) => Task.FromResult((T?)(object?)Value); public Task SetAsync<T>(string k, T v, TimeSpan? t = null, CancellationToken c = default)

    { Sets++; Value = (GlobalSearchResponse)(object)v!; return Task.CompletedTask; }

    public async Task<T> GetOrSetAsync<T>(string k, Func<Task<T>> f, TimeSpan? t = null, CancellationToken c = default) => await f(); public Task RemoveAsync(string k, CancellationToken c = default) => Task.CompletedTask; public Task RemoveByPrefixAsync(string p, CancellationToken c = default) => Task.CompletedTask; public Task<long?> IncrementAsync(string k, long b = 1, TimeSpan? t = null, CancellationToken c = default) => Task.FromResult<long?>(null);
}