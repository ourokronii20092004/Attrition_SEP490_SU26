using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// SKILL 6: Firebolt Volley (firebolt nâng cao) — Boss vung tay (charge) rồi NÉM LIÊN TIẾP
    /// volleyCount firebolt (2-3) về phía player, cách nhau volleyInterval giây. Mỗi phát ngắm lại
    /// player tại thời điểm bắn nên khó né nếu đứng yên.
    ///
    /// Tái dùng Attack animation (không cần anim mới). Host điều khiển; firebolt là NetworkObject.
    /// </summary>
    public class SF_FireboltVolleyState : SeveredFangState
    {
        private float _elapsed;
        private int _fired;
        private float _nextFireTime;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _fired = 0;
            _nextFireTime = ai.volleyChargeTime; // phát đầu sau khi vung tay

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.AttackLockedFacingDir = ai.NetFacingDir;
            ai.StopMovement();
            ai.PlayAttackAnim();
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            int total = Mathf.Max(1, ai.volleyCount);

            // Ném từng phát theo nhịp volleyInterval, mỗi phát NGẮM LẠI player hiện tại.
            if (_fired < total && _elapsed >= _nextFireTime)
            {
                // Quay mặt + ngắm lại theo player mới nhất (player có thể đã di chuyển).
                ai.FaceTowardsPlayer();

                Vector2 spawnPos = (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * 0.8f, 0.3f);
                Vector2 dir = ai.PlayerTarget != null
                    ? ((Vector2)ai.PlayerTarget.position - spawnPos).normalized
                    : new Vector2(ai.AttackLockedFacingDir, 0f);
                if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(ai.AttackLockedFacingDir, 0f);

                ai.SpawnFireBolt(spawnPos, dir, ai.volleyDamage, ai.volleyFireboltSpeed);
                _fired++;
                _nextFireTime = _elapsed + ai.volleyInterval;
            }

            // Bắn hết → chờ 1 nhịp rồi recovery.
            if (_fired >= total && _elapsed >= _nextFireTime)
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
