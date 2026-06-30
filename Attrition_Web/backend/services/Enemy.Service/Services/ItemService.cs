using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Enemy.Service.Repositories;

namespace Enemy.Service.Services;

public class ItemService : IItemService
{
    private readonly IItemRepository _repo;
    private readonly ICacheService _cache;

    public ItemService(IItemRepository repo, ICacheService cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<List<ItemResponse>> GetAllAsync(string? category, string? search)
    {
        var key = $"item-list:{category ?? "*"}:{search ?? "*"}";
        return await _cache.GetOrSetAsync(key, async () =>
        {
            var items = await _repo.GetAllWithModifiersAsync(category, search);
            return items.Select(ToResponse).ToList();
        }, TimeSpan.FromMinutes(10));
    }

    public async Task<ItemResponse?> GetByIdAsync(string itemId)
    {
        var item = await _repo.GetWithModifiersAsync(itemId);
        return item == null ? null : ToResponse(item);
    }

    private async Task InvalidateAsync()
    {
        await _cache.RemoveByPrefixAsync("item-list:");
        await _cache.RemoveAsync("item-bundle:all");
    }

    public async Task<ApiResponse<ItemResponse>> CreateAsync(ItemCreateRequest request)
    {
        var existing = await _repo.GetByIdAsync(request.ItemId);
        if (existing != null)
            return ApiResponse<ItemResponse>.Fail($"Item '{request.ItemId}' already exists.");

        var item = new ItemEntity
        {
            ItemId = request.ItemId,
            Name = request.Name,
            Category = request.Category,
            Rarity = request.Rarity,
            IconKey = request.IconKey,
            Description = request.Description is null ? null : ContentSanitizer.Sanitize(request.Description),
            MaxStack = request.MaxStack,
            IsKeyItem = request.IsKeyItem,
            Modifiers = MapModifiers(request.Modifiers)
        };

        if (!await _repo.TryAddAsync(item))
            return ApiResponse<ItemResponse>.Fail($"Item '{request.ItemId}' already exists.");
        await InvalidateAsync();
        return ApiResponse<ItemResponse>.Ok(ToResponse(item));
    }

    public async Task<ApiResponse<ItemResponse>> UpdateAsync(string itemId, ItemUpdateRequest request)
    {
        var item = await _repo.GetWithModifiersAsync(itemId);
        if (item == null) return ApiResponse<ItemResponse>.Fail("Item not found.");

        item.Name = request.Name;
        item.Category = request.Category;
        item.Rarity = request.Rarity;
        item.IconKey = request.IconKey;
        item.Description = request.Description is null ? null : ContentSanitizer.Sanitize(request.Description);
        item.MaxStack = request.MaxStack;
        item.IsKeyItem = request.IsKeyItem;
        item.UpdatedAt = DateTime.UtcNow;

        if (request.Modifiers != null)
        {
            item.Modifiers.Clear();
            item.Modifiers.AddRange(MapModifiers(request.Modifiers));
        }

        await _repo.SaveTrackedAsync();
        await InvalidateAsync();
        return ApiResponse<ItemResponse>.Ok(ToResponse(item));
    }

    public async Task<ApiResponse> DeleteAsync(string itemId)
    {
        var item = await _repo.GetByIdAsync(itemId);
        if (item == null) return ApiResponse.Fail("Item not found.");
        await _repo.DeleteAsync(item);
        await InvalidateAsync();
        return ApiResponse.Ok();
    }

    public async Task<ItemConfigBundle> GetConfigBundleAsync()
    {
        return await _cache.GetOrSetAsync("item-bundle:all", async () =>
        {
            var (max, count) = await _repo.GetVersionInfoAsync();
            var items = await _repo.GetAllForBundleAsync();
            var version = BuildVersion(max, count);
            return new ItemConfigBundle(version, items.Count, items.Select(ToResponse).ToList());
        }, TimeSpan.FromMinutes(10));
    }

    public async Task<(string version, int count)> GetVersionInfoAsync()
    {
        var (max, count) = await _repo.GetVersionInfoAsync();
        return (BuildVersion(max, count), count);
    }

    // Version = MAX(UpdatedAt) ISO-8601 + count (giống enemy). Count vào version để xoá item
    // cũng đổi version. Bảng rỗng → "0".
    private static string BuildVersion(DateTime? maxUpdatedAt, int count) =>
        maxUpdatedAt is null ? "0" : $"{maxUpdatedAt:O}|{count}";

    private static List<ItemModifierEntry> MapModifiers(List<ItemModifierDto>? mods) =>
        mods?.Select(m => new ItemModifierEntry { Stat = m.Stat, Amount = m.Amount }).ToList() ?? new();

    private static ItemResponse ToResponse(ItemEntity i) => new(
        i.ItemId, i.Name, i.Category, i.Rarity, i.IconKey, i.Description,
        i.MaxStack, i.IsKeyItem, i.CreatedAt, i.UpdatedAt,
        i.Modifiers.Select(m => new ItemModifierDto(m.Stat, m.Amount)).ToList());
}
