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

            // Walk ngắn (~0.8s) rồi dừng lại nói chuyện — vào trận nhanh hơn, đỡ lê thê.
            if (_walkTime >= 0.8f)
            {
                ai.StopMovement();
                ai.CurrentState = EnemyState.Patrol;
                _dialogueStarted = true;

                if (ai.introDialogue != null)
                {
                    // Bắn RPC cho MỌI máy cùng mở thoại. Trước đây gọi thẳng DialogueEvents ở đây,
                    // nhưng state machine CHỈ chạy trên host (EnemyController.FUN return sớm khi
                    // !HasStateAuthority) → client không bao giờ thấy thoại mở đầu.
                    // RPC tự chuyển sang IdleState (chỉ host) khi thoại đóng.
                    ai.BroadcastIntroDialogue();
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
