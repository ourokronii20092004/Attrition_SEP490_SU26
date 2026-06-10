using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Combat
{
    /// <summary>
    /// Gom logic khởi tạo projectile sau khi Runner.Spawn — trước đây lặp ở EnemyCombat và EliteEnemySkills.
    /// 1 prefab đạn chỉ có 1 trong 2 component (EnemyProjectile thường HOẶC SpearProjectile của Huntress),
    /// helper init đúng cái có mặt. Gọi trong callback OnBeforeSpawned của Runner.Spawn.
    /// </summary>
    public static class ProjectileInitializer
    {
        public const float DefaultSpeed = 8f;
        public const float DefaultKnockback = 4f;

        public static void Init(NetworkObject obj, Vector2 dir, int damage, float speed = DefaultSpeed, Attrition.Core.DamageType type = Attrition.Core.DamageType.Physical, float knockback = DefaultKnockback)
        {
            if (obj == null) return;

            var proj = obj.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                if (speed > 0f) proj.speed = speed; // tốc độ đạn thật
                proj.Init(dir, damage, knockback, type); // tham số 3 = lực đẩy lùi
            }

            var spear = obj.GetComponent<SpearProjectile>();
            if (spear != null)
            {
                if (speed > 0f) spear.speed = speed;
                spear.Init(dir, damage, knockback, type);
            }
        }
    }
}
