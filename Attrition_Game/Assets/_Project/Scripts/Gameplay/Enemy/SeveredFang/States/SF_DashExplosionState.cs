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
        private bool _attackAnimPlayed;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsedTime = 0f;
            _nextExplosionTime = 0f;
            _attackAnimPlayed = false;
            ai.DashExplosionSpawned = 0;

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
            _attackAnimPlayed = true;
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

                // Spawn FireExplosion theo interval
                if (_elapsedTime >= _nextExplosionTime && ai.DashExplosionSpawned < ai.dashExplosionCount)
                {
                    Vector2 explosionPos = (Vector2)ai.transform.position
                        + new Vector2(-ai.DashDirectionX * 0.5f, 0f); // Spawn hơi lùi phía sau
                    ai.SpawnFireExplosion(explosionPos, ai.dashExplosionDamage);
                    ai.DashExplosionSpawned++;
                    _nextExplosionTime = _elapsedTime + ai.dashExplosionInterval;
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
