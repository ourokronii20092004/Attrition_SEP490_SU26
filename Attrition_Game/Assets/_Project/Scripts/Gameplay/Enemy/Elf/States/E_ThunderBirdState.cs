using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// SKILL 2: Thunder Bird — VẬN SỨC vào mũi tên (6 frame đầu giữ ở đầu cung, animation CHỮNG lại) rồi
    /// bắn ra 1 CHIM SẤM to bay về phía player.
    ///
    /// Cách làm animation "chững": gọi `FreezeAnimation()` trên EnemyAnimation ngay khi vào state (đóng
    /// băng animator tại frame đang giơ cung), giữ suốt `birdChargeTime`, rồi `UnfreezeAnimation()` để các
    /// frame còn lại chạy tiếp lúc bắn. Hai hàm này đã có sẵn trong EnemyAnimation — không cần clip riêng.
    /// Người dựng chỉ cần đặt Animation Event `FreezeAnimation` ở frame 6 nếu muốn khớp chính xác hơn.
    ///
    /// CAO/THẤP ngẫu nhiên: bay CAO (birdHighOffsetY) buộc player NGỒI xuống né; bay THẤP (birdLowOffsetY)
    /// buộc player NHẢY qua. Chọn 50/50 mỗi lần dùng nên player không học vẹt được.
    /// </summary>
    public class E_ThunderBirdState : ElfBossState
    {
        private float _elapsed;
        private bool _fired;
        private bool _unfroze;
        private float _dirX;
        private float _offsetY;

        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _fired = false;
            _unfroze = false;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            // Cao hay thấp — quyết định 1 lần, ở host. Client không cần biết (chỉ host spawn đạn).
            _offsetY = Random.value < 0.5f ? ai.birdHighOffsetY : ai.birdLowOffsetY;

            ai.PlayAnim("Attack");
            ai.FreezeAnim();     // chững lại ở đầu cung để "vận sức"
        }

        public override void Update(ElfBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Hết thời gian vận sức → rã đông animation cho các frame bắn chạy tiếp.
            if (!_unfroze && _elapsed >= ai.birdChargeTime)
            {
                _unfroze = true;
                ai.UnfreezeAnim();
            }

            if (!_fired && _elapsed >= ai.birdChargeTime)
            {
                _fired = true;
                if (ai.HasStateAuthority)
                {
                    Vector2 pos = (Vector2)ai.transform.position + new Vector2(_dirX * 1.2f, _offsetY);
                    ai.SpawnProjectile(ai.ThunderBirdPrefab, pos, new Vector2(_dirX, 0f),
                                       ai.birdDamage, ai.birdSpeed);
                }
            }

            if (_fired && _elapsed >= ai.birdChargeTime + 0.4f)
                ai.ChangeState(ElfBossAI.RecoveryState);
        }

        public override void Exit(ElfBossAI ai)
        {
            // An toàn: nếu state bị cắt giữa lúc đang đóng băng (boss trúng knockback), phải rã đông,
            // nếu không animator đứng cứng vĩnh viễn.
            ai.UnfreezeAnim();
            ai.StopMovement();
        }
    }
}
