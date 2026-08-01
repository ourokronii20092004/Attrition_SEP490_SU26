using UnityEngine;

namespace Attrition.Gameplay.Enemy.DemonKin.States
{
    /// <summary>
    /// SKILL 3: Earth Bump — boss NÂNG ĐỊA HÌNH sát hai bên người: MỖI HƯỚNG `bumpPerSide` (2) cục đất,
    /// cục càng xa càng TO (`bumpScaleStep`), đẩy player ra xa nhưng VẪN gây sát thương.
    /// (Tham chiếu: boss Failure trong game Afterimage.)
    ///
    /// Đối xứng 2 bên nên player không thể chỉ chạy sang một phía — muốn an toàn phải rời khỏi vùng cạnh
    /// boss, đúng mục đích "làm player tránh xa". Đây cũng là skill AI chọn khi player áp sát.
    ///
    /// Cục TRONG nâng trước, cục NGOÀI nâng sau (`bumpInterval`) → cảm giác đất nứt lan từ chân boss ra.
    /// Kích thước phóng to đặt lúc spawn qua `SpawnAoEScaled`; vùng damage của prefab (EnemyAoEDamage.radius)
    /// KHÔNG tự lớn theo scale, nên tool setup đặt radius theo cục lớn nhất — chấp nhận cục nhỏ có vùng
    /// damage hơi rộng hơn hình, đổi lại không phải sync thêm biến qua mạng.
    /// </summary>
    public class DK_EarthBumpState : DemonKinBossState
    {
        private float _elapsed;
        private int _ring;              // 0 = cặp trong, 1 = cặp ngoài...
        private float _nextSpawnTime;

        public override void Enter(DemonKinBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _ring = 0;
            _nextSpawnTime = Mathf.Max(ai.bumpChargeTime, DemonKinBossAI.SkillAttackWindup);

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            ai.PlayAnim("Attack");
        }

        public override void Update(DemonKinBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            if (_ring < ai.bumpPerSide && _elapsed >= _nextSpawnTime)
            {
                if (_ring == 0) ai.PlayAnim("Idle");
                if (ai.HasStateAuthority)
                {
                    float dist = ai.bumpFirstOffset + ai.bumpStepX * _ring;
                    float scale = Mathf.Pow(Mathf.Max(1f, ai.bumpScaleStep), _ring);
                    Vector2 origin = ai.transform.position;

                    // Cục PHẢI: scale x > 0 → nhìn sang phải (mọc lên bên phải boss).
                    ai.SpawnAoEScaledFlipped(ai.EarthBumpPrefab, origin + new Vector2(dist, 0f),
                                             ai.bumpDamage, scale, flipX: false);
                    // Cục TRÁI: scale x âm → nhìn sang trái (đối xứng, không cần sprite riêng).
                    ai.SpawnAoEScaledFlipped(ai.EarthBumpPrefab, origin + new Vector2(-dist, 0f),
                                             ai.bumpDamage, scale, flipX: true);
                }
                _ring++;
                _nextSpawnTime = _elapsed + ai.bumpInterval;
            }

            if (_ring >= ai.bumpPerSide && _elapsed >= _nextSpawnTime + 0.45f)
                ai.ChangeState(DemonKinBossAI.RecoveryState);
        }

        public override void Exit(DemonKinBossAI ai) => ai.StopMovement();
    }
}
