using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// SKILL 1: Thunder Arrow — bắn `arrowCount` (3) mũi tên sấm bay SONG SONG theo chiều ngang về hướng
    /// player. Các mũi lệch nhau `arrowGapY` theo TRỤC DỌC nên có khe ở giữa để player nhảy vào né.
    ///
    /// Bắn CÙNG LÚC (không rải thời gian) — đó là điều khiến chúng "song song" và tạo khe cố định. Hướng
    /// bay là ngang thuần (dir = (±1, 0)), không ngắm chéo theo player: chéo sẽ làm khe biến dạng theo
    /// khoảng cách, mất ý nghĩa "nhảy vào giữa".
    /// </summary>
    public class E_ThunderArrowState : ElfBossState
    {
        private float _elapsed;
        private bool _fired;
        private float _dirX;

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

            ai.PlayAnim("Attack");
        }

        public override void Update(ElfBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            float releaseTime = Mathf.Max(ai.arrowChargeTime, ElfBossAI.SkillAttackWindup);
            if (!_fired && _elapsed >= releaseTime)
            {
                _fired = true;
                ai.PlayAnim("Idle"); // skill ra đúng lúc Attack kết thúc, không giữ frame cuối
                if (ai.HasStateAuthority) FireVolley(ai);
            }

            if (_fired && _elapsed >= releaseTime + 0.35f)
                ai.ChangeState(ElfBossAI.RecoveryState);
        }

        /// <summary>Bắn cả chùm cùng lúc, xếp đối xứng quanh tâm boss theo trục dọc.</summary>
        private void FireVolley(ElfBossAI ai)
        {
            int n = Mathf.Max(1, ai.arrowCount);
            Vector2 origin = ai.transform.position;
            Vector2 dir = new Vector2(_dirX, 0f);

            // n=3, gap=1.8 → offset -1.8, 0, +1.8 (giữa là ngang tâm boss).
            float startY = -(n - 1) * 0.5f * ai.arrowGapY;

            for (int i = 0; i < n; i++)
            {
                Vector2 pos = origin + new Vector2(_dirX * 1.1f, startY + i * ai.arrowGapY);
                ai.SpawnProjectile(ai.ThunderArrowPrefab, pos, dir, ai.arrowDamage, ai.arrowSpeed);
            }
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }
}
