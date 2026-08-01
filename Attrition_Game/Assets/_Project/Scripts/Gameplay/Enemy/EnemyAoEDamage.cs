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
        [Tooltip("Tự hạ vụ nổ xuống MẶT ĐẤT lúc spawn (tránh nổ lơ lửng). Tắt nếu muốn nổ đúng vị trí spawn.")]
        public bool snapToGround = true;
        [Tooltip("Layer mặt đất để hạ xuống. Bỏ trống = tự dùng layer 'Ground'.")]
        public LayerMask groundLayer;
        [Tooltip("Khoảng nhô lên trên mặt nền sau khi hạ (units).")]
        public float groundOffset = 0.3f;

        [Networked] private TickTimer LifeTimer { get; set; }
        [Networked] private int Damage { get; set; }
        [Networked] private int DamageTypeRaw { get; set; }
        [Networked] private NetworkBool DamageDealt { get; set; }

        private float _elapsed;
        private readonly HashSet<IDamageable> _hit = new HashSet<IDamageable>();

        public void Init(int dmg, Attrition.Core.DamageType type = Attrition.Core.DamageType.Magic, LayerMask? overrideHitLayer = null)
        {
            Damage = dmg;
            DamageTypeRaw = (int)type;
            if (overrideHitLayer.HasValue) hitLayer = overrideHitLayer.Value;
        }

        public override void Spawned()
        {
            _elapsed = 0f;

            // HẠ XUỐNG MẶT ĐẤT tại đây (sau khi NetworkTransform đã đặt vị trí spawn) — host sửa Y rồi
            // NetworkTransform sync xuống client. Khắc phục nổ lơ lửng do spawn ở tâm boss / NT ghi đè.
            if (HasStateAuthority && snapToGround)
                SnapToGround();

            if (HasStateAuthority) LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);

            // ponytail: debug — xóa sau khi xác nhận AoE hoạt động
            Debug.Log($"[AoE] Spawned {name} pos={transform.position} hitLayer={hitLayer.value} radius={radius} lifetime={lifetime} dmg={Damage}");
        }

        private void SnapToGround()
        {
            int mask = groundLayer.value != 0 ? groundLayer.value : LayerMask.GetMask("Ground");
            if (mask == 0) return;
            Vector2 origin = (Vector2)transform.position + Vector2.up * 5f;
            // QUAN TRỌNG: query PHYSICS SCENE CỦA FUSION, không phải Physics2D mặc định (scene đó rỗng
            // trong Fusion → raycast không trúng → nổ lơ lửng).
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = true };
            var results = new RaycastHit2D[4];
            int n = Runner.GetPhysicsScene2D().Raycast(origin, Vector2.down, 40f, filter, results);
            float bestY = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (results[i].collider == null) continue;
                if (results[i].point.y > bestY) { bestY = results[i].point.y; found = true; }
            }
            // Chỉ snap XUỐNG — không đẩy lên nếu ground cao hơn vị trí spawn (vd ceiling/platform).
            if (found && bestY + groundOffset < transform.position.y)
            {
                var p = transform.position;
                p.y = bestY + groundOffset;
                transform.position = p;
            }
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
