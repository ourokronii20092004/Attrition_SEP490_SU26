namespace Enemy.Service.Models;

public class SkillEntity
{
    public string SkillId { get; set; } = string.Empty;
    public string Element { get; set; } = "Fire";
    public int ManaCost { get; set; }
    public float CastTime { get; set; }
    public float Cooldown { get; set; }
    public float ActiveStartFrac { get; set; }
    public float ActiveEndFrac { get; set; }
    public string DamageType { get; set; } = "Magic";
    public int BaseDamage { get; set; }
    public float ApScaling { get; set; }
    public float KnockbackForce { get; set; }
    public float TickInterval { get; set; }
    public float SweetSpotRadius { get; set; }
    public float SweetSpotMultiplier { get; set; }
    public string Delivery { get; set; } = "AreaInstant";
    public string HitShape { get; set; } = "Cone";
    public float Range { get; set; }
    public float Angle { get; set; }
    public float RectWidth { get; set; }
    public float RectHeight { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float ProjectileSpeed { get; set; }
    public int ProjectileCount { get; set; }
    public float SpreadAngle { get; set; }
    public float VfxLifetime { get; set; }
    public string? ImageUrl { get; set; }

    // Baseline enables three-way merge without overwriting admin edits.
    public string? UnityBaselineJson { get; set; }
    public DateTime? ImportedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
