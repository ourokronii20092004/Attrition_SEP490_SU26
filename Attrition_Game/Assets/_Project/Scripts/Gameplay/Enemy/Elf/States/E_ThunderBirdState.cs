using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// SKILL 2: Thunder Bird — boss chạy trọn animation Attack rồi bắn 1 chim sấm lớn về phía player.
    /// `birdChargeTime` vẫn có thể kéo dài windup, nhưng không được ngắn hơn clip Attack 0.85s.
    ///
    /// CAO/THẤP ngẫu nhiên: bay CAO (birdHighOffsetY) buộc player NGỒI xuống né; bay THẤP (birdLowOffsetY)
    /// buộc player NHẢY qua. Chọn 50/50 mỗi lần dùng nên player không học vẹt được.
    /// </summary>
    public class E_ThunderBirdState : ElfBossState
    {
        private float _elapsed;
        private bool _fired;
        private float _dirX;
        private float _offsetY;

        public override void Enter(ElfBossAI ai)
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

            // Cao hay thấp — quyết định 1 lần, ở host. Client không cần biết (chỉ host spawn đạn).
            _offsetY = Random.value < 0.5f ? ai.birdHighOffsetY : ai.birdLowOffsetY;

            ai.PlayAnim("Attack");
        }

        public override void Update(ElfBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            float releaseTime = Mathf.Max(ai.birdChargeTime, ElfBossAI.SkillAttackWindup);

            if (!_fired && _elapsed >= releaseTime)
            {
                _fired = true;
                ai.PlayAnim("Idle");
                if (ai.HasStateAuthority)
                {
                    Vector2 pos = (Vector2)ai.transform.position + new Vector2(_dirX * 1.2f, _offsetY);
                    ai.SpawnProjectile(ai.ThunderBirdPrefab, pos, new Vector2(_dirX, 0f),
                                       ai.birdDamage, ai.birdSpeed);
                }
            }

            if (_fired && _elapsed >= releaseTime + 0.4f)
                ai.ChangeState(ElfBossAI.RecoveryState);
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }
}
