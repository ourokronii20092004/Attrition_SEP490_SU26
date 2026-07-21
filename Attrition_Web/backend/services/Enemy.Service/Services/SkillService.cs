using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Enemy.Service.Data;
using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Enemy.Service.Services;

public class SkillService : ISkillService
{
    private readonly EnemyDbContext _db;
    private readonly ICacheService _cache;

    public SkillService(EnemyDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<SkillResponse>> GetAllAsync()
    {
        var skills = await _db.Skills.AsNoTracking().OrderBy(x => x.SkillId).Take(100).ToListAsync();
        var items = await SkillItems(skills.Select(x => x.SkillId), false);
        return skills.Where(x => items.ContainsKey(x.SkillId)).Select(x => ToResponse(x, items[x.SkillId])).ToList();
    }

    public async Task<SkillResponse?> GetByIdAsync(string skillId)
    {
        var skill = await _db.Skills.AsNoTracking().FirstOrDefaultAsync(x => x.SkillId == skillId);
        if (skill == null) return null;
        var item = await _db.Items.AsNoTracking().Include(x => x.Modifiers)
            .FirstOrDefaultAsync(x => x.ItemId == skillId && x.Category == "Skill");
        return item == null ? null : ToResponse(skill, item);
    }

    public async Task<ApiResponse<SkillResponse>> UpdateAsync(string skillId, SkillUpdateRequest request)
    {
        var skill = await _db.Skills.FirstOrDefaultAsync(x => x.SkillId == skillId);
        var item = await _db.Items.Include(x => x.Modifiers)
            .FirstOrDefaultAsync(x => x.ItemId == skillId && x.Category == "Skill");
        if (skill == null || item == null)
            return ApiResponse<SkillResponse>.Fail("Skill not found. Sync it from Unity first.");

        Apply(skill, ToConfig(request));
        item.Name = request.Name;
        item.Description = request.Description is null ? null : ContentSanitizer.Sanitize(request.Description);
        item.IconKey = request.IconKey;
        item.Rarity = request.Rarity;
        item.MaxStack = request.MaxStack;
        item.IsKeyItem = request.IsKeyItem;
        item.ImageUrl = request.ImageUrl;
        item.Modifiers.Clear();
        item.Modifiers.AddRange(request.Modifiers?.Select(x => new ItemModifierEntry
        {
            Stat = x.Stat,
            Amount = x.Amount
        }) ?? []);

        var now = DateTime.UtcNow;
        skill.ImageUrl = request.ImageUrl;
        skill.UpdatedAt = now;
        item.UpdatedAt = now;
        await _db.SaveChangesAsync();
        await InvalidateAsync();
        return ApiResponse<SkillResponse>.Ok(ToResponse(skill, item));
    }

    public async Task<SkillConfigBundle> GetConfigBundleAsync() =>
        await _cache.GetOrSetAsync("skill-bundle:all", async () =>
        {
            var skills = await _db.Skills.AsNoTracking().OrderBy(x => x.SkillId).ToListAsync();
            var items = await SkillItems(skills.Select(x => x.SkillId), false);
            var version = BuildVersion(skills.Count == 0 ? null : skills.Max(x => x.UpdatedAt), skills.Count);
            return new SkillConfigBundle(version, skills.Count,
                skills.Select(x => ToResponse(x, items.GetValueOrDefault(x.SkillId))).ToList());
        }, TimeSpan.FromMinutes(10));

    public async Task<(string version, int count)> GetVersionInfoAsync()
    {
        var count = await _db.Skills.CountAsync();
        var max = count == 0 ? null : await _db.Skills.MaxAsync(x => (DateTime?)x.UpdatedAt);
        return (BuildVersion(max, count), count);
    }

    private async Task<Dictionary<string, ItemEntity>> SkillItems(IEnumerable<string> ids, bool tracking)
    {
        var values = ids.ToList();
        var query = _db.Items.Include(x => x.Modifiers)
            .Where(x => values.Contains(x.ItemId) && x.Category == "Skill");
        if (!tracking) query = query.AsNoTracking();
        return await query.ToDictionaryAsync(x => x.ItemId, StringComparer.Ordinal);
    }

    private async Task InvalidateAsync()
    {
        await _cache.RemoveAsync("skill-bundle:all");
        await _cache.RemoveAsync("item-bundle:all");
        await _cache.RemoveByPrefixAsync("item-list:");
    }

    internal static void Apply(SkillEntity s, SkillConfigDto d)
    {
        s.Element = d.Element;
        s.ManaCost = d.ManaCost;
        s.CastTime = d.CastTime;
        s.Cooldown = d.Cooldown;
        s.ActiveStartFrac = d.ActiveStartFrac;
        s.ActiveEndFrac = d.ActiveEndFrac;
        s.DamageType = d.DamageType;
        s.BaseDamage = d.BaseDamage;
        s.ApScaling = d.ApScaling;
        s.KnockbackForce = d.KnockbackForce;
        s.TickInterval = d.TickInterval;
        s.SweetSpotRadius = d.SweetSpotRadius;
        s.SweetSpotMultiplier = d.SweetSpotMultiplier;
        s.Delivery = d.Delivery;
        s.HitShape = d.HitShape;
        s.Range = d.Range;
        s.Angle = d.Angle;
        s.RectWidth = d.RectWidth;
        s.RectHeight = d.RectHeight;
        s.OffsetX = d.OffsetX;
        s.OffsetY = d.OffsetY;
        s.ProjectileSpeed = d.ProjectileSpeed;
        s.ProjectileCount = d.ProjectileCount;
        s.SpreadAngle = d.SpreadAngle;
        s.VfxLifetime = d.VfxLifetime;
        s.ImageUrl = d.ImageUrl;
    }

    private static SkillConfigDto ToConfig(SkillUpdateRequest d) => new(
        d.SkillId, d.Element, d.ManaCost, d.CastTime, d.Cooldown, d.ActiveStartFrac,
        d.ActiveEndFrac, d.DamageType, d.BaseDamage, d.ApScaling, d.KnockbackForce,
        d.TickInterval, d.SweetSpotRadius, d.SweetSpotMultiplier, d.Delivery, d.HitShape,
        d.Range, d.Angle, d.RectWidth, d.RectHeight, d.OffsetX, d.OffsetY, d.ProjectileSpeed,
        d.ProjectileCount, d.SpreadAngle, d.VfxLifetime, d.ImageUrl);

    internal static SkillResponse ToResponse(SkillEntity s, ItemEntity? item = null) => new(
        s.SkillId, s.Element, s.ManaCost, s.CastTime, s.Cooldown, s.ActiveStartFrac,
        s.ActiveEndFrac, s.DamageType, s.BaseDamage, s.ApScaling, s.KnockbackForce,
        s.TickInterval, s.SweetSpotRadius, s.SweetSpotMultiplier, s.Delivery, s.HitShape,
        s.Range, s.Angle, s.RectWidth, s.RectHeight, s.OffsetX, s.OffsetY, s.ProjectileSpeed,
        s.ProjectileCount, s.SpreadAngle, s.VfxLifetime, s.CreatedAt, s.UpdatedAt,
        item?.ImageUrl ?? s.ImageUrl, item?.Name ?? string.Empty, item?.Description, item?.IconKey,
        item?.Rarity ?? "Common", item?.MaxStack ?? 1, item?.IsKeyItem ?? false,
        item?.Modifiers.Select(x => new ItemModifierDto(x.Stat, x.Amount)).ToList() ?? []);

    internal static string BuildVersion(DateTime? maxUpdatedAt, int count) =>
        maxUpdatedAt is null ? "0" : $"{maxUpdatedAt:O}|{count}";
}
