using UnityEngine;

namespace Attrition.Gameplay.Enemy.ArchDemon.States
{
    /// <summary>
    /// SKILL 5: Water Splash — đánh NGAY DƯỚI CHÂN mục tiêu, tổng `splashRepeats` (3) lần, LẦN LƯỢT: lần sau
    /// chỉ bắt đầu sau khi lần trước đã kết thúc. Mỗi lần có dấu báo (`waterStartup2Prefab`) hiện trước
    /// `splashStartupLead` giây để player kịp rời chỗ.
    ///
    /// Mỗi lần NGẮM LẠI vị trí player: đó là điểm khác với skill 2 của DemonKin (chốt 1 lần). Vì có dấu báo
    /// trước nửa giây và 3 lần liên tiếp, việc ngắm lại buộc player phải LIÊN TỤC di chuyển thay vì né một
    /// lần rồi đứng yên — nhưng vẫn né được nhờ cửa sổ báo trước.
    /// </summary>
    public class AD_WaterSplashState : ArchDemonBossState
    {
        private enum Phase { Telegraph, Wait }

        private Phase _phase;
        private float _elapsed;
        private int _done;
        private Vector2 _mark;

        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _done = 0;
            ai.StopMovement();
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.PlayAttackAnimNoOrb();   // cắt trước frame 8 → không kéo theo cầu bóng tối

            BeginTelegraph(ai);
        }

        public override void Update(ArchDemonBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            switch (_phase)
            {
                case Phase.Telegraph:
                    // Hết cửa sổ báo trước → nổ tại chỗ đã đánh dấu (KHÔNG cập nhật lại: player phải được
                    // thưởng cho việc rời khỏi dấu).
                    if (_elapsed < ai.splashStartupLead) return;
                    ai.SpawnAoE(ai.WaterSplashPrefab, _mark, ai.splashDamage);
                    _done++;
                    _phase = Phase.Wait;
                    _elapsed = 0f;
                    return;

                case Phase.Wait:
                    if (_elapsed < ai.splashGap) return;
                    if (_done >= Mathf.Max(1, ai.splashRepeats))
                    {
                        ai.ChangeState(ArchDemonBossAI.RecoveryState);
                        return;
                    }
                    // Lần kế tiếp: ngắm lại vị trí player HIỆN TẠI.
                    ai.DetectPlayer();
                    ai.FaceTowardsPlayer();
                    ai.PlayAttackAnimNoOrb();   // cắt trước frame 8 → không kéo theo cầu bóng tối
                    BeginTelegraph(ai);
                    return;
            }
        }

        /// <summary>Đánh dấu chỗ player đang đứng + hiện dấu báo.</summary>
        private void BeginTelegraph(ArchDemonBossAI ai)
        {
            _phase = Phase.Telegraph;
            _elapsed = 0f;

            _mark = ai.PlayerTarget != null
                ? (Vector2)ai.PlayerTarget.position
                : (Vector2)ai.transform.position + new Vector2(ai.DirToPlayerX() * 3f, 0f);

            ai.SpawnAoE(ai.WaterStartup2Prefab, _mark, 0);
        }

        public override void Exit(ArchDemonBossAI ai) => ai.StopMovement();
    }
}
