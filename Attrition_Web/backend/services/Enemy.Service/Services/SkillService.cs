using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
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

    public async Task<List<SkillResponse>> GetAllAsync() =>
        (await _db.Skills.AsNoTracking().OrderBy(x => x.SkillId).Take(100).ToListAsync())
        .Select(ToResponse).ToList();

    public async Task<SkillResponse?> GetByIdAsync(string skillId)
    {
        var skill = await _db.Skills.AsNoTracking().FirstOrDefaultAsync(x => x.SkillId == skillId);
        return skill == null ? null : ToResponse(skill);
    }

    public async Task<ApiResponse<SkillResponse>> UpdateAsync(string skillId, SkillConfigDto request)
    {
        var skill = await _db.Skills.FirstOrDefaultAsync(x => x.SkillId == skillId);
        if (skill == null) return ApiResponse<SkillResponse>.Fail("Skill not found. Sync it from Unity first.");
        Apply(skill, request);
        skill.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await InvalidateAsync();
        return ApiResponse<SkillResponse>.Ok(ToResponse(skill));
    }

    public async Task<SkillConfigBundle> GetConfigBundleAsync() =>
        await _cache.GetOrSetAsync("skill-bundle:all", async () =>
        {
            var skills = await _db.Skills.AsNoTracking().OrderBy(x => x.SkillId).ToListAsync();
            var version = BuildVersion(skills.Count == 0 ? null : skills.Max(x => x.UpdatedAt), skills.Count);
            return new SkillConfigBundle(version, skills.Count, skills.Select(ToResponse).ToList());
        }, TimeSpan.FromMinutes(10));

    public async Task<(string version, int count)> GetVersionInfoAsync()
    {
        var count = await _db.Skills.CountAsync();
        var max = count == 0 ? null : await _db.Skills.MaxAsync(x => (DateTime?)x.UpdatedAt);
        return (BuildVersion(max, count), count);
    }

    public Task InvalidateAsync() => _cache.RemoveAsync("skill-bundle:all");

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

    internal static SkillResponse ToResponse(SkillEntity s) => new(
        s.SkillId, s.Element, s.ManaCost, s.CastTime, s.Cooldown, s.ActiveStartFrac,
        s.ActiveEndFrac, s.DamageType, s.BaseDamage, s.ApScaling, s.KnockbackForce,
        s.TickInterval, s.SweetSpotRadius, s.SweetSpotMultiplier, s.Delivery, s.HitShape,
        s.Range, s.Angle, s.RectWidth, s.RectHeight, s.OffsetX, s.OffsetY, s.ProjectileSpeed,
        s.ProjectileCount, s.SpreadAngle, s.VfxLifetime, s.CreatedAt, s.UpdatedAt, s.ImageUrl);

    internal static string BuildVersion(DateTime? maxUpdatedAt, int count) =>
        maxUpdatedAt is null ? "0" : $"{maxUpdatedAt:O}|{count}";
}
