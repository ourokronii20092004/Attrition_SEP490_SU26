using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 2: Wind Breath — boss hít hơi (charge) rồi phun 1 LUỒNG GIÓ DÀI theo đường thẳng ngang,
    /// kéo dài về phía player hết sân rồi biến mất. windBreathSegments đốt gió AoE spawn TUẦN TỰ
    /// (windBreathInterval giây/đốt) từ gần boss ra xa → thấy rõ đốt 1-2-3-4-5 lan tới.
    ///
    /// HƯỚNG: chốt theo VỊ TRÍ PLAYER lúc bắt đầu phun (không dùng facing — facing có deadZone 1.2 nên khi
    /// player đứng gần/vừa vượt qua thì facing còn hướng cũ → luồng gió phun ngược, đó là lý do "không trúng").
    /// CAO ĐỘ: lấy theo CHÂN PLAYER, không theo chân boss (boss có thể đứng cao/thấp hơn bậc nền).
    /// Đốt đầu bắt đầu ngay sát boss (i = 0) để luồng liền mạch, không hở 2 units như trước.
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
                // Không cho beam xuất hiện trước khi animation Attack của boss chạy xong.
                if (_elapsed < Mathf.Max(ai.windBreathChargeTime, ai.meleeDuration)) return;
                _charged = true;
                // Hướng theo VỊ TRÍ player (không dùng facing — deadZone làm facing lệch khi player ở gần).
                _dirX = ai.PlayerTarget != null
                    ? (ai.PlayerTarget.position.x >= ai.transform.position.x ? 1f : -1f)
                    : (ai.AttackLockedFacingDir >= 0 ? 1f : -1f);
                // Cập nhật facing để sprite quay ĐÚNG HƯỚNG beam; không cập nhật ở Enter vì lúc đó
                // player có thể đang ở sau boss nhưng boss đang charge, sẽ lật sprite ngược.
                ai.NetFacingDir = _dirX;
                ai.AttackLockedFacingDir = _dirX;
                // Cao độ theo CHÂN PLAYER để luồng gió đi ngang qua người player.
                float feetY = ai.PlayerTarget != null ? ai.PlayerTarget.position.y : ai.transform.position.y;
                _baseY = feetY + ai.windBreathHeight;
                _nextSpawnTime = _elapsed;
            }

            // Phase 2: spawn từng đốt gió tuần tự dọc đường thẳng.
            if (_spawned < Mathf.Max(1, ai.windBreathSegments))
            {
                if (_elapsed >= _nextSpawnTime && ai.HasStateAuthority)
                {
                    // Đốt 0 sát boss → luồng liền mạch, không hở khoảng trước mặt boss.
                    float x = ai.transform.position.x + _dirX * (0.8f + ai.windBreathSpacing * _spawned);
                    ai.SpawnAoEOriented(ai.WindBeamPrefab, new Vector2(x, _baseY),
                                        ai.windBreathDamage, _dirX);
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
