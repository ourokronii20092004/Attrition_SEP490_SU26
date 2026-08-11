using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Skill.Service.DTOs;
using Skill.Service.Models;
using System.Text.Json;

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

    public async Task<ApiResponse> DeleteAsync(string id)
    {
        var skill = await repository.GetByIdAsync(id, tracked: true);
        if (skill == null) return ApiResponse.Fail("Skill not found. It may already be gone.");
        repository.Remove(skill);
        await repository.SaveChangesAsync();
        await cache.RemoveAsync("skill-bundle:all");
        return ApiResponse.Ok();
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
            else if (Signature(skill) != Signature(dto))
            {
                // Unity là nguồn thật khi bấm Sync: đè hết, kể cả field admin đã sửa trên web.
                // Chiều web → game vẫn chạy qua skill config bundle nên sửa trên web vẫn vào game.
                // So GIÁ TRỊ THẬT chứ không so UnityBaselineJson — baseline từng bị ghi lệch nên
                // record đã sync bằng bản cũ sẽ mắc kẹt ở "unchanged" vĩnh viễn.
                Apply(skill, dto);
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

    // Phải liệt kê đúng các field Apply() ghi — khác nhau ⇒ Apply() mới có tác dụng.
    private static string Signature(SkillEntity s) => JsonSerializer.Serialize(new object?[] {
        s.Name, s.Description, s.IconKey, s.Rarity, s.Element, s.ManaCost, s.CastTime, s.Cooldown,
        s.ActiveStartFrac, s.ActiveEndFrac, s.DamageType, s.BaseDamage, s.ApScaling, s.KnockbackForce,
        s.TickInterval, s.SweetSpotRadius, s.SweetSpotMultiplier, s.Delivery, s.HitShape, s.Range,
        s.Angle, s.RectWidth, s.RectHeight, s.OffsetX, s.OffsetY, s.ProjectileSpeed,
        s.ProjectileCount, s.SpreadAngle, s.VfxLifetime, s.ImageUrl }, JsonOptions);

    private static string Signature(SkillImportDto d) => JsonSerializer.Serialize(new object?[] {
        d.Name, Clean(d.Description), d.IconKey, d.Rarity, d.Element, d.ManaCost, d.CastTime, d.Cooldown,
        d.ActiveStartFrac, d.ActiveEndFrac, d.DamageType, d.BaseDamage, d.ApScaling, d.KnockbackForce,
        d.TickInterval, d.SweetSpotRadius, d.SweetSpotMultiplier, d.Delivery, d.HitShape, d.Range,
        d.Angle, d.RectWidth, d.RectHeight, d.OffsetX, d.OffsetY, d.ProjectileSpeed,
        d.ProjectileCount, d.SpreadAngle, d.VfxLifetime, d.ImageUrl }, JsonOptions);

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

    private static SkillDto ToDto(SkillEntity s) => new(s.SkillId, s.Name, s.Description, s.IconKey, s.Rarity,
        s.Element, s.ManaCost, s.CastTime, s.Cooldown, s.ActiveStartFrac, s.ActiveEndFrac, s.DamageType,
        s.BaseDamage, s.ApScaling, s.KnockbackForce, s.TickInterval, s.SweetSpotRadius, s.SweetSpotMultiplier,
        s.Delivery, s.HitShape, s.Range, s.Angle, s.RectWidth, s.RectHeight, s.OffsetX, s.OffsetY,
        s.ProjectileSpeed, s.ProjectileCount, s.SpreadAngle, s.VfxLifetime, s.CreatedAt, s.UpdatedAt, s.ImageUrl);
}