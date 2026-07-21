using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Enemy.Service.Data;
using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Enemy.Service.Services;

public interface IGameDataImportService
{
    Task<ApiResponse<GameDataImportResult>> ImportAsync(GameDataImportRequest request);
}

public class GameDataImportService : IGameDataImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EnemyDbContext _db;
    private readonly ICacheService _cache;

    public GameDataImportService(EnemyDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ApiResponse<GameDataImportResult>> ImportAsync(GameDataImportRequest request)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        var counts = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            var itemCounts = await ImportItems(request.Items, now);
            var enemyCounts = await ImportEnemies(request.Enemies, now);
            var skillCounts = await ImportSkills(request.Skills, now);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (itemCounts, enemyCounts, skillCounts);
        });

        await InvalidateAsync();
        var (enemyMax, enemyCount) = await VersionInfo(_db.Enemies.Select(x => x.UpdatedAt));
        var (itemMax, itemCount) = await VersionInfo(_db.Items.Select(x => x.UpdatedAt));
        var (skillMax, skillCount) = await VersionInfo(_db.Skills.Select(x => x.UpdatedAt));
        return ApiResponse<GameDataImportResult>.Ok(new GameDataImportResult(
            counts.itemCounts, counts.enemyCounts, counts.skillCounts,
            Version(enemyMax, enemyCount), Version(itemMax, itemCount), Version(skillMax, skillCount)));
    }

    private async Task<ImportCounts> ImportItems(List<UnityItemImport> input, DateTime now)
    {
        var existing = await _db.Items.Include(x => x.Modifiers)
            .Where(x => input.Select(i => i.ItemId).Contains(x.ItemId))
            .ToDictionaryAsync(x => x.ItemId, StringComparer.Ordinal);
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var dto in input)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.ItemId, out var item))
            {
                item = new ItemEntity
                {
                    ItemId = dto.ItemId,
                    Name = dto.Name,
                    Category = dto.Category,
                    Rarity = "Common",
                    Description = dto.Description is null ? null : ContentSanitizer.Sanitize(dto.Description),
                    MaxStack = dto.MaxStack,
                    IsKeyItem = dto.IsKeyItem,
                    ImageUrl = dto.ImageUrl,
                    IconKey = dto.ItemId,
                    Modifiers = dto.Modifiers?.Select(x => new ItemModifierEntry { Stat = x.Stat, Amount = x.Amount }).ToList() ?? new(),
                    UnityBaselineJson = baseline,
                    ImportedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Items.Add(item);
                created++;
            }
            else if (item.UnityBaselineJson != baseline)
            {
                var previous = Deserialize<UnityItemImport>(item.UnityBaselineJson);
                if (previous != null)
                {
                    item.Name = Merge(item.Name, previous.Name, dto.Name);
                    item.Category = Merge(item.Category, previous.Category, dto.Category);
                    item.Description = Merge(item.Description,
                        previous.Description is null ? null : ContentSanitizer.Sanitize(previous.Description),
                        dto.Description is null ? null : ContentSanitizer.Sanitize(dto.Description));
                    item.MaxStack = Merge(item.MaxStack, previous.MaxStack, dto.MaxStack);
                    item.IsKeyItem = Merge(item.IsKeyItem, previous.IsKeyItem, dto.IsKeyItem);
                    item.ImageUrl = Merge(item.ImageUrl, previous.ImageUrl, dto.ImageUrl);
                    if (ModifiersEqual(item.Modifiers, previous.Modifiers))
                    {
                        item.Modifiers.Clear();
                        item.Modifiers.AddRange(dto.Modifiers?.Select(x => new ItemModifierEntry { Stat = x.Stat, Amount = x.Amount }) ?? []);
                    }
                }
                else if (item.ImageUrl == null) item.ImageUrl = dto.ImageUrl;
                item.UnityBaselineJson = baseline;
                item.ImportedAt = now;
                item.UpdatedAt = now;
                updated++;
            }
            else unchanged++;
        }
        return new ImportCounts(created, updated, unchanged);
    }

    private async Task<ImportCounts> ImportEnemies(List<UnityEnemyImport> input, DateTime now)
    {
        var ids = input.Select(i => i.EnemyId).ToList();
        var existing = await _db.Enemies.Where(x => ids.Contains(x.EnemyId))
            .ToDictionaryAsync(x => x.EnemyId, StringComparer.Ordinal);
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var dto in input)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.EnemyId, out var enemy))
            {
                enemy = new EnemyEntity
                {
                    EnemyId = dto.EnemyId, Name = dto.Name, Tier = dto.Tier, Hp = dto.Hp,
                    Ad = dto.Ad, Ap = dto.Ap, Def = dto.Def, Res = dto.Res, Poise = dto.Poise,
                    PoiseRecoveryTime = dto.PoiseRecoveryTime, PatrolSpeed = dto.PatrolSpeed,
                    ChaseSpeed = dto.ChaseSpeed, AttackSpeed = dto.AttackSpeed,
                    ExpReward = dto.ExpReward, ImageUrl = dto.ImageUrl,
                    UnityBaselineJson = baseline, ImportedAt = now, CreatedAt = now, UpdatedAt = now
                };
                _db.Enemies.Add(enemy);
                created++;
            }
            else if (enemy.UnityBaselineJson != baseline)
            {
                var previous = Deserialize<UnityEnemyImport>(enemy.UnityBaselineJson);
                if (previous == null && enemy.UnityBaselineJson == null)
                {
                    enemy.Poise = dto.Poise;
                    enemy.PoiseRecoveryTime = dto.PoiseRecoveryTime;
                    enemy.PatrolSpeed = dto.PatrolSpeed;
                    enemy.ChaseSpeed = dto.ChaseSpeed;
                }
                if (previous != null)
                {
                    enemy.Name = Merge(enemy.Name, previous.Name, dto.Name);
                    enemy.Tier = Merge(enemy.Tier, previous.Tier, dto.Tier);
                    enemy.Hp = Merge(enemy.Hp, previous.Hp, dto.Hp);
                    enemy.Ad = Merge(enemy.Ad, previous.Ad, dto.Ad);
                    enemy.Ap = Merge(enemy.Ap, previous.Ap, dto.Ap);
                    enemy.Def = Merge(enemy.Def, previous.Def, dto.Def);
                    enemy.Res = Merge(enemy.Res, previous.Res, dto.Res);
                    enemy.Poise = Merge(enemy.Poise, previous.Poise, dto.Poise);
                    enemy.PoiseRecoveryTime = Merge(enemy.PoiseRecoveryTime, previous.PoiseRecoveryTime, dto.PoiseRecoveryTime);
                    enemy.PatrolSpeed = Merge(enemy.PatrolSpeed, previous.PatrolSpeed, dto.PatrolSpeed);
                    enemy.ChaseSpeed = Merge(enemy.ChaseSpeed, previous.ChaseSpeed, dto.ChaseSpeed);
                    enemy.AttackSpeed = Merge(enemy.AttackSpeed, previous.AttackSpeed, dto.AttackSpeed);
                    enemy.ExpReward = Merge(enemy.ExpReward, previous.ExpReward, dto.ExpReward);
                    enemy.ImageUrl = Merge(enemy.ImageUrl, previous.ImageUrl, dto.ImageUrl);
                }
                else if (enemy.ImageUrl == null) enemy.ImageUrl = dto.ImageUrl;
                enemy.UnityBaselineJson = baseline;
                enemy.ImportedAt = now;
                enemy.UpdatedAt = now;
                updated++;
            }
            else unchanged++;
        }
        return new ImportCounts(created, updated, unchanged);
    }

    private async Task<ImportCounts> ImportSkills(List<SkillConfigDto> input, DateTime now)
    {
        var ids = input.Select(i => i.SkillId).ToList();
        var existing = await _db.Skills.Where(x => ids.Contains(x.SkillId))
            .ToDictionaryAsync(x => x.SkillId, StringComparer.Ordinal);
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var dto in input)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.SkillId, out var skill))
            {
                skill = new SkillEntity { SkillId = dto.SkillId, CreatedAt = now, UpdatedAt = now };
                SkillService.Apply(skill, dto);
                skill.UnityBaselineJson = baseline;
                skill.ImportedAt = now;
                _db.Skills.Add(skill);
                created++;
            }
            else if (skill.UnityBaselineJson != baseline)
            {
                var previous = Deserialize<SkillConfigDto>(skill.UnityBaselineJson);
                if (previous != null) MergeSkill(skill, previous, dto);
                else if (skill.ImageUrl == null) skill.ImageUrl = dto.ImageUrl;
                skill.UnityBaselineJson = baseline;
                skill.ImportedAt = now;
                skill.UpdatedAt = now;
                updated++;
            }
            else unchanged++;
        }
        return new ImportCounts(created, updated, unchanged);
    }

    private async Task InvalidateAsync()
    {
        await _cache.RemoveByPrefixAsync("list:");
        await _cache.RemoveAsync("bundle:all");
        await _cache.RemoveByPrefixAsync("item-list:");
        await _cache.RemoveAsync("item-bundle:all");
        await _cache.RemoveAsync("skill-bundle:all");
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static T Merge<T>(T current, T previous, T next) =>
        EqualityComparer<T>.Default.Equals(current, previous) ? next : current;

    private static bool ModifiersEqual(List<ItemModifierEntry> current, List<ItemModifierDto>? previous)
    {
        previous ??= [];
        return current.Count == previous.Count && current.Zip(previous)
            .All(x => x.First.Stat == x.Second.Stat && x.First.Amount == x.Second.Amount);
    }

    private static void MergeSkill(SkillEntity skill, SkillConfigDto previous, SkillConfigDto next)
    {
        skill.Element = Merge(skill.Element, previous.Element, next.Element);
        skill.ManaCost = Merge(skill.ManaCost, previous.ManaCost, next.ManaCost);
        skill.CastTime = Merge(skill.CastTime, previous.CastTime, next.CastTime);
        skill.Cooldown = Merge(skill.Cooldown, previous.Cooldown, next.Cooldown);
        skill.ActiveStartFrac = Merge(skill.ActiveStartFrac, previous.ActiveStartFrac, next.ActiveStartFrac);
        skill.ActiveEndFrac = Merge(skill.ActiveEndFrac, previous.ActiveEndFrac, next.ActiveEndFrac);
        skill.DamageType = Merge(skill.DamageType, previous.DamageType, next.DamageType);
        skill.BaseDamage = Merge(skill.BaseDamage, previous.BaseDamage, next.BaseDamage);
        skill.ApScaling = Merge(skill.ApScaling, previous.ApScaling, next.ApScaling);
        skill.KnockbackForce = Merge(skill.KnockbackForce, previous.KnockbackForce, next.KnockbackForce);
        skill.TickInterval = Merge(skill.TickInterval, previous.TickInterval, next.TickInterval);
        skill.SweetSpotRadius = Merge(skill.SweetSpotRadius, previous.SweetSpotRadius, next.SweetSpotRadius);
        skill.SweetSpotMultiplier = Merge(skill.SweetSpotMultiplier, previous.SweetSpotMultiplier, next.SweetSpotMultiplier);
        skill.Delivery = Merge(skill.Delivery, previous.Delivery, next.Delivery);
        skill.HitShape = Merge(skill.HitShape, previous.HitShape, next.HitShape);
        skill.Range = Merge(skill.Range, previous.Range, next.Range);
        skill.Angle = Merge(skill.Angle, previous.Angle, next.Angle);
        skill.RectWidth = Merge(skill.RectWidth, previous.RectWidth, next.RectWidth);
        skill.RectHeight = Merge(skill.RectHeight, previous.RectHeight, next.RectHeight);
        skill.OffsetX = Merge(skill.OffsetX, previous.OffsetX, next.OffsetX);
        skill.OffsetY = Merge(skill.OffsetY, previous.OffsetY, next.OffsetY);
        skill.ProjectileSpeed = Merge(skill.ProjectileSpeed, previous.ProjectileSpeed, next.ProjectileSpeed);
        skill.ProjectileCount = Merge(skill.ProjectileCount, previous.ProjectileCount, next.ProjectileCount);
        skill.SpreadAngle = Merge(skill.SpreadAngle, previous.SpreadAngle, next.SpreadAngle);
        skill.VfxLifetime = Merge(skill.VfxLifetime, previous.VfxLifetime, next.VfxLifetime);
        skill.ImageUrl = Merge(skill.ImageUrl, previous.ImageUrl, next.ImageUrl);
    }

    private static async Task<(DateTime? max, int count)> VersionInfo(IQueryable<DateTime> query)
    {
        var count = await query.CountAsync();
        return count == 0 ? (null, 0) : (await query.MaxAsync(x => (DateTime?)x), count);
    }

    private static string Version(DateTime? max, int count) => max is null ? "0" : $"{max:O}|{count}";
}
