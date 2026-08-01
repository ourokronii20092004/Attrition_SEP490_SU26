using UnityEngine;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// SKILL 4: Air Burst — boss chơi XONG pha vung tay rồi mới đặt từng mục tiêu.
    /// Mỗi mục tiêu: Pull In (báo trước, damage 0) → chờ airBurstPrepareTime → AirBurst gây damage →
    /// chờ airBurstInterval rồi mới tới mục tiêu kế. Các mục tiêu chỉ rải về phía trước player.
    /// </summary>
    public class D_AirBurstState : DruidBossState
    {
        private enum Phase { AttackWindup, PullIn, Burst, Gap }

        private Phase _phase;
        private float _elapsed;
        private int _spawned;
        private Vector2 _center;
        private Vector2 _currentPoint;
        private float _dirX;
        private float _playerFacingX;

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
                ? (ai.PlayerTarget.position.x >= ai.transform.position.x ? 1f : -1f)
                : (ai.AttackLockedFacingDir >= 0 ? 1f : -1f);
            var targetPlayer = ai.PlayerTarget != null
                ? ai.PlayerTarget.GetComponentInParent<PlayerController>()
                : null;
            _playerFacingX = targetPlayer == null || targetPlayer.IsFacingRight ? 1f : -1f;

            _center = ai.PlayerTarget != null
                ? (Vector2)ai.PlayerTarget.position
                : (Vector2)ai.transform.position + new Vector2(_dirX * 3f, 0f);

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
                    // Đợi boss dùng XONG animation Attack rồi mới có VFX/skill xuất hiện.
                    if (_elapsed < ai.meleeDuration) return;
                    BeginPullIn(ai);
                    return;

                case Phase.PullIn:
                    if (_elapsed < ai.airBurstPrepareTime) return;
                    if (ai.HasStateAuthority)
                        // Burst nằm trước mặt player và đâm ngược vào player.
                        ai.SpawnAoEOriented(ai.AirBurstPrefab, _currentPoint, ai.airBurstDamage, -_playerFacingX);
                    _phase = Phase.Burst;
                    _elapsed = 0f;
                    return;

                case Phase.Burst:
                    // Để clip AirBurst chạy tới gần hết (0.4375s) rồi mới tính gap.
                    if (_elapsed < 0.45f) return;
                    _spawned++;
                    if (_spawned >= Mathf.Max(1, ai.airBurstCount))
                    {
                        ai.ChangeState(DruidBossAI.RecoveryState);
                        return;
                    }
                    _phase = Phase.Gap;
                    _elapsed = 0f;
                    return;

                case Phase.Gap:
                    // Nhịp giữa hai đòn dài hơn để player kịp quan sát và đổi vị trí.
                    if (_elapsed < ai.airBurstInterval) return;
                    BeginPullIn(ai);
                    return;
            }
        }

        private void BeginPullIn(DruidBossAI ai)
        {
            float y = _center.y + 1f;
            // Điểm đầu cũng cách player tối thiểu 1.2 units về phía trước; không spawn ngay tâm khiến
            // mắt đọc thành "đánh sau lưng". Các điểm sau tiếp tục rải xa hơn cùng hướng nhìn player.
            float offX = _playerFacingX * (_spawned == 0
                ? 1.2f
                : Random.Range(1.2f, Mathf.Max(1.21f, ai.airBurstScatter)));
            _currentPoint = new Vector2(_center.x + offX, y);

            if (ai.HasStateAuthority)
                ai.SpawnAoE(ai.AirBurstPullInPrefab, _currentPoint, 0);

            _phase = Phase.PullIn;
            _elapsed = 0f;
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
