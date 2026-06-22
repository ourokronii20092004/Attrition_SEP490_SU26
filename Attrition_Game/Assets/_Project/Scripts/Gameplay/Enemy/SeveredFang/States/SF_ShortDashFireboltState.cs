using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// SKILL 3: Short Dash Firebolt — Boss lướt một đoạn ngắn về phía trước,
    /// ngay sau đó lập tức ném ra đạn Firebolt. Dùng khi muốn áp sát nhanh
    /// và tung đòn bất ngờ.
    /// </summary>
    public class SF_ShortDashFireboltState : SeveredFangState
    {
        private float _elapsedTime;
        private bool _fireboltSpawned;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsedTime = 0f;
            _fireboltSpawned = false;

            // Chốt hướng
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.DashDirectionX = ai.NetFacingDir;
            ai.AttackLockedFacingDir = ai.NetFacingDir;

            ai.PlayAttackAnim(); // Sử dụng chung Attack animation nhưng tốc độ lướt khác
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsedTime += ai.Runner.DeltaTime;

            // Phase 1: Lướt ngắn
            if (_elapsedTime <= ai.shortDashDuration)
            {
                ai.Rb.linearVelocity = new Vector2(ai.DashDirectionX * ai.shortDashSpeed, ai.Rb.linearVelocity.y);
                ai.NetSpeed = ai.shortDashSpeed;
            }
            // Phase 2: Dừng lại và bắn
            else if (!_fireboltSpawned)
            {
                ai.StopMovement();
                _fireboltSpawned = true;

                // Bắn luôn theo hướng lướt ngang, không ngắm kĩ như Sheathe
                Vector2 spawnPos = (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * 0.8f, 1f);
                Vector2 fireDir = new Vector2(ai.AttackLockedFacingDir, 0).normalized;

                ai.SpawnFireBolt(spawnPos, fireDir, ai.shortDashFireboltDamage, ai.shortDashFireboltSpeed);
            }
            // Phase 3: Hoàn thành → Recovery
            else if (_fireboltSpawned && _elapsedTime >= ai.shortDashDuration + 0.3f)
            {
                ai.ChangeState(SeveredFangAI.RecoveryState);
            }
        }

        public override void Exit(SeveredFangAI ai)
        {
            ai.StopMovement();
        }
    }
}
