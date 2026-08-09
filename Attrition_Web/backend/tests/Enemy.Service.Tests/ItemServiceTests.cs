using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Enemy.Service.Repositories.Interface;
using Enemy.Service.Services;
using NSubstitute;

namespace Enemy.Service.Tests;

public class ItemServiceTests
{
    private readonly IItemRepository _repo = Substitute.For<IItemRepository>();
    private readonly TestCache _cache = new();
    private ItemService Sut => new(_repo, _cache);

    private static ItemEntity Item(string id = "iron_helm") => new()
    {
        ItemId = id,
        Name = "Iron Helm",
        Category = "Equipment",
        Rarity = "Common",
        MaxStack = 1,
        Modifiers = new() { new() { Stat = "DEF", Amount = 5 }, new() { Stat = "RES", Amount = 2 } }
    };

    private static ItemCreateRequest Create(string id = "steel_helm", string? description = "plain", List<ItemModifierDto>? mods = null) =>
        new(id, "Steel Helm", "Equipment", "Rare", null, description, 1, false, mods);

    private static ItemUpdateRequest Update(string? description = "plain", List<ItemModifierDto>? mods = null) =>
        new("Updated Helm", "Equipment", "Rare", null, description, 1, false, mods);

    [Theory]
    [InlineData(null, null, "UTCID01")]
    [InlineData(null, "leather", "UTCID02")]
    [InlineData(null, "LEATHER", "UTCID03")]
    [InlineData(null, "zzqx", "UTCID04")]
    [InlineData("Elite", null, "UTCID05")]
    [InlineData("Elite", "leather", "UTCID06")]
    [InlineData("Unknown", null, "UTCID07")]
    public async Task Browse_ForwardsFiltersAndMapsRows(string? category, string? search, string _)
    {
        var rows = search == "zzqx" || category == "Unknown" ? new List<ItemEntity>() : new() { Item() };
        _repo.GetAllWithModifiersAsync(category, search).Returns(rows);
        var result = await Sut.GetAllAsync(category, search);
        Assert.Equal(rows.Count, result.Count); await _repo.Received(1).GetAllWithModifiersAsync(category, search);
        if (result.Count > 0) Assert.Equal(2, result[0].Modifiers.Count);
    }

    [Fact]
    public async Task Browse_UTCID08_WarmCache_DoesNotQueryRepository()
    {
        _cache.Seed("item-list:*:*", new List<ItemResponse>());
        Assert.Empty(await Sut.GetAllAsync(null, null));
        await _repo.DidNotReceiveWithAnyArgs().GetAllWithModifiersAsync(default, default);
    }

    [Fact]
    public async Task Add_UTCID01_FreeId_CreatesAndInvalidatesCaches()
    {
        ItemEntity? inserted = null; _repo.TryAddAsync(Arg.Do<ItemEntity>(x => inserted = x)).Returns(true);
        var result = await Sut.CreateAsync(Create());
        Assert.True(result.Success); Assert.Equal("steel_helm", inserted!.ItemId);
        Assert.Contains("item-list:", _cache.Removed); Assert.Contains("item-bundle:all", _cache.Removed);
    }

    [Theory]
    [InlineData("UTCID02")]
    [InlineData("UTCID07")]
    public async Task Add_TakenId_FailsBeforeInsert(string _)
    {
        _repo.GetByIdAsync("iron_helm").Returns(Item());
        var result = await Sut.CreateAsync(Create("iron_helm", "<b>text</b>", new() { new("DEF", 2) }));
        Assert.False(result.Success); Assert.Contains("already exists", result.Error!);
        await _repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!);
    }

    [Fact]
    public async Task Add_UTCID03_EmptyId_IsRejectedByValidator()
    {
        var result = await new Enemy.Service.Validators.ItemCreateRequestValidator().ValidateAsync(Create(""));
        Assert.False(result.IsValid); Assert.Contains(result.Errors, e => e.PropertyName == "ItemId");
    }

    [Fact]
    public async Task Add_UTCID04_HtmlDescription_IsSanitized()
    {
        ItemEntity? inserted = null; _repo.TryAddAsync(Arg.Do<ItemEntity>(x => inserted = x)).Returns(true);
        Assert.True((await Sut.CreateAsync(Create(description: "<script>x</script><b>safe</b>"))).Success);
        Assert.DoesNotContain("script", inserted!.Description!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Add_UTCID05_NullDescription_IsPreserved()
    {
        ItemEntity? inserted = null; _repo.TryAddAsync(Arg.Do<ItemEntity>(x => inserted = x)).Returns(true);
        await Sut.CreateAsync(Create(description: null)); Assert.Null(inserted!.Description);
    }

    [Fact]
    public async Task Add_UTCID06_Modifiers_AreMapped()
    {
        ItemEntity? inserted = null; _repo.TryAddAsync(Arg.Do<ItemEntity>(x => inserted = x)).Returns(true);
        await Sut.CreateAsync(Create(mods: new() { new("DEF", 5), new("RES", 2) }));
        Assert.Equal(2, inserted!.Modifiers.Count); Assert.Equal(5, inserted.Modifiers[0].Amount);
    }

    [Fact]
    public async Task Edit_UTCID01_NullModifiers_KeepsExistingList() => await AssertEdit(null, 2);

    [Fact]
    public async Task Edit_UTCID02_NewModifierList_ReplacesExisting() => await AssertEdit(new() { new("AD", 9) }, 1);

    [Fact]
    public async Task Edit_UTCID03_EmptyModifierList_ClearsExisting() => await AssertEdit(new(), 0);

    private async Task AssertEdit(List<ItemModifierDto>? mods, int expected)
    {
        var item = Item(); _repo.GetWithModifiersAsync(item.ItemId).Returns(item);
        var result = await Sut.UpdateAsync(item.ItemId, Update(mods: mods));
        Assert.True(result.Success); Assert.Equal(expected, item.Modifiers.Count);
        await _repo.Received(1).SaveTrackedAsync(); Assert.Contains("item-list:", _cache.Removed);
    }

    [Fact]
    public async Task Edit_UTCID04_HtmlDescription_IsSanitized()
    {
        var item = Item(); _repo.GetWithModifiersAsync(item.ItemId).Returns(item);
        await Sut.UpdateAsync(item.ItemId, Update("<script>x</script><b>safe</b>"));
        Assert.DoesNotContain("script", item.Description!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UTCID05", false)]
    [InlineData("UTCID06", true)]
    public async Task Edit_UnknownItem_Fails(string _, bool withMods)
    {
        var result = await Sut.UpdateAsync("ghost_item", Update(mods: withMods ? new() { new("AD", 1) } : null));
        Assert.False(result.Success); Assert.Equal("Item not found.", result.Error);
    }

    [Theory]
    [InlineData("iron_helm", "UTCID01")]
    [InlineData("iron_helm", "UTCID02")]
    public async Task Delete_ExistingItem_DeletesAndInvalidates(string id, string _)
    {
        var item = Item(id); _repo.GetByIdAsync(id).Returns(item);
        Assert.True((await Sut.DeleteAsync(id)).Success); await _repo.Received(1).DeleteAsync(item);
        Assert.Contains("item-list:", _cache.Removed);
    }

    [Theory]
    [InlineData("ghost_item", "UTCID03")]
    [InlineData("", "UTCID04")]
    public async Task Delete_UnknownItem_Fails(string id, string _)
    {
        var result = await Sut.DeleteAsync(id); Assert.False(result.Success); Assert.Equal("Item not found.", result.Error);
    }
}