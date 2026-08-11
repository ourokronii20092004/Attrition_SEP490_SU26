namespace Skill.Service.DTOs;

public record SkillDto(
    string SkillId, string Name, string? Description, string? IconKey, string Rarity,
    string Element, int ManaCost, float CastTime, float Cooldown, float ActiveStartFrac,
    float ActiveEndFrac, string DamageType, int BaseDamage, float ApScaling,
    float KnockbackForce, float TickInterval, float SweetSpotRadius,
    float SweetSpotMultiplier, string Delivery, string HitShape, float Range, float Angle,
    float RectWidth, float RectHeight, float OffsetX, float OffsetY, float ProjectileSpeed,
    int ProjectileCount, float SpreadAngle, float VfxLifetime, DateTime CreatedAt,
    DateTime UpdatedAt, string? ImageUrl = null);

public record SkillImportDto(
    string SkillId, string Name, string? Description, string? IconKey, string Rarity,
    string Element, int ManaCost, float CastTime, float Cooldown, float ActiveStartFrac,
    float ActiveEndFrac, string DamageType, int BaseDamage, float ApScaling,
    float KnockbackForce, float TickInterval, float SweetSpotRadius,
    float SweetSpotMultiplier, string Delivery, string HitShape, float Range, float Angle,
    float RectWidth, float RectHeight, float OffsetX, float OffsetY, float ProjectileSpeed,
    int ProjectileCount, float SpreadAngle, float VfxLifetime, string? ImageUrl = null);

public record SkillUpdateRequest(
    string Name, string? Description, string? IconKey, string Rarity, string Element,
    int ManaCost, float CastTime, float Cooldown, float ActiveStartFrac, float ActiveEndFrac,
    string DamageType, int BaseDamage, float ApScaling, float KnockbackForce,
    float TickInterval, float SweetSpotRadius, float SweetSpotMultiplier, string Delivery,
    string HitShape, float Range, float Angle, float RectWidth, float RectHeight,
    float OffsetX, float OffsetY, float ProjectileSpeed, int ProjectileCount,
    float SpreadAngle, float VfxLifetime, string? ImageUrl = null);

public record SkillImportRequest(List<SkillImportDto> Skills);
public record ImportCounts(int Created, int BaselinesUpdated, int Unchanged);
public record SkillImportResult(ImportCounts Skills, string Version);
public record SkillConfigBundle(string Version, int Count, List<SkillDto> Skills);