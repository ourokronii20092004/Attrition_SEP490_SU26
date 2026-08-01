using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 5: Air Explosion — tạo chuỗi airExplosionCount điểm nổ theo hình ZIGZAG chạy về phía player,
    /// và nổ LẦN LƯỢT theo thứ tự (điểm 1 → 2 → ... → n). Mỗi điểm spawn cách nhau airExplosionInterval
    /// giây; vì AoE (airExplosionPrefab) tự gây damage sau damageDelay ngắn của nó, việc spawn tuần tự tạo
    /// đúng cảm giác "nổ lần lượt" chạy dọc hàng.
    ///
    /// Zigzag: X tiến đều theo hướng nhìn (airExplosionStepX), Y so le cao/thấp (airExplosionAmplitudeY)
    /// theo chỉ số chẵn/lẻ. Điểm gốc = chân boss.
    /// </summary>
    public class D_AirExplosionState : DruidBossState
    {
        private float _elapsed;
        private int _spawnedCount;
        private float _nextSpawnTime;
        private Vector2 _origin;
        private float _dirX;

        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _spawnedCount = 0;
            _nextSpawnTime = 0f;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.PlayerTarget != null
                ? (ai.PlayerTarget.position.x > ai.transform.position.x ? 1f : -1f)
                : (ai.NetFacingDir > 0 ? 1f : -1f);
            _origin = (Vector2)ai.transform.position + new Vector2(_dirX * ai.airExplosionStepX, 0f);

            // Druid chưa có clip skill riêng; tái dùng clip Attack thay vì tạo trigger/controller thừa.
            ai.PlayAnim("Attack");
        }

        public override void Update(DruidBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Spawn từng điểm nổ theo nhịp airExplosionInterval → nổ lần lượt 1..n.
            if (_spawnedCount < ai.airExplosionCount && _elapsed >= _nextSpawnTime)
            {
                if (ai.HasStateAuthority)
                {
                    // Zigzag: X tiến đều; Y so le theo chẵn/lẻ.
                    float x = _origin.x + _dirX * ai.airExplosionStepX * _spawnedCount;
                    float y = _origin.y + ((_spawnedCount % 2 == 0) ? ai.airExplosionAmplitudeY : -ai.airExplosionAmplitudeY);
                    ai.SpawnAoE(ai.AirExplosionPrefab, new Vector2(x, y), ai.airExplosionDamage);
                }
                _spawnedCount++;
                _nextSpawnTime = _elapsed + ai.airExplosionInterval;
            }

            // Xong toàn bộ chuỗi + đệm cho quả cuối nổ → recovery.
            if (_spawnedCount >= ai.airExplosionCount && _elapsed >= _nextSpawnTime + 0.5f)
                ai.ChangeState(DruidBossAI.RecoveryState);
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
