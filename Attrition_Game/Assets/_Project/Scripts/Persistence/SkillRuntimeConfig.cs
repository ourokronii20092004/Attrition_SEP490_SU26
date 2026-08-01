using Attrition.Core;
using Attrition.Data;
using UnityEngine;

namespace Attrition.Persistence
{
    public sealed class SkillRuntimeConfig
    {
        public SkillElement element;
        public int manaCost;
        public float castTime;
        public float cooldown;
        public float activeStartFrac;
        public float activeEndFrac;
        public DamageType damageType;
        public int baseDamage;
        public float apScaling;
        public float knockbackForce;
        public float tickInterval;
        public float sweetSpotRadius;
        public float sweetSpotMultiplier;
        public SkillDelivery delivery;
        public SkillHitShape hitShape;
        public float range;
        public float angle;
        public Vector2 rectSize;
        public Vector2 hitboxOffset;
        public float projectileSpeed;
        public int projectileCount;
        public float spreadAngle;
        public float projectileInterval;
        public float vfxLifetime;

        public int ComputeTickCount()
        {
            if (tickInterval <= 0f) return 1;
            float activeDuration = Mathf.Max(0f, activeEndFrac - activeStartFrac) * castTime;
            return Mathf.Max(1, Mathf.FloorToInt(activeDuration / tickInterval) + 1);
        }

        public static SkillRuntimeConfig From(SkillSO so)
        {
            var config = new SkillRuntimeConfig
            {
                element = so.element, manaCost = so.manaCost, castTime = so.castTime, cooldown = so.cooldown,
                activeStartFrac = so.activeStartFrac, activeEndFrac = so.activeEndFrac,
                damageType = so.damageType, baseDamage = so.baseDamage, apScaling = so.apScaling,
                knockbackForce = so.knockbackForce, tickInterval = so.tickInterval,
                sweetSpotRadius = so.sweetSpotRadius, sweetSpotMultiplier = so.sweetSpotMultiplier,
                delivery = so.delivery, hitShape = so.hitShape, range = so.range, angle = so.angle,
                rectSize = so.rectSize, hitboxOffset = so.hitboxOffset, projectileSpeed = so.projectileSpeed,
                projectileCount = so.projectileCount, spreadAngle = so.spreadAngle,
                projectileInterval = so.projectileInterval, vfxLifetime = so.vfxLifetime
            };
            var provider = SkillConfigProvider.Instance;
            return provider != null ? provider.ApplyOverride(so.itemId, config) : config;
        }
    }
}
