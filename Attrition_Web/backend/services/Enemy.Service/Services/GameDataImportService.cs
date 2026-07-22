using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Enemy.Service.Data;
using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Enemy.Service.Services;

public interface IGameDataImportService { Task<ApiResponse<GameDataImportResult>> ImportAsync(GameDataImportRequest request); }

public class GameDataImportService(EnemyDbContext db, ICacheService cache) : IGameDataImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiResponse<GameDataImportResult>> ImportAsync(GameDataImportRequest request)
    {
        var counts = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            var itemCounts = await ImportItems(request.Items, now);
            var enemyCounts = await ImportEnemies(request.Enemies, now);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return (itemCounts, enemyCounts);
        });
        await cache.RemoveByPrefixAsync("list:"); await cache.RemoveAsync("bundle:all");
        await cache.RemoveByPrefixAsync("item-list:"); await cache.RemoveAsync("item-bundle:all");
        var (enemyMax, enemyCount) = await VersionInfo(db.Enemies.Select(x => x.UpdatedAt));
        var (itemMax, itemCount) = await VersionInfo(db.Items.Select(x => x.UpdatedAt));
        return ApiResponse<GameDataImportResult>.Ok(new(counts.itemCounts, counts.enemyCounts,
            Version(enemyMax, enemyCount), Version(itemMax, itemCount)));
    }

    private async Task<ImportCounts> ImportItems(List<UnityItemImport> input, DateTime now)
    {
        var ids = input.Select(x => x.ItemId).ToList();
        var existing = await db.Items.Include(x => x.Modifiers).Where(x => ids.Contains(x.ItemId))
            .ToDictionaryAsync(x => x.ItemId, StringComparer.Ordinal);
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var dto in input)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.ItemId, out var item))
            {
                item = new ItemEntity { ItemId = dto.ItemId, Name = dto.Name, Category = dto.Category,
                    Description = Clean(dto.Description), MaxStack = dto.MaxStack, IsKeyItem = dto.IsKeyItem,
                    ImageUrl = dto.ImageUrl, IconKey = dto.ItemId,
                    Modifiers = dto.Modifiers?.Select(x => new ItemModifierEntry { Stat = x.Stat, Amount = x.Amount }).ToList() ?? [],
                    UnityBaselineJson = baseline, ImportedAt = now, CreatedAt = now, UpdatedAt = now };
                db.Items.Add(item); created++;
            }
            else if (item.UnityBaselineJson != baseline)
            {
                var previous = Deserialize<UnityItemImport>(item.UnityBaselineJson);
                if (previous != null)
                {
                    item.Name = Merge(item.Name, previous.Name, dto.Name); item.Category = Merge(item.Category, previous.Category, dto.Category);
                    item.Description = Merge(item.Description, Clean(previous.Description), Clean(dto.Description));
                    item.MaxStack = Merge(item.MaxStack, previous.MaxStack, dto.MaxStack); item.IsKeyItem = Merge(item.IsKeyItem, previous.IsKeyItem, dto.IsKeyItem);
                    item.ImageUrl = Merge(item.ImageUrl, previous.ImageUrl, dto.ImageUrl);
                    if (ModifiersEqual(item.Modifiers, previous.Modifiers)) { item.Modifiers.Clear(); item.Modifiers.AddRange(dto.Modifiers?.Select(x => new ItemModifierEntry { Stat = x.Stat, Amount = x.Amount }) ?? []); }
                }
                else if (item.ImageUrl == null) item.ImageUrl = dto.ImageUrl;
                item.UnityBaselineJson = baseline; item.ImportedAt = now; item.UpdatedAt = now; updated++;
            }
            else unchanged++;
        }
        return new(created, updated, unchanged);
    }

    private async Task<ImportCounts> ImportEnemies(List<UnityEnemyImport> input, DateTime now)
    {
        var ids = input.Select(x => x.EnemyId).ToList();
        var existing = await db.Enemies.Where(x => ids.Contains(x.EnemyId)).ToDictionaryAsync(x => x.EnemyId, StringComparer.Ordinal);
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var dto in input)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.EnemyId, out var enemy))
            {
                enemy = new EnemyEntity { EnemyId = dto.EnemyId, Name = dto.Name, Tier = dto.Tier, Hp = dto.Hp, Ad = dto.Ad, Ap = dto.Ap, Def = dto.Def, Res = dto.Res,
                    Poise = dto.Poise, PoiseRecoveryTime = dto.PoiseRecoveryTime, PatrolSpeed = dto.PatrolSpeed, ChaseSpeed = dto.ChaseSpeed, AttackSpeed = dto.AttackSpeed,
                    ExpReward = dto.ExpReward, ImageUrl = dto.ImageUrl, UnityBaselineJson = baseline, ImportedAt = now, CreatedAt = now, UpdatedAt = now };
                db.Enemies.Add(enemy); created++;
            }
            else if (enemy.UnityBaselineJson != baseline)
            {
                var p = Deserialize<UnityEnemyImport>(enemy.UnityBaselineJson);
                if (p != null) { enemy.Name = Merge(enemy.Name,p.Name,dto.Name); enemy.Tier=Merge(enemy.Tier,p.Tier,dto.Tier); enemy.Hp=Merge(enemy.Hp,p.Hp,dto.Hp); enemy.Ad=Merge(enemy.Ad,p.Ad,dto.Ad); enemy.Ap=Merge(enemy.Ap,p.Ap,dto.Ap); enemy.Def=Merge(enemy.Def,p.Def,dto.Def); enemy.Res=Merge(enemy.Res,p.Res,dto.Res); enemy.Poise=Merge(enemy.Poise,p.Poise,dto.Poise); enemy.PoiseRecoveryTime=Merge(enemy.PoiseRecoveryTime,p.PoiseRecoveryTime,dto.PoiseRecoveryTime); enemy.PatrolSpeed=Merge(enemy.PatrolSpeed,p.PatrolSpeed,dto.PatrolSpeed); enemy.ChaseSpeed=Merge(enemy.ChaseSpeed,p.ChaseSpeed,dto.ChaseSpeed); enemy.AttackSpeed=Merge(enemy.AttackSpeed,p.AttackSpeed,dto.AttackSpeed); enemy.ExpReward=Merge(enemy.ExpReward,p.ExpReward,dto.ExpReward); enemy.ImageUrl=Merge(enemy.ImageUrl,p.ImageUrl,dto.ImageUrl); }
                else if (enemy.ImageUrl == null) enemy.ImageUrl = dto.ImageUrl;
                enemy.UnityBaselineJson = baseline; enemy.ImportedAt = now; enemy.UpdatedAt = now; updated++;
            }
            else unchanged++;
        }
        return new(created, updated, unchanged);
    }

    private static string? Clean(string? value) => value == null ? null : ContentSanitizer.Sanitize(value);
    private static T? Deserialize<T>(string? json) where T : class { if (string.IsNullOrEmpty(json)) return null; try { return JsonSerializer.Deserialize<T>(json, JsonOptions); } catch (JsonException) { return null; } }
    private static T Merge<T>(T current, T previous, T next) => EqualityComparer<T>.Default.Equals(current, previous) ? next : current;
    private static bool ModifiersEqual(List<ItemModifierEntry> current, List<ItemModifierDto>? previous) { previous ??= []; return current.Count == previous.Count && current.Zip(previous).All(x => x.First.Stat == x.Second.Stat && x.First.Amount == x.Second.Amount); }
    private static async Task<(DateTime? max, int count)> VersionInfo(IQueryable<DateTime> query) { var count = await query.CountAsync(); return count == 0 ? (null, 0) : (await query.MaxAsync(x => (DateTime?)x), count); }
    private static string Version(DateTime? max, int count) => max is null ? "0" : $"{max:O}|{count}";
}
