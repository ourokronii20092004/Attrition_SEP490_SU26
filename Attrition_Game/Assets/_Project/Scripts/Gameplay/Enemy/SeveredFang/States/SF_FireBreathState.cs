using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// SKILL 4: Fire Breath — Boss ĐỨNG YÊN vung kiếm (trigger anim "FireBreath"), rồi tạo 5-6 vệt lửa
    /// (FireExplosion) LAN TUẦN TỰ trên mặt đất về phía player. Mỗi vệt cách nhau fireBreathSpacing (units)
    /// và fireBreathInterval (giây) → tạo cảm giác lửa "chạy" tới trước, player phải né/nhảy.
    ///
    /// Chưa có animation riêng cho vệt lửa → tái dùng FireExplosion prefab (tự hạ xuống mặt đất qua
    /// EnemyAoEDamage.SnapToGround). Chỉ host spawn; damage host-authoritative trong AoE.
    /// </summary>
    public class SF_FireBreathState : SeveredFangState
    {
        private float _elapsed;
        private float _nextStreakTime;
        private int _spawned;
        private bool _charging;
        private float _dir;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _nextStreakTime = ai.fireBreathChargeTime; // vệt đầu tiên xuất hiện sau charge
            _spawned = 0;
            _charging = true;

            // Chốt hướng vệt lửa về phía player.
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            _dir = ai.PlayerTarget != null
                ? (ai.PlayerTarget.position.x > ai.transform.position.x ? 1f : -1f)
                : (ai.NetFacingDir > 0 ? 1f : -1f);
            ai.AttackLockedFacingDir = _dir;
            ai.NetFacingDir = _dir;

            ai.StopMovement();
            ai.PlayFireBreathAnim(); // trigger anim mới "FireBreath"
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;

            // Đứng yên suốt skill (chỉ vung kiếm tại chỗ).
            ai.StopMovement();

            if (!ai.HasStateAuthority)
            {
                CheckDone(ai);
                return;
            }

            // Spawn từng vệt lửa lan xa dần theo thời gian.
            if (_spawned < ai.fireBreathStreakCount && _elapsed >= _nextStreakTime)
            {
                // Vệt thứ i cách boss (i+1)*spacing về phía player → lửa "chạy" ra xa.
                float offsetX = _dir * ai.fireBreathSpacing * (_spawned + 1);
                Vector2 pos = (Vector2)ai.transform.position + new Vector2(offsetX, 0.2f);
                ai.SpawnFireExplosion(pos, ai.fireBreathDamage); // tự hạ xuống mặt đất
                _spawned++;
                _nextStreakTime = _elapsed + ai.fireBreathInterval;
                _charging = false;
            }

            CheckDone(ai);
        }

        private void CheckDone(SeveredFangAI ai)
        {
            // Xong khi đã spawn đủ vệt + chờ nốt 1 nhịp cho vệt cuối nổ.
            if (!_charging && _spawned >= ai.fireBreathStreakCount
                && _elapsed >= _nextStreakTime + 0.3f)
            {
                ai.ChangeState(SeveredFangAI.RecoveryState);
            }
        }

        public override void Exit(SeveredFangAI ai)
        {
            ai.StopMovement();
        }
    }
}
