namespace Enemy.Service.DTOs;

public record SkillConfigDto(
    string SkillId, string Element, int ManaCost, float CastTime, float Cooldown,
    float ActiveStartFrac, float ActiveEndFrac, string DamageType, int BaseDamage,
    float ApScaling, float KnockbackForce, float TickInterval, float SweetSpotRadius,
    float SweetSpotMultiplier, string Delivery, string HitShape, float Range, float Angle,
    float RectWidth, float RectHeight, float OffsetX, float OffsetY, float ProjectileSpeed,
    int ProjectileCount, float SpreadAngle, float VfxLifetime, string? ImageUrl = null);

public record SkillResponse(
    string SkillId, string Element, int ManaCost, float CastTime, float Cooldown,
    float ActiveStartFrac, float ActiveEndFrac, string DamageType, int BaseDamage,
    float ApScaling, float KnockbackForce, float TickInterval, float SweetSpotRadius,
    float SweetSpotMultiplier, string Delivery, string HitShape, float Range, float Angle,
    float RectWidth, float RectHeight, float OffsetX, float OffsetY, float ProjectileSpeed,
    int ProjectileCount, float SpreadAngle, float VfxLifetime, DateTime CreatedAt,
    DateTime UpdatedAt, string? ImageUrl = null);

public record SkillConfigBundle(string Version, int Count, List<SkillResponse> Skills);
