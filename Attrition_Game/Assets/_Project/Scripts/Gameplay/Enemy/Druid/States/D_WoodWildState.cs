using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 1: Wood Wild — phóng woodWaves lượt (mặc định 3), mỗi lượt woodPerWave viên gỗ (mặc định 3)
    /// RƠI DỌC từ trên xuống, rải ngang quanh vị trí player. Giữa 2 lượt có khoảng trống woodWaveGap giây
    /// để player kịp né. Viên gỗ là EnemyProjectile bay hướng xuống (Vector2.down).
    ///
    /// Mỗi lượt "khoá" vị trí X của player tại thời điểm bắt đầu lượt đó → player di chuyển là né được.
    /// </summary>
    public class D_WoodWildState : DruidBossState
    {
        private int _wavesDone;
        private float _waveTimer;

        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _wavesDone = 0;
            _waveTimer = ai.meleeDuration; // boss chạy xong Attack rồi lượt gỗ đầu mới rơi

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();
            ai.PlayAnim("Attack"); // tái dùng anim Attack (chưa có anim riêng)
        }

        public override void Update(DruidBossAI ai)
        {
            ai.StopMovement();
            _waveTimer -= ai.Runner.DeltaTime;

            // Lượt đầu chờ animation Attack; các lượt sau chờ woodWaveGap.
            if (_waveTimer <= 0f && _wavesDone < Mathf.Max(1, ai.woodWaves))
            {
                SpawnWave(ai);
                _wavesDone++;
                _waveTimer = ai.woodWaveGap;
                return;
            }

            // Xong hết lượt + chờ nốt khoảng trống cuối → recovery.
            if (_wavesDone >= Mathf.Max(1, ai.woodWaves) && _waveTimer <= 0f)
                ai.ChangeState(DruidBossAI.RecoveryState);
        }

        /// <summary>Thả woodPerWave viên gỗ rơi dọc, rải ngang quanh cột X của player hiện tại.</summary>
        private void SpawnWave(DruidBossAI ai)
        {
            if (!ai.HasStateAuthority) return;

            float centerX = ai.PlayerTarget != null ? ai.PlayerTarget.position.x : ai.transform.position.x;
            float baseY = (ai.PlayerTarget != null ? ai.PlayerTarget.position.y : ai.transform.position.y) + ai.woodSpawnHeight;

            int count = Mathf.Max(1, ai.woodPerWave);
            float start = centerX - (count - 1) * ai.woodSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector2 pos = new Vector2(start + i * ai.woodSpacing, baseY);
                ai.SpawnProjectile(ai.WoodProjectilePrefab, pos, Vector2.down, ai.woodDamage, ai.woodSpeed);
            }
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
