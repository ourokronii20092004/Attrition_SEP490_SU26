using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Enemy.Service.Repositories;

namespace Enemy.Service.Services;

public class EnemyService : IEnemyService
{
    private readonly IEnemyRepository _repo;
    private readonly ICacheService _cache;

    public EnemyService(IEnemyRepository repo, ICacheService cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<List<EnemyResponse>> GetAllAsync(string? tier, string? search)
    {
        // Bestiary listings are read-heavy and change only via admin edits.
        var key = $"list:{tier ?? "*"}:{search ?? "*"}";
        return await _cache.GetOrSetAsync(key, async () =>
        {
            var enemies = await _repo.GetAllWithLootAsync(tier, search);
            return enemies.Select(ToResponse).ToList();
        }, TimeSpan.FromMinutes(10));
    }

    public async Task<EnemyResponse?> GetByIdAsync(string enemyId)
    {
        var enemy = await _repo.GetWithLootAsync(enemyId);
        return enemy == null ? null : ToResponse(enemy);
    }

    private Task InvalidateAsync() => _cache.RemoveByPrefixAsync("list:");

    public async Task<ApiResponse<EnemyResponse>> CreateAsync(EnemyCreateRequest request)
    {
        var existing = await _repo.GetByIdAsync(request.EnemyId);
        if (existing != null)
            return ApiResponse<EnemyResponse>.Fail($"Enemy '{request.EnemyId}' already exists.");

        var enemy = new EnemyEntity
        {
            EnemyId = request.EnemyId,
            Name = request.Name,
            Tier = request.Tier,
            SpawnBiome = request.SpawnBiome,
            Hp = request.Hp,
            Ad = request.Ad,
            Ap = request.Ap,
            Def = request.Def,
            Res = request.Res,
            AttackSpeed = request.AttackSpeed,
            IsRanged = request.IsRanged,
            ExpReward = request.ExpReward,
            GoldReward = request.GoldReward,
            Lore = request.Lore,
            LootTable = MapLoot(request.LootTable)
        };

        // Optimistic check above for a friendly message; TryAddAsync makes the insert race-safe so a
        // concurrent duplicate hits the PK constraint and returns the same message instead of a 500.
        if (!await _repo.TryAddAsync(enemy))
            return ApiResponse<EnemyResponse>.Fail($"Enemy '{request.EnemyId}' already exists.");
        await InvalidateAsync();
        return ApiResponse<EnemyResponse>.Ok(ToResponse(enemy));
    }

    public async Task<ApiResponse<EnemyResponse>> UpdateAsync(string enemyId, EnemyUpdateRequest request)
    {
        var enemy = await _repo.GetWithLootAsync(enemyId);
        if (enemy == null) return ApiResponse<EnemyResponse>.Fail("Enemy not found.");

        enemy.Name = request.Name;
        enemy.Tier = request.Tier;
        enemy.SpawnBiome = request.SpawnBiome;
        enemy.Hp = request.Hp;
        enemy.Ad = request.Ad;
        enemy.Ap = request.Ap;
        enemy.Def = request.Def;
        enemy.Res = request.Res;
        enemy.AttackSpeed = request.AttackSpeed;
        enemy.IsRanged = request.IsRanged;
        enemy.ExpReward = request.ExpReward;
        enemy.GoldReward = request.GoldReward;
        enemy.Lore = request.Lore;
        enemy.UpdatedAt = DateTime.UtcNow;

        // Replace loot wholesale — owned collection, so clearing + re-adding is the clean path.
        if (request.LootTable != null)
        {
            enemy.LootTable.Clear();
            enemy.LootTable.AddRange(MapLoot(request.LootTable));
        }

        // Save the tracked graph so EF emits the owned-loot deletes/inserts itself.
        // (Routing through the generic UpdateAsync would re-mark the root Modified and
        // not reliably diff the owned collection.)
        await _repo.SaveTrackedAsync();
        await InvalidateAsync();
        return ApiResponse<EnemyResponse>.Ok(ToResponse(enemy));
    }

    public async Task<ApiResponse> DeleteAsync(string enemyId)
    {
        var enemy = await _repo.GetByIdAsync(enemyId);
        if (enemy == null) return ApiResponse.Fail("Enemy not found.");
        await _repo.DeleteAsync(enemy);
        await InvalidateAsync();
        return ApiResponse.Ok();
    }

    public async Task<List<EnemySummaryDto>> SearchAsync(string query, int limit)
    {
        var enemies = await _repo.SearchAsync(query, limit);
        return enemies.Select(e => new EnemySummaryDto(e.EnemyId, e.Name, e.Tier)).ToList();
    }

    public Task<int> CountAsync() => _repo.CountAsync();

    private static List<EnemyLootEntry> MapLoot(List<LootEntryDto>? loot) =>
        loot?.Select(l => new EnemyLootEntry
        {
            ItemName = l.ItemName,
            Rarity = l.Rarity,
            IconKey = l.IconKey,
            DropChance = l.DropChance,
            MinQty = l.MinQty,
            MaxQty = l.MaxQty
        }).ToList() ?? new();

    private static EnemyResponse ToResponse(EnemyEntity e) => new(
        e.EnemyId, e.Name, e.Tier, e.SpawnBiome, e.Hp, e.Ad, e.Ap, e.Def, e.Res,
        e.AttackSpeed, e.IsRanged, e.ExpReward, e.GoldReward, e.Lore, e.CreatedAt, e.UpdatedAt,
        e.LootTable.Select(l => new LootEntryDto(l.ItemName, l.Rarity, l.IconKey, l.DropChance, l.MinQty, l.MaxQty)).ToList());
}
