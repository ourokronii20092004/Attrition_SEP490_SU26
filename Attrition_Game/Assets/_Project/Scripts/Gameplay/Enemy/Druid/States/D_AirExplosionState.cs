using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 5: chuỗi zigzag tuần tự. Boss chơi xong Attack trước; mỗi điểm sau đó chạy
    /// Explosion Startup/repair (damage 0) → chờ lead → AirExplosion gây damage.
    /// </summary>
    public class D_AirExplosionState : DruidBossState
    {
        private enum Phase { AttackWindup, Startup, Explosion, Gap }

        private Phase _phase;
        private float _elapsed;
        private int _spawned;
        private Vector2 _origin;
        private Vector2 _currentPoint;
        private float _dirX;

        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _phase = Phase.AttackWindup;
            _elapsed = 0f;
            _spawned = 0;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.PlayerTarget != null
                ? (ai.PlayerTarget.position.x > ai.transform.position.x ? 1f : -1f)
                : (ai.NetFacingDir > 0 ? 1f : -1f);
            float feetY = ai.PlayerTarget != null ? ai.PlayerTarget.position.y : ai.transform.position.y;
            _origin = new Vector2(ai.transform.position.x + _dirX * ai.airExplosionStepX, feetY + 0.6f);

            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;
            ai.PlayAnim("Attack");
        }

        public override void Update(DruidBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            switch (_phase)
            {
                case Phase.AttackWindup:
                    if (_elapsed < ai.meleeDuration) return;
                    BeginStartup(ai);
                    return;

                case Phase.Startup:
                    if (_elapsed < ai.airExplosionStartupLead) return;
                    if (ai.HasStateAuthority)
                        ai.SpawnAoE(ai.AirExplosionPrefab, _currentPoint, ai.airExplosionDamage);
                    _phase = Phase.Explosion;
                    _elapsed = 0f;
                    return;

                case Phase.Explosion:
                    if (_elapsed < 0.45f) return;
                    _spawned++;
                    if (_spawned >= Mathf.Max(1, ai.airExplosionCount))
                    {
                        ai.ChangeState(DruidBossAI.RecoveryState);
                        return;
                    }
                    _phase = Phase.Gap;
                    _elapsed = 0f;
                    return;

                case Phase.Gap:
                    if (_elapsed < ai.airExplosionInterval) return;
                    BeginStartup(ai);
                    return;
            }
        }

        private void BeginStartup(DruidBossAI ai)
        {
            float x = _origin.x + _dirX * ai.airExplosionStepX * _spawned;
            float y = _origin.y + ((_spawned % 2 == 0) ? 0f : ai.airExplosionAmplitudeY);
            _currentPoint = new Vector2(x, y);

            if (ai.HasStateAuthority)
                ai.SpawnAoE(ai.AirExplosionStartupPrefab, _currentPoint, 0);

            _phase = Phase.Startup;
            _elapsed = 0f;
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
