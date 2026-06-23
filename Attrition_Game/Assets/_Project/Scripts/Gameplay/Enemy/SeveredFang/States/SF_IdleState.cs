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
        private float _idleTime;

        public override void Enter(SeveredFangAI ai)
        {
            ai.StopMovement();
            ai.CurrentState = EnemyState.Patrol;
            ai.NetSpeed = 0f;
            _idleTime = 0f;
        }

        public override void Update(SeveredFangAI ai)
        {
            _idleTime += ai.Runner.DeltaTime;

            ai.DetectPlayer();
            if (ai.PlayerTarget == null)
            {
                ai.StopMovement();
                return;
            }

            ai.FaceTowardsPlayer();

            if (_idleTime < 1.0f)
            {
                ai.StopMovement();
            }
            else
            {
                if (ai.DistanceToPlayer() > ai.meleeRange)
                {
                    float walkSpeed = ai.StatsComp != null ? ai.StatsComp.PatrolSpeed : 3f;
                    ai.MoveTowardsPlayer(walkSpeed);
                }
                else
                {
                    ai.StopMovement();
                }
            }

            // Chờ ít nhất 1.5 giây VÀ hết cooldown skill mới sang Chase
            if (_idleTime >= 1.5f && ai.SkillCooldownTimer.ExpiredOrNotRunning(ai.Runner))
            {
                ai.ChangeState(SeveredFangAI.ChaseState);
            }
        }
    }
}
