using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 3: Wind Sword — boss vung tay (charge) rồi bắn windSwordCount lưỡi dao gió về phía player.
    /// windSwordInterval > 0 = bắn LẦN LƯỢT (mỗi lưỡi ngắm lại player, cảm giác truy đuổi); = 0 = bắn
    /// CÙNG LÚC theo quạt windSwordSpread quanh hướng tới player. Mỗi lưỡi là EnemyProjectile.
    /// </summary>
    public class D_WindSwordState : DruidBossState
    {
        private float _elapsed;
        private bool _charged;
        private int _fired;
        private float _nextFireTime;
        private Vector2 _spawnPos;
        private Vector2 _centerDir;

        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _charged = false;
            _fired = 0;
            _nextFireTime = 0f;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();
            ai.PlayAnim("Attack");
        }

        public override void Update(DruidBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Phase 1: charge.
            if (!_charged)
            {
                if (_elapsed < Mathf.Max(ai.windSwordChargeTime, ai.meleeDuration)) return;
                _charged = true;
                _nextFireTime = _elapsed;
                _spawnPos = (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * 0.8f, 0.5f);
                _centerDir = AimDir(ai, _spawnPos);
            }

            int count = Mathf.Max(1, ai.windSwordCount);

            // windSwordInterval = 0 → bắn cùng lúc theo quạt.
            if (ai.windSwordInterval <= 0.001f)
            {
                if (ai.HasStateAuthority)
                {
                    float baseAng = Mathf.Atan2(_centerDir.y, _centerDir.x) * Mathf.Rad2Deg;
                    float step = count > 1 ? ai.windSwordSpread / (count - 1) : 0f;
                    float start = baseAng - (count > 1 ? ai.windSwordSpread * 0.5f : 0f);
                    for (int i = 0; i < count; i++)
                    {
                        float a = (start + step * i) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        ai.SpawnProjectile(ai.WindSwordPrefab, _spawnPos, dir, ai.windSwordDamage, ai.windSwordSpeed);
                    }
                }
                ai.ChangeState(DruidBossAI.RecoveryState);
                return;
            }

            // windSwordInterval > 0 → bắn lần lượt, mỗi lưỡi ngắm lại player.
            if (_fired < count)
            {
                if (_elapsed >= _nextFireTime && ai.HasStateAuthority)
                {
                    Vector2 dir = AimDir(ai, _spawnPos);
                    ai.SpawnProjectile(ai.WindSwordPrefab, _spawnPos, dir, ai.windSwordDamage, ai.windSwordSpeed);
                    _fired++;
                    _nextFireTime = _elapsed + ai.windSwordInterval;
                }
                return;
            }

            ai.ChangeState(DruidBossAI.RecoveryState);
        }

        private static Vector2 AimDir(DruidBossAI ai, Vector2 from)
        {
            Vector2 dir = ai.PlayerTarget != null
                ? ((Vector2)ai.PlayerTarget.position - from).normalized
                : new Vector2(ai.AttackLockedFacingDir, 0f);
            if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(ai.AttackLockedFacingDir, 0f);
            return dir;
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
