using UnityEngine;

namespace Attrition.Gameplay.Enemy.ArchDemon.States
{
    /// <summary>
    /// SKILL 1: Dark Orb — tung quả cầu bóng tối về phía trước gây sát thương.
    ///
    /// KHÔNG DÙNG PREFAB: quả cầu đã được VẼ SẴN trong clip `ArchDemon_BasicAttack` (bung ra ở frame 8 —
    /// xem <see cref="ArchDemonBossAI.DarkOrbFrameTime"/>). Vì vậy state này chỉ chạy animation đầy đủ rồi
    /// quét sát thương ĐÚNG lúc cầu rời tay, không spawn NetworkObject nào.
    ///
    /// Tầm đánh: art chỉ vẽ cầu tới mép sprite (frame 9 trở đi cầu đã ra ngoài khung, không còn hình) nên
    /// vùng sát thương là một hộp phía trước boss dài `orbRange`, KHÔNG phải đạn bay hết phòng — đạn bay xa
    /// mà không có hình sẽ thành "sát thương vô hình".
    ///
    /// Đây là skill nhẹ nhất, dùng làm nhịp nghỉ giữa các skill nước để player có cửa sổ đánh trả.
    /// </summary>
    public class AD_DarkOrbState : ArchDemonBossState
    {
        private float _elapsed;
        private bool _fired;
        private float _dirX;

        private readonly Collider2D[] _results = new Collider2D[8];
        private readonly System.Collections.Generic.HashSet<IDamageable> _done =
            new System.Collections.Generic.HashSet<IDamageable>();

        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _fired = false;
            _done.Clear();

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            // Animation ĐẦY ĐỦ (không cắt) — đây là skill duy nhất cần quả cầu trong clip.
            ai.PlayAttackAnimWithOrb();
        }

        public override void Update(ArchDemonBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Quét sát thương đúng frame cầu rời tay (1.1667s) — khớp hình với đòn đánh.
            if (!_fired && _elapsed >= ArchDemonBossAI.DarkOrbFrameTime && ai.HasStateAuthority)
            {
                _fired = true;

                Vector2 center = (Vector2)ai.transform.position
                                 + new Vector2(_dirX * ai.orbRange * 0.5f, 0.3f);
                Vector2 size = new Vector2(ai.orbRange, 2.6f);
                var filter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = LayerMask.GetMask("Player"),
                    useTriggers = false
                };
                int n = ai.Runner.GetPhysicsScene2D().OverlapBox(center, size, 0f, filter, _results);
                for (int i = 0; i < n; i++)
                {
                    var dmg = _results[i] != null ? _results[i].GetComponentInParent<IDamageable>() : null;
                    if (dmg == null || dmg.IsDead || _done.Contains(dmg)) continue;
                    _done.Add(dmg);
                    Vector2 push = new Vector2(_dirX, 0.35f).normalized;
                    dmg.TakeDamage(ai.orbDamage, push, 6f, Attrition.Core.DamageType.Magic);
                }
            }

            // Chờ animation chạy nốt (clip dài 2s) rồi mới sang recovery, tránh cắt giữa động tác.
            if (_fired && _elapsed >= ai.orbTotalTime)
                ai.ChangeState(ArchDemonBossAI.RecoveryState);
        }

        public override void Exit(ArchDemonBossAI ai) => ai.StopMovement();
    }
}
