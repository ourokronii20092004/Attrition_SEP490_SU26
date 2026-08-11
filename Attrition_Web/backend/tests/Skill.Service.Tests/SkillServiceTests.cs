using BuildingBlocks.Caching;
using NSubstitute;
using Skill.Service.DTOs;
using Skill.Service.Models;
using Skill.Service.Repositories.Interface;
using Skill.Service.Services;

namespace Skill.Service.Tests;

public class SkillServiceTests
{
    private readonly ISkillRepository _repo = Substitute.For<ISkillRepository>();
    private readonly Cache _cache = new();
    private SkillService Sut => new(_repo, _cache);

    private static SkillEntity Entity(string id = "fireball") => new()
    {
        SkillId = id,
        Name = "Fireball",
        Description = "fire",
        Rarity = "Rare",
        Element = "Fire",
        ManaCost = 10,
        CastTime = .5f,
        Cooldown = 2,
        ActiveStartFrac = .2f,
        ActiveEndFrac = .7f,
        DamageType = "Magic",
        BaseDamage = 50,
        ApScaling = 1,
        SweetSpotMultiplier = 1,
        Delivery = "Projectile",
        HitShape = "Circle",
        Range = 10,
        ProjectileSpeed = 8,
        ProjectileCount = 1,
        VfxLifetime = 1,
        UpdatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
    };

    private static SkillImportDto Import(string id = "fireball", string name = "Fireball") => new(
        id, name, "fire", null, "Rare", "Fire", 10, .5f, 2, .2f, .7f, "Magic", 50, 1, 0, 0, 0, 1,
        "Projectile", "Circle", 10, 0, 0, 0, 0, 0, 8, 1, 0, 1);

    private static SkillUpdateRequest Update(string? description = "updated", int mana = 20) => new(
        "Updated", description, null, "Epic", "Fire", mana, .5f, 2, .2f, .7f, "Magic", 60, 1, 0, 0, 0, 1,
        "Projectile", "Circle", 10, 0, 0, 0, 0, 0, 8, 1, 0, 1);

    [Fact]
    public async Task Overview_UTCID01_GetAll_MapsEverySkill()
    {
        _repo.GetAllAsync().Returns(new List<SkillEntity> { Entity(), Entity("icebolt") }); Assert.Equal(2, (await Sut.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Overview_UTCID02_ExistingId_ReturnsSkill()
    {
        _repo.GetByIdAsync("fireball").Returns(Entity()); Assert.Equal("fireball", (await Sut.GetByIdAsync("fireball"))!.SkillId);
    }

    [Fact]
    public async Task Overview_UTCID03_UnknownId_ReturnsNull()
    {
        Assert.Null(await Sut.GetByIdAsync("unknown"));
    }

    [Fact]
    public async Task Overview_UTCID04_BundleVersion_UsesNewestTimestampAndCount()
    {
        var rows = new List<SkillEntity> { Entity(), Entity("icebolt") }; _repo.GetAllAsync(true).Returns(rows);
        var result = await Sut.GetConfigBundleAsync(); Assert.Equal(2, result.Count); Assert.Equal($"{rows.Max(x => x.UpdatedAt):O}|2", result.Version);
    }

    [Fact]
    public async Task Overview_UTCID05_EmptyBundle_HasZeroVersion()
    {
        _repo.GetAllAsync(true).Returns(new List<SkillEntity>()); var r = await Sut.GetConfigBundleAsync(); Assert.Equal("0", r.Version); Assert.Empty(r.Skills);
    }

    [Fact]
    public async Task Overview_UTCID06_EmptyGetAll_ReturnsEmpty()
    {
        _repo.GetAllAsync().Returns(new List<SkillEntity>()); Assert.Empty(await Sut.GetAllAsync());
    }

    [Fact]
    public async Task Add_UTCID01_NewImport_IsCreated()
    {
        SkillEntity? added = null; _repo.GetByIdsAsync(Arg.Any<IEnumerable<string>>()).Returns(new Dictionary<string, SkillEntity>());
        _repo.When(x => x.Add(Arg.Any<SkillEntity>())).Do(c => added = c.Arg<SkillEntity>()); _repo.GetVersionInfoAsync().Returns((DateTime.UtcNow, 1));
        var r = await Sut.ImportAsync(new(new() { Import("icebolt", "Icebolt") }));
        Assert.True(r.Success); Assert.Equal(1, r.Data!.Skills.Created); Assert.Equal("icebolt", added!.SkillId);
    }

    [Fact]
    public async Task Add_UTCID02_IdenticalImport_IsUnchanged()
    {
        var e = Entity(); _repo.GetByIdsAsync(Arg.Any<IEnumerable<string>>()).Returns(new Dictionary<string, SkillEntity> { { e.SkillId, e } }); _repo.GetVersionInfoAsync().Returns((e.UpdatedAt, 1));
        var r = await Sut.ImportAsync(new(new() { Import() })); Assert.Equal(1, r.Data!.Skills.Unchanged); _repo.DidNotReceive().Add(Arg.Any<SkillEntity>());
    }

    [Theory]
    [InlineData("UTCID03")]
    [InlineData("UTCID04")]
    [InlineData("UTCID05")]
    [InlineData("UTCID06")]
    public async Task Add_ChangedImport_OverwritesCurrentFields(string _)
    {
        var e = Entity(); var dto = Import(name: "Changed"); _repo.GetByIdsAsync(Arg.Any<IEnumerable<string>>()).Returns(new Dictionary<string, SkillEntity> { { e.SkillId, e } }); _repo.GetVersionInfoAsync().Returns((DateTime.UtcNow, 1));
        var r = await Sut.ImportAsync(new(new() { dto })); Assert.Equal(1, r.Data!.Skills.BaselinesUpdated); Assert.Equal("Changed", e.Name);
    }

    [Fact]
    public async Task List_UTCID01_ColdBundle_QueriesDatabase()
    {
        _repo.GetAllAsync(true).Returns(new List<SkillEntity> { Entity() }); Assert.Single((await Sut.GetConfigBundleAsync()).Skills); Assert.Equal(1, _cache.FactoryCalls);
    }

    [Fact]
    public async Task List_UTCID02_WarmBundle_SkipsDatabase()
    {
        _cache.Seed("skill-bundle:all", new SkillConfigBundle("v", 0, new())); Assert.Equal("v", (await Sut.GetConfigBundleAsync()).Version); await _repo.DidNotReceive().GetAllAsync(true);
    }

    [Fact]
    public async Task List_UTCID03_EmptyBundle_HasZeroVersion()
    {
        _repo.GetAllAsync(true).Returns(new List<SkillEntity>()); Assert.Equal("0", (await Sut.GetConfigBundleAsync()).Version);
    }

    [Theory]
    [InlineData(3, "UTCID04")]
    [InlineData(0, "UTCID05")]
    public async Task List_Count_ReturnsVersionInfoCount(int count, string _)
    { _repo.GetVersionInfoAsync().Returns((null, count)); Assert.Equal(count, await Sut.CountAsync()); }

    [Fact]
    public async Task Edit_UTCID01_UpdatesAndInvalidates()
    {
        await AssertEdit(Update(), "Updated", 20);
    }

    [Fact]
    public async Task Edit_UTCID03_HtmlDescription_IsSanitized()
    {
        await AssertEdit(Update("<script>x</script><b>safe</b>"), "Updated", 20, true);
    }

    [Fact]
    public async Task Edit_UTCID04_NullDescription_RemainsNull()
    {
        await AssertEdit(Update(null), "Updated", 20);
    }

    [Fact]
    public async Task Edit_UTCID05_NegativeMana_IsStoredAsCurrentServiceAllows()
    {
        await AssertEdit(Update(mana: -1), "Updated", -1);
    }

    private async Task AssertEdit(SkillUpdateRequest request, string name, int mana, bool sanitized = false)
    {
        var e = Entity(); _repo.GetByIdAsync(e.SkillId, true).Returns(e); var r = await Sut.UpdateAsync(e.SkillId, request);
        Assert.True(r.Success); Assert.Equal(name, e.Name); Assert.Equal(mana, e.ManaCost); if (sanitized) Assert.DoesNotContain("script", e.Description!, StringComparison.OrdinalIgnoreCase);
        await _repo.Received(1).SaveChangesAsync(); Assert.Contains("skill-bundle:all", _cache.Removed);
    }

    [Theory]
    [InlineData("UTCID02")]
    [InlineData("UTCID06")]
    public async Task Edit_UnknownSkill_Fails(string _)
    { var r = await Sut.UpdateAsync("unknown_skill", Update()); Assert.False(r.Success); Assert.Contains("Sync it from Unity", r.Error!); }
}

internal sealed class Cache : ICacheService
{
    private readonly Dictionary<string, object> values = new(); internal int FactoryCalls { get; private set; }
    internal List<string> Removed { get; } = new();

    internal void Seed<T>(string k, T v) => values[k] = v!;

    public async Task<T> GetOrSetAsync<T>(string k, Func<Task<T>> f, TimeSpan? t = null, CancellationToken c = default)
    { if (values.TryGetValue(k, out var v)) return (T)v; FactoryCalls++; var r = await f(); values[k] = r!; return r; }

    public Task<T?> GetAsync<T>(string k, CancellationToken c = default) => Task.FromResult(values.TryGetValue(k, out var v) ? (T?)v : default);

    public Task SetAsync<T>(string k, T v, TimeSpan? t = null, CancellationToken c = default)
    { values[k] = v!; return Task.CompletedTask; }

    public Task RemoveAsync(string k, CancellationToken c = default)
    { Removed.Add(k); values.Remove(k); return Task.CompletedTask; }

    public Task RemoveByPrefixAsync(string k, CancellationToken c = default) => Task.CompletedTask;

    public Task<long?> IncrementAsync(string k, long b = 1, TimeSpan? t = null, CancellationToken c = default) => Task.FromResult<long?>(null);
}