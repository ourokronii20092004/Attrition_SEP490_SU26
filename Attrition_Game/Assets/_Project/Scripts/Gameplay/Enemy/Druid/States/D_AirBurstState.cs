using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 4: Air Burst — tạo airBurstCount điểm NGẪU NHIÊN quanh player (rải trong bán kính
    /// airBurstScatter, lệch phương ngang). Mỗi điểm là 1 AoE (airBurstPrefab) với "preparing" = damageDelay
    /// của prefab, nên sau airBurstPrepareTime tất cả cùng bung gây damage. Player thấy điểm hiện ra trước
    /// (telegraph) rồi né trước khi nổ.
    ///
    /// Chốt vị trí player 1 LẦN lúc bắt đầu (điểm cố định để né được) — spawn ngay toàn bộ điểm.
    /// AoE tự snap xuống đất nếu prefab bật snapToGround; ở đây thường tắt để nổ giữa không (air).
    /// </summary>
    public class D_AirBurstState : DruidBossState
    {
        private float _elapsed;
        private bool _spawned;

        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _spawned = false;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();
            // Druid chưa có clip skill riêng; tái dùng clip Attack thay vì tạo trigger/controller thừa.
            ai.PlayAnim("Attack");
        }

        public override void Update(DruidBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            if (!_spawned)
            {
                _spawned = true;
                if (ai.HasStateAuthority) SpawnBurstPoints(ai);
                return;
            }

            // Chờ preparing + chút đệm cho animation nổ rồi recovery (AoE tự gây damage theo damageDelay).
            if (_elapsed >= ai.airBurstPrepareTime + 0.4f)
                ai.ChangeState(DruidBossAI.RecoveryState);
        }

        private void SpawnBurstPoints(DruidBossAI ai)
        {
            Vector2 center = ai.PlayerTarget != null
                ? (Vector2)ai.PlayerTarget.position
                : (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * 3f, 0f);

            int count = Mathf.Max(1, ai.airBurstCount);
            for (int i = 0; i < count; i++)
            {
                // Rải quanh player: lệch ngang mạnh (pull-in phương ngang) + lệch dọc nhẹ.
                float offX = Random.Range(-ai.airBurstScatter, ai.airBurstScatter);
                float offY = Random.Range(-ai.airBurstScatter * 0.4f, ai.airBurstScatter * 0.4f);
                Vector2 pos = center + new Vector2(offX, offY);
                ai.SpawnAoE(ai.AirBurstPrefab, pos, ai.airBurstDamage);
            }
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
