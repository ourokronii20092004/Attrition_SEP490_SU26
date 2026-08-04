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
            // BÁM LẠI vị trí player MỖI ĐÒN, không dùng tâm đã chốt từ Enter().
            //
            // VÌ SAO: chuỗi có airBurstCount (mặc định 6) đòn, mỗi đòn mất
            // airBurstPrepareTime (0.8) + 0.45 + airBurstInterval (0.55) ≈ 1.8s → cả skill dài ~11 GIÂY.
            // Chốt tâm một lần lúc Enter nghĩa là 5 đòn sau đều nổ ở chỗ player đã rời khỏi từ lâu.
            // Đây chính là lỗi "airburst spawn cách quá xa player nên không gây được sát thương".
            //
            // Vẫn né được: PullIn (damage 0) hiện TRƯỚC airBurstPrepareTime giây tại đúng điểm sắp nổ, nên
            // cửa sổ né nằm ở đó — khác ThunderSplash (đòn dịch chuyển tức thì nên phải chốt đích từ đầu).
            if (ai.PlayerTarget != null)
            {
                _center = ai.PlayerTarget.position;
                var pc = ai.PlayerTarget.GetComponentInParent<PlayerController>();
                _playerFacingX = pc == null || pc.IsFacingRight ? 1f : -1f;
            }

            // Lệch ra TRƯỚC MẶT player (giữ ý "đâm từ trước tới", không phải sau lưng) nhưng phải nằm
            // TRONG bán kính AoE của AirBurst — prefab để radius 1.6. Trước đây lệch ngang tới
            // airBurstScatter (4) cộng lệch cao 1 → cách tâm 4.1 units, quá bán kính nên trượt chắc chắn.
            _currentPoint = new Vector2(_center.x + _playerFacingX * FrontOffsetX,
                                        _center.y + FrontOffsetY);

            if (ai.HasStateAuthority)
                ai.SpawnAoE(ai.AirBurstPullInPrefab, _currentPoint, 0);

            _phase = Phase.PullIn;
            _elapsed = 0f;
        }

        /// <summary>
        /// Lệch của điểm nổ so với tâm player. Khoảng cách tổng (~0.96) phải NHỎ HƠN bán kính AoE của
        /// AirBurst (1.6 trên prefab), nếu không đòn có hình đúng hướng mà không bao giờ trúng.
        /// </summary>
        private const float FrontOffsetX = 0.9f;
        private const float FrontOffsetY = 0.35f;

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
