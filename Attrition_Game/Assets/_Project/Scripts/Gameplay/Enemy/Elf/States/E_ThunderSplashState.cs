using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// SKILL 4: Thunder Splash — chuỗi 4 pha theo đúng yêu cầu:
    /// hình dáng ban đầu → HOÁ SẤM (biến mất) → hiện lại NGAY CHỖ PLAYER đang bị ngắm → bắn 2 mũi tên
    /// sấm sang 2 HƯỚNG (mỗi hướng 1 mũi, dùng prefab của skill 1).
    ///
    /// Bắn 2 hướng chứ không nhắm player: sau khi boss nhảy tới sát người, player thường sẽ chạy sang một
    /// bên — 2 mũi ngược chiều bịt cả hai đường thoát ngang, buộc player phải NHẢY. Đó là ý nghĩa của skill.
    ///
    /// COOP: `TeleportTo` ghi `rb.position` ở host, NetworkTransform sync xuống client — không cần RPC vị trí.
    /// Vệt sấm (thunderSplashPrefab) spawn ở CẢ điểm đi và điểm đến để client thấy rõ boss đã dời chỗ.
    /// </summary>
    public class E_ThunderSplashState : ElfBossState
    {
        // Pha của skill — tách enum cho dễ đọc thay vì đếm mốc thời gian rải rác.
        private enum Phase { Vanish, Arrive, Shoot, Done }

        private Phase _phase;
        private float _elapsed;
        private Vector2 _destination;

        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _phase = Phase.Vanish;
            _elapsed = 0f;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            // Chốt điểm đến NGAY BÂY GIỜ (chỗ player đang bị ngắm), không đuổi theo trong lúc dịch chuyển —
            // nếu cập nhật liên tục thì player không bao giờ né được, thành đòn không thể tránh.
            if (ai.PlayerTarget != null)
            {
                float side = ai.PlayerTarget.position.x > ai.transform.position.x ? 1f : -1f;
                _destination = (Vector2)ai.PlayerTarget.position + new Vector2(-side * ai.splashArriveOffsetX, 0f);
            }
            else _destination = ai.transform.position;

            ai.PlayAnim("Skill");

            // Vệt sấm tại chỗ ĐI (báo cho player biết boss vừa rời khỏi đây).
            if (ai.HasStateAuthority)
                ai.SpawnAoE(ai.ThunderSplashPrefab, ai.transform.position, ai.splashDamage);
        }

        public override void Update(ElfBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            switch (_phase)
            {
                case Phase.Vanish:
                    if (_elapsed < ai.splashVanishTime) return;
                    // Dời chỗ + vệt sấm tại điểm ĐẾN.
                    if (ai.HasStateAuthority)
                    {
                        ai.TeleportTo(_destination);
                        ai.SpawnAoE(ai.ThunderSplashPrefab, _destination, ai.splashDamage);
                    }
                    ai.PlayAnim("Idle");     // trở lại hình dáng ban đầu
                    _phase = Phase.Arrive;
                    _elapsed = 0f;
                    return;

                case Phase.Arrive:
                    // Đứng lại một nhịp cho player thấy boss đã hiện hình trước khi bị bắn.
                    if (_elapsed < ai.splashAppearTime + ai.splashShootDelay) return;
                    if (ai.HasStateAuthority) FireBothWays(ai);
                    ai.PlayAnim("Attack");
                    _phase = Phase.Shoot;
                    _elapsed = 0f;
                    return;

                case Phase.Shoot:
                    if (_elapsed < 0.35f) return;
                    _phase = Phase.Done;
                    ai.ChangeState(ElfBossAI.RecoveryState);
                    return;
            }
        }

        /// <summary>Mỗi hướng 1 mũi tên sấm — dùng lại prefab skill 1 theo yêu cầu.</summary>
        private void FireBothWays(ElfBossAI ai)
        {
            Vector2 origin = ai.transform.position;
            ai.SpawnProjectile(ai.ThunderArrowPrefab, origin + new Vector2(1.1f, 0f),
                               Vector2.right, ai.arrowDamage, ai.arrowSpeed);
            ai.SpawnProjectile(ai.ThunderArrowPrefab, origin + new Vector2(-1.1f, 0f),
                               Vector2.left, ai.arrowDamage, ai.arrowSpeed);
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }
}
