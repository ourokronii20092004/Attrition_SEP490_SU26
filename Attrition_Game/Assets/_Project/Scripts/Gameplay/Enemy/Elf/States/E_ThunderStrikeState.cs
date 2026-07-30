using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// SKILL 5: Thunder Strike — sét giáng từ trên trời xuống HẾT CHIỀU NGANG PHÒNG, mỗi cột cách nhau
    /// `strikeSpacing` (2-3 tile) để player có khe né. Sau đó lượt 2 giáng lại y như vậy NHƯNG rơi vào
    /// CHỖ PLAYER VỪA NÉ TỚI.
    ///
    /// Lượt 1 "quét sạch": vị trí cột tính từ biên trái tới biên phải của phòng (BossRoomBounds đọc
    /// CameraBoundsZone) nên phủ đúng căn phòng thật, không phải một khoảng đoán.
    ///
    /// Lượt 2 "truy đuổi": chốt vị trí player NGAY TRƯỚC lượt 2 rồi giáng `strikeChaseCount` cột quanh đó.
    /// Chốt trước (không cập nhật liên tục) để player vẫn né được bằng cách di chuyển tiếp — nếu bám theo
    /// mỗi tick thì thành đòn không thể tránh.
    /// </summary>
    public class E_ThunderStrikeState : ElfBossState
    {
        private enum Phase { SweepWave, Gap, ChaseWave, Done }

        private Phase _phase;
        private float _elapsed;
        private float _nextSpawnTime;
        private int _spawned;
        private int _sweepTotal;
        private float _minX, _maxX;
        private float _chaseCenterX;

        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _phase = Phase.SweepWave;
            _elapsed = 0f;
            _spawned = 0;
            _nextSpawnTime = 0.25f;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            BossRoomBounds.GetHorizontal(ai.transform.position, ai.strikeFallbackHalfWidth,
                                         out _minX, out _maxX);

            float spacing = Mathf.Max(0.5f, ai.strikeSpacing);
            _sweepTotal = Mathf.Max(1, Mathf.FloorToInt((_maxX - _minX) / spacing) + 1);

            ai.PlayAnim("Attack");
        }

        public override void Update(ElfBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            switch (_phase)
            {
                case Phase.SweepWave:
                    // Lượt 1: rải đều từ biên trái sang biên phải.
                    if (_spawned < _sweepTotal && _elapsed >= _nextSpawnTime)
                    {
                        if (ai.HasStateAuthority)
                        {
                            float x = _minX + Mathf.Max(0.5f, ai.strikeSpacing) * _spawned;
                            SpawnStrike(ai, x);
                        }
                        _spawned++;
                        _nextSpawnTime = _elapsed + ai.strikeInterval;
                    }
                    if (_spawned >= _sweepTotal && _elapsed >= _nextSpawnTime + 0.25f)
                    {
                        _phase = Phase.Gap;
                        _elapsed = 0f;
                    }
                    return;

                case Phase.Gap:
                    // Khoảng nghỉ giữa 2 lượt: player kịp nhận ra mình đã né xong lượt 1.
                    if (_elapsed < ai.strikeWaveGap) return;
                    // Chốt vị trí player NGAY LÚC NÀY cho lượt truy đuổi.
                    ai.DetectPlayer();
                    _chaseCenterX = ai.PlayerTarget != null
                        ? ai.PlayerTarget.position.x
                        : ai.transform.position.x;
                    _phase = Phase.ChaseWave;
                    _elapsed = 0f;
                    _spawned = 0;
                    _nextSpawnTime = 0f;
                    return;

                case Phase.ChaseWave:
                    int chaseTotal = Mathf.Max(1, ai.strikeChaseCount);
                    if (_spawned < chaseTotal && _elapsed >= _nextSpawnTime)
                    {
                        if (ai.HasStateAuthority)
                        {
                            // Rải quanh chỗ player vừa né tới, đối xứng: -1, 0, +1 nhân spacing.
                            float offset = (_spawned - (chaseTotal - 1) * 0.5f) * ai.strikeSpacing;
                            SpawnStrike(ai, _chaseCenterX + offset);
                        }
                        _spawned++;
                        _nextSpawnTime = _elapsed + ai.strikeInterval;
                    }
                    if (_spawned >= chaseTotal && _elapsed >= _nextSpawnTime + 0.5f)
                    {
                        _phase = Phase.Done;
                        ai.ChangeState(ElfBossAI.RecoveryState);
                    }
                    return;
            }
        }

        /// <summary>
        /// Spawn 1 cột sét ở toạ độ x. Y đặt cao hơn boss cho hình "rơi từ trên"; `EnemyAoEDamage` trên
        /// prefab tự hạ xuống mặt đất (snapToGround) nên vùng damage vẫn nằm đúng nền.
        /// Kẹp x trong biên phòng để lượt truy đuổi không giáng ra ngoài tường.
        /// </summary>
        private void SpawnStrike(ElfBossAI ai, float x)
        {
            float clampedX = Mathf.Clamp(x, _minX, _maxX);
            Vector2 pos = new Vector2(clampedX, ai.transform.position.y + ai.strikeSpawnHeight);
            ai.SpawnAoE(ai.ThunderStrikePrefab, pos, ai.strikeDamage);
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }
}
