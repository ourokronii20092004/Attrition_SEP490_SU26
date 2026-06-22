using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// Trạng thái đuổi — Boss đi bộ lại gần player.
    /// Khi đủ gần → chọn ngẫu nhiên 1 skill để thi triển (giống Hollow Knight:
    /// boss tiến lại gần rồi mới ra đòn, không đánh từ xa vô lý).
    /// </summary>
    public class SF_ChaseState : SeveredFangState
    {
        /// <summary>Khoảng cách đủ gần để bắt đầu ra skill.</summary>
        private const float SkillEngageRange = 6f;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Chase;
        }

        public override void Update(SeveredFangAI ai)
        {
            ai.DetectPlayer();
            if (ai.PlayerTarget == null)
            {
                // Mất target → về idle
                ai.ChangeState(SeveredFangAI.IdleState);
                return;
            }

            ai.FaceTowardsPlayer();

            float dist = ai.DistanceToPlayer();
            float cSpeed = ai.StatsComp != null ? ai.StatsComp.ChaseSpeed : 5f;

            if (dist <= SkillEngageRange)
            {
                // Đủ gần → chọn skill ngẫu nhiên
                ai.StopMovement();
                ai.PickRandomSkill();
            }
            else
            {
                // Còn xa → tiến lại
                ai.MoveTowardsPlayer(cSpeed);
            }
        }

        public override void Exit(SeveredFangAI ai)
        {
            ai.StopMovement();
        }
    }
}
