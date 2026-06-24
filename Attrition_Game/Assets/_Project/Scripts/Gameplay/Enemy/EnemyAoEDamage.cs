using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace Attrition.Gameplay.Enemy
{
    /// <summary>
    /// Vùng sát thương DIỆN RỘNG (AoE) cho đòn nổ đứng yên — vd FireExplosion của boss.
    /// Khác EnemyProjectile (đạn bay, CircleCast theo hướng): cái này quét OverlapCircle tại chỗ,
    /// gây damage MỘT LẦN cho mọi target trong bán kính, rồi sống nốt thời gian animation và despawn.
    ///
    /// Host-authoritative: chỉ host tính damage + despawn. Init() truyền damage/loại sát thương từ AI.
    /// Gắn lên prefab nổ (không cần collider — dùng physics query theo hitLayer).
    /// </summary>
    public class EnemyAoEDamage : NetworkBehaviour
    {
        [Tooltip("Layer bị trúng (nên gồm Player). Đặt qua Inspector hoặc tool.")]
        public LayerMask hitLayer;
        [Tooltip("Bán kính vùng nổ (đơn vị world).")]
        public float radius = 1.5f;
        [Tooltip("Thời gian sống trước khi despawn (khớp độ dài animation nổ).")]
        public float lifetime = 0.6f;
        [Tooltip("Trễ trước khi gây damage (giây) — khớp frame nổ bung ra. 0 = gây ngay khi spawn.")]
        public float damageDelay = 0.05f;
        [Tooltip("Lực đẩy lùi khi trúng.")]
        public float knockbackForce = 4f;

        [Networked] private TickTimer LifeTimer { get; set; }
        [Networked] private int Damage { get; set; }
        [Networked] private int DamageTypeRaw { get; set; }
        [Networked] private NetworkBool DamageDealt { get; set; }

        private float _elapsed;
        private readonly HashSet<IDamageable> _hit = new HashSet<IDamageable>();

        public void Init(int dmg, Attrition.Core.DamageType type = Attrition.Core.DamageType.Magic)
        {
            Damage = dmg;
            DamageTypeRaw = (int)type;
        }

        public override void Spawned()
        {
            _elapsed = 0f;
            if (HasStateAuthority) LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
        }

        public override void FixedUpdateNetwork()
        {
            if (Attrition.Persistence.GamePause.IsPaused) return;
            if (!HasStateAuthority) return;

            _elapsed += Runner.DeltaTime;

            // Gây damage 1 lần sau damageDelay (quét vùng tròn).
            if (!DamageDealt && _elapsed >= damageDelay)
            {
                DamageDealt = true;
                DealAreaDamage();
            }

            if (LifeTimer.Expired(Runner))
                Runner.Despawn(Object);
        }

        private void DealAreaDamage()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitLayer, useTriggers = true };
            var results = new Collider2D[10];
            int count = Runner.GetPhysicsScene2D().OverlapCircle(transform.position, radius, filter, results);

            for (int i = 0; i < count; i++)
            {
                var col = results[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || dmg.IsDead || _hit.Contains(dmg)) continue;
                _hit.Add(dmg);

                Vector2 dir = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;
                Vector2 pushDir = new Vector2(dir.x, 0.5f).normalized;
                dmg.TakeDamage(Damage, pushDir, knockbackForce, (Attrition.Core.DamageType)DamageTypeRaw);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
