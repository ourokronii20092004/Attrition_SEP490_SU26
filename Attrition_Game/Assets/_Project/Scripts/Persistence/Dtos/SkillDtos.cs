using System;
using System.Collections.Generic;

namespace Attrition.Persistence.Dtos
{
    [Serializable]
    public class SkillResponseDto
    {
        public string SkillId;
        public string Element;
        public int ManaCost;
        public float CastTime;
        public float Cooldown;
        public float ActiveStartFrac;
        public float ActiveEndFrac;
        public string DamageType;
        public int BaseDamage;
        public float ApScaling;
        public float KnockbackForce;
        public float TickInterval;
        public float SweetSpotRadius;
        public float SweetSpotMultiplier;
        public string Delivery;
        public string HitShape;
        public float Range;
        public float Angle;
        public float RectWidth;
        public float RectHeight;
        public float OffsetX;
        public float OffsetY;
        public float ProjectileSpeed;
        public int ProjectileCount;
        public float SpreadAngle;
        public float VfxLifetime;
    }

    [Serializable]
    public class SkillConfigBundleDto
    {
        public string Version;
        public int Count;
        public List<SkillResponseDto> Skills;
    }
}
