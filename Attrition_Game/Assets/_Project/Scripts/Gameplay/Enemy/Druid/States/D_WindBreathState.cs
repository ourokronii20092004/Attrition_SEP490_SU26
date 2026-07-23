using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 2: Wind Breath — boss hít hơi (charge) rồi phun 1 LUỒNG GIÓ DÀI theo đường thẳng ngang,
    /// kéo dài về phía player hết sân rồi biến mất. Cài đặt bằng windBreathSegments đốt gió AoE spawn
    /// TUẦN TỰ (windBreathInterval giây/đốt) từ gần boss ra xa → cảm giác luồng gió lan tới trước.
    ///
    /// Hướng cố định (chốt lúc bắt đầu phun) theo hướng nhìn — luồng gió KHÔNG bám theo player khi đã phun,
    /// đúng mô tả "cố định 1 đường thẳng". Mỗi đốt là EnemyAoEDamage (snapToGround tùy prefab).
    /// </summary>
    public class D_WindBreathState : DruidBossState
    {
        private float _elapsed;
        private bool _charged;
        private int _spawned;
        private float _nextSpawnTime;
        private float _dirX;
        private float _baseY;

        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _charged = false;
            _spawned = 0;
            _nextSpawnTime = 0f;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();
            ai.PlayAnim("Attack");
        }

        public override void Update(DruidBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Phase 1: charge (hít hơi) — chốt hướng + cao độ luồng khi charge xong.
            if (!_charged)
            {
                if (_elapsed < ai.windBreathChargeTime) return;
                _charged = true;
                _dirX = ai.AttackLockedFacingDir >= 0 ? 1f : -1f;
                _baseY = ai.transform.position.y + ai.windBreathHeight;
                _nextSpawnTime = _elapsed;
            }

            // Phase 2: spawn từng đốt gió tuần tự dọc đường thẳng.
            if (_spawned < Mathf.Max(1, ai.windBreathSegments))
            {
                if (_elapsed >= _nextSpawnTime && ai.HasStateAuthority)
                {
                    float x = ai.transform.position.x + _dirX * ai.windBreathSpacing * (_spawned + 1);
                    ai.SpawnAoE(ai.WindBeamPrefab, new Vector2(x, _baseY), ai.windBreathDamage);
                    _spawned++;
                    _nextSpawnTime = _elapsed + ai.windBreathInterval;
                }
                return;
            }

            // Phase 3: phun xong → recovery.
            ai.ChangeState(DruidBossAI.RecoveryState);
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
