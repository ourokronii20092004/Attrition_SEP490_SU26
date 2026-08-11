using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Enemy.Service.Repositories.Interface;
using Enemy.Service.Services;
using NSubstitute;

namespace Enemy.Service.Tests;

public class EnemyServiceTests
{
    private readonly IEnemyRepository _repo = Substitute.For<IEnemyRepository>();
    private readonly TestCache _cache = new();
    private EnemyService Sut => new(_repo, _cache);

    private static EnemyEntity Enemy(string id = "armored_crab", bool loot = true) => new()
    {
        EnemyId = id,
        Name = "Armored Crab",
        Tier = "Elite",
        Hp = 100,
        Ad = 20,
        Def = 10,
        AttackSpeed = 1,
        LootTable = loot ? new() { new() { ItemName = "Shell", DropChance = .5f, MinQty = 1, MaxQty = 2 } } : new()
    };

    private static EnemyCreateRequest Create(string id = "stone_golem", List<LootEntryDto>? loot = null) =>
        new(id, "Stone Golem", "Elite", "Cave", 100, 20, 0, 10, 5, 1, false, 10, 5, "lore", loot);

    private static EnemyUpdateRequest Update(List<LootEntryDto>? loot = null, int hp = 200) =>
        new("Updated", "Boss", "Cave", hp, 25, 0, 12, 6, 1, false, 20, 10, "lore", loot);

    [Theory]
    [InlineData(null, null, "UTCID01")]
    [InlineData("Elite", null, "UTCID02")]
    [InlineData(null, "crab", "UTCID03")]
    public async Task Browse_GetAll_ForwardsFiltersAndMapsLoot(string? tier, string? search, string _)
    {
        _repo.GetAllWithLootAsync(tier, search).Returns(new List<EnemyEntity> { Enemy() });
        var result = await Sut.GetAllAsync(tier, search);
        Assert.Single(result);
        Assert.Single(result[0].LootTable);
    }

    [Fact]
    public async Task Browse_UTCID04_GetExistingById_ReturnsStatsAndLoot()
    {
        _repo.GetWithLootAsync("armored_crab").Returns(Enemy());
        var result = await Sut.GetByIdAsync("armored_crab");
        Assert.NotNull(result);
        Assert.Equal(100, result.Hp);
        Assert.Single(result.LootTable);
    }

    [Fact]
    public async Task Browse_UTCID05_UnknownId_ReturnsNull() => Assert.Null(await Sut.GetByIdAsync("ghost"));

    [Theory]
    [InlineData("UTCID06")]
    [InlineData("UTCID07")]
    public async Task Browse_EmptyLoot_IsMappedAsEmptyList(string caseId)
    {
        if (caseId == "UTCID06")
        {
            _repo.GetWithLootAsync("armored_crab").Returns(Enemy(loot: false));
            Assert.Empty((await Sut.GetByIdAsync("armored_crab"))!.LootTable);
        }
        else
        {
            _repo.GetAllWithLootAsync(null, null).Returns(new List<EnemyEntity> { Enemy(loot: false) });
            Assert.Empty((await Sut.GetAllAsync(null, null))[0].LootTable);
        }
    }

    [Fact]
    public async Task Add_UTCID01_FreeId_CreatesAndInvalidatesCaches()
    {
        EnemyEntity? added = null;
        _repo.TryAddAsync(Arg.Do<EnemyEntity>(x => added = x)).Returns(true);
        var result = await Sut.CreateAsync(Create());
        Assert.True(result.Success);
        Assert.Equal("stone_golem", added!.EnemyId);
        Assert.Contains("list:", _cache.Removed);
        Assert.Contains("bundle:all", _cache.Removed);
    }

    [Fact]
    public async Task Add_UTCID02_ExistingId_FailsBeforeInsert()
    {
        _repo.GetByIdAsync("armored_crab").Returns(Enemy());
        var result = await Sut.CreateAsync(Create("armored_crab"));
        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error!);
    }

    [Fact]
    public async Task Add_UTCID03_EmptyId_IsRejectedByValidator()
    {
        var result = await new Enemy.Service.Validators.EnemyCreateRequestValidator().ValidateAsync(Create(""));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EnemyId");
    }

    [Fact]
    public async Task Add_UTCID04_LootRows_AreMapped()
    {
        EnemyEntity? added = null;
        _repo.TryAddAsync(Arg.Do<EnemyEntity>(x => added = x)).Returns(true);
        await Sut.CreateAsync(Create(loot: new() { new("Shell", "Rare", null, .5f, 1, 2), new("Gold", "Common", null, 1, 1, 3) }));
        Assert.Equal(2, added!.LootTable.Count);
    }

    [Theory]
    [InlineData("UTCID05")]
    [InlineData("UTCID06")]
    public async Task Add_ConcurrentDuplicate_ReturnsFriendlyFailure(string _)
    {
        _repo.TryAddAsync(Arg.Any<EnemyEntity>()).Returns(false);
        var result = await Sut.CreateAsync(Create(loot: _ == "UTCID06" ? new() { new("Shell", "Rare", null, .5f, 1, 2) } : null));
        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error!);
    }

    [Theory]
    [InlineData("crab", 10, 2, "UTCID01")]
    [InlineData("crab", 1, 1, "UTCID02")]
    [InlineData("crab", 0, 0, "UTCID03")]
    [InlineData("", 10, 0, "UTCID04")]
    [InlineData("zzz", 10, 0, "UTCID05")]
    public async Task AdminList_Search_ReturnsRepositorySummaries(string query, int limit, int count, string _)
    {
        _repo.SearchAsync(query, limit).Returns(Enumerable.Range(0, count).Select(i => Enemy($"e{i}")).ToList());
        Assert.Equal(count, (await Sut.SearchAsync(query, limit)).Count);
    }

    [Fact]
    public async Task AdminList_UTCID06_Count_IsForwarded()
    {
        _repo.CountAsync().Returns(7);
        Assert.Equal(7, await Sut.CountAsync());
    }

    [Fact]
    public async Task AdminList_UTCID07_Stats_AreForwarded()
    {
        _repo.GetStatsAsync().Returns((7, 12));
        Assert.Equal((7, 12), await Sut.GetStatsAsync());
    }

    [Fact]
    public async Task Edit_UTCID01_NullLoot_KeepsRowsAndUpdatesStats()
    {
        await AssertEdit(null, 1);
    }

    [Fact]
    public async Task Edit_UTCID02_NewLoot_ReplacesRows()
    {
        await AssertEdit(new() { new("Gold", "Common", null, 1, 1, 2) }, 1, "Gold");
    }

    [Fact]
    public async Task Edit_UTCID03_EmptyLoot_ClearsRows()
    {
        await AssertEdit(new(), 0);
    }

    private async Task AssertEdit(List<LootEntryDto>? loot, int count, string? first = null)
    {
        var enemy = Enemy();
        _repo.GetWithLootAsync(enemy.EnemyId).Returns(enemy);
        var before = enemy.UpdatedAt;

        var result = await Sut.UpdateAsync(enemy.EnemyId, Update(loot));
        Assert.True(result.Success);
        Assert.Equal(200, enemy.Hp);
        Assert.Equal(count, enemy.LootTable.Count);
        if (first != null)
        {
            Assert.Equal(first, enemy.LootTable[0].ItemName);
        }
        Assert.True(enemy.UpdatedAt >= before);
        await _repo.Received(1).SaveTrackedAsync();
        Assert.Contains("list:", _cache.Removed);
    }

    [Theory]
    [InlineData("UTCID04", false)]
    [InlineData("UTCID06", true)]
    public async Task Edit_UnknownEnemy_Fails(string _, bool withLoot)
    {
        var result = await Sut.UpdateAsync("ghost", Update(withLoot ? new() { new("Gold", "Common", null, 1, 1, 1) } : null));
        Assert.False(result.Success); Assert.Equal("Enemy not found.", result.Error);
    }

    [Fact]
    public async Task Edit_UTCID05_NegativeStats_AreRejectedByCurrentValidator()
    {
        // Workbook says the game clamps negatives, but the current API trust boundary intentionally rejects them.
        var result = await new Enemy.Service.Validators.EnemyUpdateRequestValidator().ValidateAsync(Update(hp: -1));
        Assert.False(result.IsValid); Assert.Contains(result.Errors, e => e.PropertyName == "Hp");
    }

    [Theory]
    [InlineData("UTCID01", true)]
    [InlineData("UTCID02", false)]
    public async Task Delete_ExistingEnemy_DeletesGraphAndInvalidates(string _, bool loot)
    {
        var enemy = Enemy(loot: loot); _repo.GetByIdAsync(enemy.EnemyId).Returns(enemy);
        Assert.True((await Sut.DeleteAsync(enemy.EnemyId)).Success); await _repo.Received(1).DeleteAsync(enemy);
        Assert.Contains("bundle:all", _cache.Removed);
    }

    [Theory]
    [InlineData("ghost", "UTCID03")]
    [InlineData("", "UTCID04")]
    public async Task Delete_UnknownEnemy_Fails(string id, string _)
    {
        var result = await Sut.DeleteAsync(id); Assert.False(result.Success); Assert.Equal("Enemy not found.", result.Error);
    }
}