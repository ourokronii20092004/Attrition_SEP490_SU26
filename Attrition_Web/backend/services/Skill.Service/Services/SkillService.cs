using System.Text.Json;
using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Skill.Service.DTOs;
using Skill.Service.Models;

namespace Skill.Service.Services;

public class SkillService(ISkillRepository repository, ICacheService cache) : ISkillService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<SkillDto>> GetAllAsync() =>
        (await repository.GetAllAsync()).Select(ToDto).ToList();

    public async Task<SkillDto?> GetByIdAsync(string id)
    {
        var skill = await repository.GetByIdAsync(id);
        return skill == null ? null : ToDto(skill);
    }

    public async Task<ApiResponse<SkillDto>> UpdateAsync(string id, SkillUpdateRequest request)
    {
        var skill = await repository.GetByIdAsync(id, tracked: true);
        if (skill == null) return ApiResponse<SkillDto>.Fail("Skill not found. Sync it from Unity first.");
        Apply(skill, request);
        skill.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync();
        await cache.RemoveAsync("skill-bundle:all");
        return ApiResponse<SkillDto>.Ok(ToDto(skill));
    }

    public async Task<ApiResponse<SkillImportResult>> ImportAsync(SkillImportRequest request)
    {
        var ids = request.Skills.Select(x => x.SkillId).ToList();
        var existing = await repository.GetByIdsAsync(ids);
        var created = 0; var updated = 0; var unchanged = 0;
        var now = DateTime.UtcNow;
        foreach (var dto in request.Skills)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.SkillId, out var skill))
            {
                skill = new SkillEntity { SkillId = dto.SkillId, CreatedAt = now, UpdatedAt = now };
                Apply(skill, dto);
                skill.UnityBaselineJson = baseline;
                skill.ImportedAt = now;
                repository.Add(skill);
                created++;
            }
            else if (skill.UnityBaselineJson != baseline)
            {
                var previous = Deserialize(skill.UnityBaselineJson);
                if (previous == null) ApplyMissing(skill, dto);
                else Merge(skill, previous, dto);
                skill.UnityBaselineJson = baseline;
                skill.ImportedAt = now;
                skill.UpdatedAt = now;
                updated++;
            }
            else unchanged++;
        }
        await repository.SaveChangesAsync();
        await cache.RemoveAsync("skill-bundle:all");
        return ApiResponse<SkillImportResult>.Ok(new(new(created, updated, unchanged), await VersionAsync()));
    }

    public Task<SkillConfigBundle> GetConfigBundleAsync() =>
        cache.GetOrSetAsync("skill-bundle:all", async () =>
        {
            var skills = await repository.GetAllAsync(orderById: true);
            return new SkillConfigBundle(Version(skills.Count == 0 ? null : skills.Max(x => x.UpdatedAt), skills.Count),
                skills.Count, skills.Select(ToDto).ToList());
        }, TimeSpan.FromMinutes(10));

    public async Task<int> CountAsync() => (await repository.GetVersionInfoAsync()).Count;

    private async Task<string> VersionAsync()
    {
        var (max, count) = await repository.GetVersionInfoAsync();
        return Version(max, count);
    }

    private static string Version(DateTime? max, int count) => max is null ? "0" : $"{max:O}|{count}";
    private static SkillImportDto? Deserialize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try { return JsonSerializer.Deserialize<SkillImportDto>(value, JsonOptions); }
        catch (JsonException) { return null; }
    }
    private static T Choose<T>(T current, T previous, T next) => EqualityComparer<T>.Default.Equals(current, previous) ? next : current;

    private static void Apply(SkillEntity s, SkillImportDto d)
    {
        s.Name = d.Name; s.Description = Clean(d.Description); s.IconKey = d.IconKey; s.Rarity = d.Rarity;
        ApplyConfig(s, d.Element, d.ManaCost, d.CastTime, d.Cooldown, d.ActiveStartFrac, d.ActiveEndFrac,
            d.DamageType, d.BaseDamage, d.ApScaling, d.KnockbackForce, d.TickInterval, d.SweetSpotRadius,
            d.SweetSpotMultiplier, d.Delivery, d.HitShape, d.Range, d.Angle, d.RectWidth, d.RectHeight,
            d.OffsetX, d.OffsetY, d.ProjectileSpeed, d.ProjectileCount, d.SpreadAngle, d.VfxLifetime, d.ImageUrl);
    }
    private static void Apply(SkillEntity s, SkillUpdateRequest d)
    {
        s.Name = d.Name; s.Description = Clean(d.Description); s.IconKey = d.IconKey; s.Rarity = d.Rarity;
        ApplyConfig(s, d.Element, d.ManaCost, d.CastTime, d.Cooldown, d.ActiveStartFrac, d.ActiveEndFrac,
            d.DamageType, d.BaseDamage, d.ApScaling, d.KnockbackForce, d.TickInterval, d.SweetSpotRadius,
            d.SweetSpotMultiplier, d.Delivery, d.HitShape, d.Range, d.Angle, d.RectWidth, d.RectHeight,
            d.OffsetX, d.OffsetY, d.ProjectileSpeed, d.ProjectileCount, d.SpreadAngle, d.VfxLifetime, d.ImageUrl);
    }
    private static string? Clean(string? value) => value is null ? null : ContentSanitizer.Sanitize(value);
    private static void ApplyConfig(SkillEntity s, string element, int mana, float cast, float cooldown,
        float start, float end, string damageType, int damage, float scaling, float knockback, float tick,
        float sweetRadius, float sweetMultiplier, string delivery, string shape, float range, float angle,
        float width, float height, float x, float y, float speed, int count, float spread, float lifetime, string? image)
    {
        s.Element = element; s.ManaCost = mana; s.CastTime = cast; s.Cooldown = cooldown;
        s.ActiveStartFrac = start; s.ActiveEndFrac = end; s.DamageType = damageType; s.BaseDamage = damage;
        s.ApScaling = scaling; s.KnockbackForce = knockback; s.TickInterval = tick;
        s.SweetSpotRadius = sweetRadius; s.SweetSpotMultiplier = sweetMultiplier; s.Delivery = delivery;
        s.HitShape = shape; s.Range = range; s.Angle = angle; s.RectWidth = width; s.RectHeight = height;
        s.OffsetX = x; s.OffsetY = y; s.ProjectileSpeed = speed; s.ProjectileCount = count;
        s.SpreadAngle = spread; s.VfxLifetime = lifetime; s.ImageUrl = image;
    }
    private static void ApplyMissing(SkillEntity s, SkillImportDto d)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) s.Name = d.Name;
        if (s.Description == null) s.Description = Clean(d.Description);
        if (s.IconKey == null) s.IconKey = d.IconKey;
        if (s.ImageUrl == null) s.ImageUrl = d.ImageUrl;
    }
    private static void Merge(SkillEntity s, SkillImportDto p, SkillImportDto n)
    {
        s.Name = Choose(s.Name, p.Name, n.Name); s.Description = Choose(s.Description, Clean(p.Description), Clean(n.Description));
        s.IconKey = Choose(s.IconKey, p.IconKey, n.IconKey); s.Rarity = Choose(s.Rarity, p.Rarity, n.Rarity);
        s.Element = Choose(s.Element, p.Element, n.Element); s.ManaCost = Choose(s.ManaCost, p.ManaCost, n.ManaCost);
        s.CastTime = Choose(s.CastTime, p.CastTime, n.CastTime); s.Cooldown = Choose(s.Cooldown, p.Cooldown, n.Cooldown);
        s.ActiveStartFrac = Choose(s.ActiveStartFrac, p.ActiveStartFrac, n.ActiveStartFrac); s.ActiveEndFrac = Choose(s.ActiveEndFrac, p.ActiveEndFrac, n.ActiveEndFrac);
        s.DamageType = Choose(s.DamageType, p.DamageType, n.DamageType); s.BaseDamage = Choose(s.BaseDamage, p.BaseDamage, n.BaseDamage);
        s.ApScaling = Choose(s.ApScaling, p.ApScaling, n.ApScaling); s.KnockbackForce = Choose(s.KnockbackForce, p.KnockbackForce, n.KnockbackForce);
        s.TickInterval = Choose(s.TickInterval, p.TickInterval, n.TickInterval); s.SweetSpotRadius = Choose(s.SweetSpotRadius, p.SweetSpotRadius, n.SweetSpotRadius);
        s.SweetSpotMultiplier = Choose(s.SweetSpotMultiplier, p.SweetSpotMultiplier, n.SweetSpotMultiplier); s.Delivery = Choose(s.Delivery, p.Delivery, n.Delivery);
        s.HitShape = Choose(s.HitShape, p.HitShape, n.HitShape); s.Range = Choose(s.Range, p.Range, n.Range); s.Angle = Choose(s.Angle, p.Angle, n.Angle);
        s.RectWidth = Choose(s.RectWidth, p.RectWidth, n.RectWidth); s.RectHeight = Choose(s.RectHeight, p.RectHeight, n.RectHeight);
        s.OffsetX = Choose(s.OffsetX, p.OffsetX, n.OffsetX); s.OffsetY = Choose(s.OffsetY, p.OffsetY, n.OffsetY);
        s.ProjectileSpeed = Choose(s.ProjectileSpeed, p.ProjectileSpeed, n.ProjectileSpeed); s.ProjectileCount = Choose(s.ProjectileCount, p.ProjectileCount, n.ProjectileCount);
        s.SpreadAngle = Choose(s.SpreadAngle, p.SpreadAngle, n.SpreadAngle); s.VfxLifetime = Choose(s.VfxLifetime, p.VfxLifetime, n.VfxLifetime);
        s.ImageUrl = Choose(s.ImageUrl, p.ImageUrl, n.ImageUrl);
    }
    private static SkillDto ToDto(SkillEntity s) => new(s.SkillId, s.Name, s.Description, s.IconKey, s.Rarity,
        s.Element, s.ManaCost, s.CastTime, s.Cooldown, s.ActiveStartFrac, s.ActiveEndFrac, s.DamageType,
        s.BaseDamage, s.ApScaling, s.KnockbackForce, s.TickInterval, s.SweetSpotRadius, s.SweetSpotMultiplier,
        s.Delivery, s.HitShape, s.Range, s.Angle, s.RectWidth, s.RectHeight, s.OffsetX, s.OffsetY,
        s.ProjectileSpeed, s.ProjectileCount, s.SpreadAngle, s.VfxLifetime, s.CreatedAt, s.UpdatedAt, s.ImageUrl);
}
