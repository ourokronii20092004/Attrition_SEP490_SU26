using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// SKILL 5: Thunder Strike — 2 LƯỢT SÉT SO LE (đúng mô tả gốc):
    ///
    ///   Lượt 1: `strikeColumns` cột (mặc định 5) hiện ra LẦN LƯỢT, cách nhau `strikeSpacing` (3-4 tile)
    ///           rồi giật xuống. Player đứng vào các KHE giữa 2 cột để né.
    ///   Nghỉ  : `strikeWaveGap` giây (1-2s) — đủ để player thấy mình đã né xong và bắt đầu di chuyển.
    ///   Lượt 2: ĐẢO LẠI — sét giáng đúng vào các KHE của lượt 1 (lệch nửa spacing), còn chỗ vừa giáng ở
    ///           lượt 1 trở thành khe an toàn. Player buộc phải đổi chỗ giữa 2 lượt.
    ///
    /// KHÁC BẢN CŨ: bản cũ quét sạch cả phòng ở lượt 1 rồi lượt 2 "truy đuổi" 3 cột bám vị trí player — vừa
    /// không khớp mô tả, vừa không có khe né rõ ràng. Nay hai lượt là một cặp so le CỐ ĐỊNH, đọc được bằng mắt.
    ///
    /// Tâm hàng cột chốt theo vị trí PLAYER lúc bắt đầu (không phải boss) để lưới sét phủ đúng chỗ player
    /// đang đứng; toàn bộ kẹp trong biên phòng (BossRoomBounds đọc CameraBoundsZone).
    /// </summary>
    public class E_ThunderStrikeState : ElfBossState
    {
        private enum Phase { Wave1, Gap, Wave2, Done }

        private Phase _phase;
        private float _elapsed;
        private float _nextSpawnTime;
        private int _spawned;
        private float _minX, _maxX;
        private float _firstX;   // toạ độ cột đầu của lượt 1

        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _phase = Phase.Wave1;
            _elapsed = 0f;
            _spawned = 0;
            _nextSpawnTime = ElfBossAI.SkillAttackWindup;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            BossRoomBounds.GetHorizontal(ai.transform.position, ai.strikeFallbackHalfWidth,
                                         out _minX, out _maxX);

            // Hàng cột đặt QUANH PLAYER: cột giữa ở chỗ player, toả đều 2 bên.
            float centerX = ai.PlayerTarget != null ? ai.PlayerTarget.position.x : ai.transform.position.x;
            int columns = Mathf.Max(2, ai.strikeColumns);
            _firstX = centerX - (columns - 1) * 0.5f * ai.strikeSpacing;

            ai.PlayAnim("Attack");
        }

        public override void Update(ElfBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            int columns = Mathf.Max(2, ai.strikeColumns);

            switch (_phase)
            {
                case Phase.Wave1:
                    // Lượt 1: cột 1 → 5 hiện lần lượt tại _firstX + i*spacing.
                    if (_spawned < columns && _elapsed >= _nextSpawnTime)
                    {
                        if (_spawned == 0) ai.PlayAnim("Idle");
                        if (ai.HasStateAuthority)
                            SpawnStrike(ai, _firstX + ai.strikeSpacing * _spawned);
                        _spawned++;
                        _nextSpawnTime = _elapsed + ai.strikeInterval;
                    }
                    if (_spawned >= columns && _elapsed >= _nextSpawnTime + 0.25f)
                    {
                        _phase = Phase.Gap;
                        _elapsed = 0f;
                    }
                    return;

                case Phase.Gap:
                    // Nghỉ 1-2s: player nhận ra lượt 1 đã xong và bắt đầu đổi chỗ.
                    if (_elapsed < ai.strikeWaveGap) return;
                    _phase = Phase.Wave2;
                    _elapsed = 0f;
                    _spawned = 0;
                    ai.PlayAnim("Attack");
                    _nextSpawnTime = ElfBossAI.SkillAttackWindup;
                    return;

                case Phase.Wave2:
                    // Lượt 2 ĐẢO LẠI: giáng vào các KHE của lượt 1 (lệch nửa spacing).
                    // columns-1 khe giữa các cột, cộng 1 khe ngoài mỗi biên → phủ kín chỗ vừa an toàn.
                    int gapCount = columns + 1;
                    if (_spawned < gapCount && _elapsed >= _nextSpawnTime)
                    {
                        if (_spawned == 0) ai.PlayAnim("Idle");
                        if (ai.HasStateAuthority)
                            SpawnStrike(ai, _firstX + ai.strikeSpacing * (_spawned - 0.5f));
                        _spawned++;
                        _nextSpawnTime = _elapsed + ai.strikeInterval;
                    }
                    if (_spawned >= gapCount && _elapsed >= _nextSpawnTime + 0.5f)
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
        /// BỎ QUA cột nằm ngoài biên phòng — trước đây Clamp làm mọi cột tràn biên dồn về đúng 1 chỗ ở tường,
        /// phá vỡ thế so le của 2 lượt.
        /// </summary>
        private void SpawnStrike(ElfBossAI ai, float x)
        {
            if (x < _minX || x > _maxX) return;
            Vector2 pos = new Vector2(x, ai.transform.position.y + ai.strikeSpawnHeight);
            ai.SpawnAoE(ai.ThunderStrikePrefab, pos, ai.strikeDamage);
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }
}
