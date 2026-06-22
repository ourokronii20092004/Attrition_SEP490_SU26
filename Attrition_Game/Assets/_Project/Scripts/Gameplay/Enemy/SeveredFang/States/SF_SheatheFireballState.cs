using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// SKILL 2: Sheathe Fireball — Boss rút kiếm (Sheathe animation) và tụ lực,
    /// sau đó ném ra một đạn Firebolt về phía người chơi.
    /// Tương tự đòn cast spell của boss (đứng yên, cast, nhắm chuẩn).
    /// </summary>
    public class SF_SheatheFireballState : SeveredFangState
    {
        private float _elapsedTime;
        private bool _fireboltSpawned;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsedTime = 0f;
            _fireboltSpawned = false;
            ai.StopMovement();

            // Nhìn về player
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();

            // Phát animation Sheathe (rút kiếm tụ lực)
            ai.PlaySheatheAnim();
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsedTime += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Nếu đang trong thời gian charge, liên tục cập nhật hướng nhìn player
            if (!_fireboltSpawned && _elapsedTime < ai.sheatheChargeTime)
            {
                ai.DetectPlayer();
                ai.FaceTowardsPlayer();
            }

            // Hết charge time → ném Firebolt
            if (!_fireboltSpawned && _elapsedTime >= ai.sheatheChargeTime)
            {
                _fireboltSpawned = true;

                // Tính hướng bắn
                Vector2 targetPos = ai.PlayerTarget != null ? (Vector2)ai.PlayerTarget.position : (Vector2)ai.transform.position + new Vector2(ai.NetFacingDir, 0);
                
                // Điểm spawn đạn: trước mặt boss một chút và cao hơn mặt đất
                Vector2 spawnPos = (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * 0.8f, 1f);
                Vector2 fireDir = (targetPos - spawnPos).normalized;

                // Tránh việc đạn bắn xuống đất thẳng đứng
                if (fireDir.y < -0.8f) fireDir.y = -0.8f;
                fireDir = fireDir.normalized;

                ai.SpawnFireBolt(spawnPos, fireDir, ai.sheatheFireboltDamage, ai.sheatheFireboltSpeed);
            }

            // Chờ animation hoàn thành (khoảng 0.5s sau khi bắn) → Recovery
            if (_fireboltSpawned && _elapsedTime >= ai.sheatheChargeTime + 0.5f)
            {
                ai.ChangeState(SeveredFangAI.RecoveryState);
            }
        }
    }
}
