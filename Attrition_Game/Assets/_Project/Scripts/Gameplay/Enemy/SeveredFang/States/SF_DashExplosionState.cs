using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// SKILL 1: Dash Explosion — Lướt dài xuyên qua player,
    /// để lại nhiều vụ nổ FireExplosion phía sau gây sát thương vùng.
    /// 
    /// Flow: Chơi animation Attack → Dash nhanh → Spawn FireExplosion dọc đường → Recovery.
    /// Tham khảo: Hornet dash attack trong Hollow Knight.
    /// </summary>
    public class SF_DashExplosionState : SeveredFangState
    {
        private float _elapsedTime;
        private float _nextExplosionTime;
        private float _lastExplosionX;     // vị trí X vụ nổ gần nhất (cho chế độ theo khoảng cách)
        private bool _hasFirstExplosion;
        
        // Cache to prevent GC alloc lag and duplicate hits
        private Collider2D[] _hitResults = new Collider2D[5];
        private ContactFilter2D _contactFilter;
        private System.Collections.Generic.HashSet<IDamageable> _hitTargets = new System.Collections.Generic.HashSet<IDamageable>();

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsedTime = 0f;
            _nextExplosionTime = 0f;
            _hasFirstExplosion = false;
            ai.DashExplosionSpawned = 0;
            _hitTargets.Clear();

            // Chốt hướng dash về phía player
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            if (ai.PlayerTarget != null)
            {
                ai.DashDirectionX = ai.PlayerTarget.position.x > ai.transform.position.x ? 1f : -1f;
            }
            else
            {
                ai.DashDirectionX = ai.NetFacingDir > 0 ? 1f : -1f;
            }
            ai.AttackLockedFacingDir = ai.DashDirectionX;
            ai.NetFacingDir = ai.DashDirectionX;

            // Chơi animation attack
            ai.PlayAttackAnim();

            _contactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = ai.obstacleLayer | LayerMask.GetMask("Player"),
                useTriggers = false
            };
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsedTime += ai.Runner.DeltaTime;

            // Phase 1: Lướt nhanh
            if (_elapsedTime <= ai.dashDuration)
            {
                // Di chuyển nhanh theo hướng đã chốt
                ai.Rb.linearVelocity = new Vector2(ai.DashDirectionX * ai.dashSpeed, ai.Rb.linearVelocity.y);
                ai.NetSpeed = ai.dashSpeed;

                if (ai.HasStateAuthority)
                {
                    Vector2 hitboxCenter = (Vector2)ai.transform.position;
                    Vector2 boxSize = new Vector2(2f, 2.5f);
                    
                    int count = Attrition.Gameplay.Combat.HitboxResolver.Overlap(
                        ai.Runner.GetPhysicsScene2D(), EnemyCombat.HitboxShape.Rectangle,
                        hitboxCenter, hitboxCenter, new Vector2(ai.DashDirectionX, 0),
                        0f, 0f, boxSize, _contactFilter, _hitResults);

                    for (int i = 0; i < count; i++)
                    {
                        var hit = _hitResults[i];
                        if (hit.gameObject.layer == LayerMask.NameToLayer("Enemy")) continue;
                        IDamageable dmg = hit.GetComponentInParent<IDamageable>();
                        if (dmg != null && !dmg.IsDead && !_hitTargets.Contains(dmg))
                        {
                            _hitTargets.Add(dmg);
                            Vector2 pushDir = new Vector2(ai.DashDirectionX, 0.5f).normalized;
                            dmg.TakeDamage(ai.meleeDamage, pushDir, 5f, Attrition.Core.DamageType.Physical);
                        }
                    }
                }

                // Spawn FireExplosion — theo KHOẢNG CÁCH (để có khe trống cho player né) nếu spacing>0,
                // ngược lại theo thời gian (interval) như cũ.
                bool shouldSpawn;
                if (ai.dashExplosionSpacing > 0.01f)
                {
                    shouldSpawn = !_hasFirstExplosion
                        || Mathf.Abs(ai.transform.position.x - _lastExplosionX) >= ai.dashExplosionSpacing;
                }
                else
                {
                    shouldSpawn = _elapsedTime >= _nextExplosionTime;
                }

                if (shouldSpawn && ai.DashExplosionSpawned < ai.dashExplosionCount)
                {
                    Vector2 explosionPos = (Vector2)ai.transform.position
                        + new Vector2(-ai.DashDirectionX * 0.5f, 0f); // Spawn hơi lùi phía sau
                    ai.SpawnFireExplosion(explosionPos, ai.dashExplosionDamage); // tự hạ xuống mặt đất
                    ai.DashExplosionSpawned++;
                    _nextExplosionTime = _elapsedTime + ai.dashExplosionInterval;
                    _lastExplosionX = ai.transform.position.x;
                    _hasFirstExplosion = true;
                }
            }
            else
            {
                // Phase 2: Dash xong → chuyển Recovery
                ai.StopMovement();
                ai.ChangeState(SeveredFangAI.RecoveryState);
            }
        }

        public override void Exit(SeveredFangAI ai)
        {
            ai.StopMovement();
        }
    }
}
