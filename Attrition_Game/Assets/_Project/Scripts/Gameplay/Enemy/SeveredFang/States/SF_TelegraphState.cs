using UnityEngine;
using Fusion;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// State cảnh báo (Telegraph): Boss đứng im, nhấp nháy đỏ 0.5s trước khi tung đòn.
    /// Dùng EnemyAnimation.SetTelegraph() có sẵn trong hệ thống.
    /// </summary>
    public class SF_TelegraphState : SeveredFangState
    {
        private float _telegraphTimer;

        public override void Enter(SeveredFangAI ai)
        {
            _telegraphTimer = 0f;
            ai.StopMovement();
            ai.CurrentState = EnemyState.Telegraphing;

            // Bật nhấp nháy đỏ bằng hệ thống telegraph có sẵn
            if (ai.AnimComp != null)
            {
                ai.AnimComp.SetTelegraph(true);
            }
        }

        public override void Update(SeveredFangAI ai)
        {
            _telegraphTimer += ai.Runner.DeltaTime;

            if (_telegraphTimer >= 0.5f) // Thời gian cảnh báo: 0.5s
            {
                // Tắt nhấp nháy
                if (ai.AnimComp != null)
                {
                    ai.AnimComp.SetTelegraph(false);
                }

                if (ai.NextAttackState != null)
                {
                    ai.ChangeState(ai.NextAttackState);
                }
                else
                {
                    ai.ChangeState(SeveredFangAI.IdleState);
                }
            }
        }

        public override void Exit(SeveredFangAI ai)
        {
            // Đảm bảo tắt telegraph nếu bị ngắt giữa chừng
            if (ai.AnimComp != null)
            {
                ai.AnimComp.SetTelegraph(false);
            }
        }
    }
}
