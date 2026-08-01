using UnityEngine;

namespace Attrition.Gameplay.Enemy.DemonKin.States
{
    /// <summary>
    /// SKILL 1: Earth Barrage — boss tung LIÊN TIẾP `barrageCount` (4) đòn đánh, mỗi đòn phóng 1 viên đất
    /// bay về phía trước tới hết chiều ngang phòng.
    ///
    /// Mỗi đòn cách nhau `barrageInterval` (0.45s) — đủ để player thấy từng viên và nhảy né, thay vì 4 viên
    /// ra cùng lúc thành một bức tường không thể tránh.
    ///
    /// "Đi hết chiều ngang room": lifetime của viên đất được tính từ chiều rộng phòng thật (BossRoomBounds)
    /// chia cho tốc độ, rồi ghi lên EnemyProjectile lúc spawn. Không tự tính thì prefab lifetime cố định
    /// (3s) sẽ khiến đạn tan giữa phòng ở phòng rộng, hoặc bay dai vô nghĩa ở phòng hẹp.
    ///
    /// Hướng bay CHỐT MỘT LẦN ở Enter: nếu cập nhật mỗi đòn thì player chạy vòng qua boss sẽ khiến 4 viên
    /// toả 2 phía, mất cảm giác "một loạt đòn".
    /// </summary>
    public class DK_EarthBarrageState : DemonKinBossState
    {
        private float _elapsed;
        private int _fired;
        private float _nextFireTime;
        private float _dirX;
        private float _travelDistance;

        public override void Enter(DemonKinBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _fired = 0;
            _nextFireTime = Mathf.Max(ai.barrageChargeTime, DemonKinBossAI.SkillAttackWindup);

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            // Quãng đường cần bay = từ boss tới biên phòng theo hướng bắn.
            BossRoomBounds.GetHorizontal(ai.transform.position, ai.roomFallbackHalfWidth,
                                         out float minX, out float maxX);
            float bossX = ai.transform.position.x;
            _travelDistance = _dirX > 0 ? Mathf.Max(1f, maxX - bossX) : Mathf.Max(1f, bossX - minX);

            ai.PlayAnim("Attack");
        }

        public override void Update(DemonKinBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            if (_fired < ai.barrageCount && _elapsed >= _nextFireTime)
            {
                if (_fired == 0) ai.PlayAnim("Idle");
                if (ai.HasStateAuthority)
                {
                    Vector2 pos = (Vector2)ai.transform.position + new Vector2(_dirX * 1.2f, 0.1f);
                    ai.SpawnProjectileRanged(ai.EarthProjectilePrefab, pos, new Vector2(_dirX, 0f),
                                             ai.barrageDamage, ai.barrageSpeed, _travelDistance);
                }
                _fired++;
                _nextFireTime = _elapsed + ai.barrageInterval;

                // Một animation Attack hoàn chỉnh mở đầu cả loạt; không restart giữa từng viên.
            }

            if (_fired >= ai.barrageCount && _elapsed >= _nextFireTime)
                ai.ChangeState(DemonKinBossAI.RecoveryState);
        }

        public override void Exit(DemonKinBossAI ai) => ai.StopMovement();
    }
}
