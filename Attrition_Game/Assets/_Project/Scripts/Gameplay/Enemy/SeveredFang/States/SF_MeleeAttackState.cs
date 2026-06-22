using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// Đòn đánh cận chiến thông thường, được kích hoạt khi ở khoảng cách gần.
    /// Tính toán hitbox melee và gây damage trực tiếp mà không dùng đạn.
    /// </summary>
    public class SF_MeleeAttackState : SeveredFangState
    {
        private float _elapsedTime;
        private bool _damageDealt;
        private const float DamageFrameTime = 0.3f; // Thời điểm gây sát thương (khớp animation)
        private const float TotalAttackTime = 0.7f; // Tổng thời gian animation chém

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsedTime = 0f;
            _damageDealt = false;
            ai.StopMovement();

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.AttackLockedFacingDir = ai.NetFacingDir;

            ai.PlayAttackAnim();
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsedTime += ai.Runner.DeltaTime;
            ai.StopMovement();

            if (!_damageDealt && _elapsedTime >= DamageFrameTime)
            {
                _damageDealt = true;
                if (ai.HasStateAuthority)
                {
                    PerformMeleeHitbox(ai);
                }
            }

            if (_elapsedTime >= TotalAttackTime)
            {
                ai.ChangeState(SeveredFangAI.RecoveryState);
            }
        }

        private void PerformMeleeHitbox(SeveredFangAI ai)
        {
            // Điểm chém trước mặt boss
            Vector2 hitboxCenter = (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * 1.0f, 0.5f);
            Vector2 boxSize = new Vector2(2f, 2f);

            Collider2D[] results = new Collider2D[10];
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = ai.obstacleLayer | LayerMask.GetMask("Player"), // Thường có layer Player riêng, dùng obstacleLayer tạm (tùy config EnemyAI)
                useTriggers = false
            };
            
            // Xoay hitbox theo hướng nhìn
            float angle = ai.AttackLockedFacingDir > 0 ? 0f : 180f;

            int count = Attrition.Gameplay.Combat.HitboxResolver.Overlap(
                ai.Runner.GetPhysicsScene2D(),
                EnemyCombat.HitboxShape.Rectangle,
                hitboxCenter, hitboxCenter,
                new Vector2(ai.AttackLockedFacingDir, 0),
                0f, 0f, boxSize, filter, results);

            for (int i = 0; i < count; i++)
            {
                var hit = results[i];
                IDamageable dmg = hit.GetComponentInParent<IDamageable>();
                // Tránh chém trúng quái khác (nếu layerMask dính layer Enemy)
                if (hit.gameObject.layer == LayerMask.NameToLayer("Enemy")) continue; 

                if (dmg != null && !dmg.IsDead)
                {
                    Vector2 pushDir = new Vector2(ai.AttackLockedFacingDir, 0.5f).normalized;
                    dmg.TakeDamage(ai.meleeDamage, pushDir, 3f, Attrition.Core.DamageType.Physical);
                }
            }
        }
    }
}
