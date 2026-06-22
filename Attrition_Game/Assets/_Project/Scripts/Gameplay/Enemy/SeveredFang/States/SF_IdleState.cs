using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// Trạng thái nghỉ — Boss đứng yên, chờ cooldown hết rồi chuyển sang Chase.
    /// Tương tự "Idle" phase trong boss Hollow Knight: boss dừng lại 1-2 giây
    /// giữa các đợt tấn công để player kịp phản ứng.
    /// </summary>
    public class SF_IdleState : SeveredFangState
    {
        public override void Enter(SeveredFangAI ai)
        {
            ai.StopMovement();
            ai.CurrentState = EnemyState.Patrol; // Cho EnemyController biết boss "bình thường"
            ai.NetSpeed = 0f;
        }

        public override void Update(SeveredFangAI ai)
        {
            ai.StopMovement();

            // Tìm player
            ai.DetectPlayer();
            if (ai.PlayerTarget == null) return;

            ai.FaceTowardsPlayer();

            // Chờ cooldown skill hết → sang Chase
            if (ai.SkillCooldownTimer.ExpiredOrNotRunning(ai.Runner))
            {
                ai.ChangeState(SeveredFangAI.ChaseState);
            }
        }
    }
}
