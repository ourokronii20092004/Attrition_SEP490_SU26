using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    public class SF_IdleState : SeveredFangState
    {
        private float _idleTime;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Patrol; // Play Walk anim
            _idleTime = 0f;
            ai.DetectPlayer();
        }

        public override void Update(SeveredFangAI ai)
        {
            _idleTime += ai.Runner.DeltaTime;

            // Walk chậm rãi về phía player trong thời gian nghỉ
            if (ai.PlayerTarget != null)
            {
                float xDiff = ai.PlayerTarget.position.x - ai.Rb.position.x;

                // Chỉ quay mặt + đi khi player LỆCH HẲN sang một bên (deadzone). Player đứng giữa/đè boss
                // → xDiff ~0 → KHÔNG lật hướng, đứng yên (tránh boss xoay + giật qua lại liên tục).
                ai.FaceTowardsPlayer(); // đã có deadzone rộng cho boss

                float deadZone = 1.2f;
                if (Mathf.Abs(xDiff) > deadZone)
                {
                    float dir = ai.NetFacingDir > 0 ? 1f : -1f;
                    float walkSpd = ai.StatsComp.PatrolSpeed * 0.7f; // Đi chậm rãi
                    ai.NetSpeed = walkSpd;
                    ai.Rb.linearVelocity = new Vector2(dir * walkSpd, ai.Rb.linearVelocity.y);
                }
                else
                {
                    ai.StopMovement(); // đủ gần theo trục X → đứng yên, không giật
                }
            }
            else
            {
                ai.DetectPlayer();
            }

            if (_idleTime >= 1.5f) // Thời gian nghỉ ngơi giữa các đòn đánh
            {
                ai.StopMovement();
                ChooseNextAttack(ai);
            }
        }

        private void ChooseNextAttack(SeveredFangAI ai)
        {
            ai.DetectPlayer();
            if (ai.PlayerTarget == null)
            {
                ai.ChangeState(SeveredFangAI.IdleState);
                return;
            }

            float dist = Vector2.Distance(ai.Rb.position, ai.PlayerTarget.position);

            SeveredFangState nextState = null;

            if (dist <= ai.meleeRange)
            {
                nextState = SeveredFangAI.MeleeAttackState;
            }
            else
            {
                float r = Random.value;
                if (r < 0.33f) nextState = SeveredFangAI.DashExplosionState;
                else if (r < 0.66f) nextState = SeveredFangAI.SheatheFireballState;
                else nextState = SeveredFangAI.ShortDashFireboltState;
            }

            // Gán NextAttack và vào trạng thái báo động (Telegraph)
            ai.NextAttackState = nextState;
            ai.ChangeState(SeveredFangAI.TelegraphState);
        }
    }
}
