using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// Trạng thái phục hồi (Recovery) sau mỗi đòn đánh/skill.
    /// Cho phép chạy nốt animation khựng lại sau đòn, và gán cooldown cho skill.
    /// Sau đó trở về IdleState.
    /// </summary>
    public class SF_RecoveryState : SeveredFangState
    {
        private float _elapsedTime;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Recovery;
            _elapsedTime = 0f;
            ai.StopMovement();

            // Set global skill cooldown
            if (ai.HasStateAuthority)
            {
                ai.SkillCooldownTimer = TickTimer.CreateFromSeconds(ai.Runner, ai.skillCooldown);
            }
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsedTime += ai.Runner.DeltaTime;
            ai.StopMovement();

            if (_elapsedTime >= ai.recoveryTime)
            {
                ai.ChangeState(SeveredFangAI.IdleState);
            }
        }
    }
}
