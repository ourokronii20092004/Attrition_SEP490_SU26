using UnityEngine;

namespace Attrition.Gameplay.Enemy.ArchDemon.States
{
    /// <summary>
    /// SKILL 2: Water Ball x3 — tung 3 quả cầu nước TỐC ĐỘ NHANH: 1 quả từ BOSS bay về phía player, và
    /// 2 quả xuất hiện ở PHÍA ĐỐI DIỆN XA BOSS rồi bay NGƯỢC VỀ phía boss.
    ///
    /// Ý nghĩa: player đứng giữa boss và điểm xa sẽ bị KẸP từ hai đầu. Hai quả phía đối diện lệch nhau
    /// `ballFarGapY` theo trục dọc nên vẫn còn khe để lách/nhảy — nếu xếp cùng độ cao thì thành đòn không
    /// thể tránh.
    ///
    /// Cả 3 bắn CÙNG LÚC và đi nhanh (`ballSpeed` 18) theo yêu cầu "tốc độ nhanh".
    ///
    /// Hiệu ứng va đập (WaterBall - Impact) KHÔNG spawn ở đây mà do `EnemyProjectile.impactPrefab` trên
    /// chính prefab cầu nước lo — chỉ bản thân viên đạn biết nó trúng player hay tới rìa map. Tool setup
    /// gán sẵn ô đó.
    /// </summary>
    public class AD_WaterBallState : ArchDemonBossState
    {
        private float _elapsed;
        private bool _fired;
        private float _dirX;

        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _fired = false;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            ai.PlayAttackAnimNoOrb();   // cắt clip trước frame 8 — skill nước không kèm cầu bóng tối
        }

        public override void Update(ArchDemonBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            if (!_fired && _elapsed >= ai.ballChargeTime)
            {
                _fired = true;
                FireAll(ai);
            }

            if (_fired && _elapsed >= ai.ballChargeTime + 0.4f)
                ai.ChangeState(ArchDemonBossAI.RecoveryState);
        }

        private void FireAll(ArchDemonBossAI ai)
        {
            Vector2 origin = ai.transform.position;

            // 1) Quả từ BOSS bay về phía player.
            ai.SpawnProjectile(ai.WaterBallPrefab, origin + new Vector2(_dirX * 1.3f, 0.3f),
                               new Vector2(_dirX, 0f), ai.ballDamage, ai.ballSpeed);

            // 2+3) Hai quả ở PHÍA ĐỐI DIỆN (xa boss theo hướng bắn), bay NGƯỢC LẠI về boss.
            // Kẹp trong biên phòng để không spawn ngoài tường.
            BossRoomBounds.GetHorizontal(origin, ai.roomFallbackHalfWidth, out float minX, out float maxX);
            float farX = Mathf.Clamp(origin.x + _dirX * ai.ballFarDistance, minX + 0.5f, maxX - 0.5f);
            Vector2 backDir = new Vector2(-_dirX, 0f);

            float halfGap = ai.ballFarGapY * 0.5f;
            ai.SpawnProjectile(ai.WaterBallPrefab, new Vector2(farX, origin.y + halfGap),
                               backDir, ai.ballDamage, ai.ballSpeed);
            ai.SpawnProjectile(ai.WaterBallPrefab, new Vector2(farX, origin.y - halfGap),
                               backDir, ai.ballDamage, ai.ballSpeed);
        }

        public override void Exit(ArchDemonBossAI ai) => ai.StopMovement();
    }
}
