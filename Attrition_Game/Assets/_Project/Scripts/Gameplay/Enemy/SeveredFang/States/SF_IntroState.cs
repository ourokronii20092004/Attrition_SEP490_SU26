using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// State Intro: Boss walk ra giữa phòng, dừng lại, nói chuyện, rồi vào combat.
    /// </summary>
    public class SF_IntroState : SeveredFangState
    {
        private float _walkTime;
        private bool _dialogueStarted;

        public override void Enter(SeveredFangAI ai)
        {
            _walkTime = 0f;
            _dialogueStarted = false;
            
            // Boss walk về phía player
            ai.DetectPlayer();
            float dir = ai.PlayerTarget != null ? Mathf.Sign(ai.PlayerTarget.position.x - ai.Rb.position.x) : -1f;
            ai.NetFacingDir = dir;
            float walkSpd = ai.StatsComp != null ? ai.StatsComp.PatrolSpeed : 3f;
            ai.NetSpeed = walkSpd;
            ai.Rb.linearVelocity = new Vector2(dir * walkSpd, ai.Rb.linearVelocity.y);
            ai.CurrentState = EnemyState.Patrol; // Play walk anim
        }

        public override void Update(SeveredFangAI ai)
        {
            if (_dialogueStarted) return;

            _walkTime += ai.Runner.DeltaTime;
            
            // Walk khoảng 2 giây rồi dừng lại nói chuyện
            if (_walkTime >= 2f)
            {
                ai.StopMovement();
                ai.CurrentState = EnemyState.Patrol;
                _dialogueStarted = true;

                if (ai.introDialogue != null)
                {
                    Attrition.Data.DialogueEvents.OnOpenCustomDialogue?.Invoke(ai.introDialogue, () => 
                    {
                        // Khi thoại đóng, vào combat
                        ai.ChangeState(SeveredFangAI.IdleState);
                    });
                }
                else
                {
                    // Nếu không có thoại, đánh luôn
                    ai.ChangeState(SeveredFangAI.IdleState);
                }
            }
        }

        public override void Exit(SeveredFangAI ai)
        {
            ai.StopMovement();
        }
    }
}
