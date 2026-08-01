using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// SKILL 3: Thunder Wave — cho nổ `waveCount` quả cầu sấm theo hình CHỮ W-W chạy về phía player, nổ
    /// LẦN LƯỢT: điểm nào spawn trước thì nổ trước.
    ///
    /// "Nổ lần lượt" đến từ việc spawn TUẦN TỰ cách nhau `waveInterval` giây — mỗi quả cầu
    /// (EnemyAoEDamage) tự nổ sau `damageDelay` ngắn của riêng nó, nên thứ tự spawn = thứ tự nổ.
    ///
    /// HÌNH CHỮ W: mỗi chữ W gồm 4 điểm với Y theo mẫu cao-thấp-cao-thấp... Dùng chu kỳ 4 điểm:
    /// i%4 == 0 → đỉnh, 1 → đáy, 2 → đỉnh giữa (thấp hơn đỉnh ngoài), 3 → đáy. Với waveCount = 8 thì ra
    /// đúng 2 chữ W liền nhau.
    /// </summary>
    public class E_ThunderWaveState : ElfBossState
    {
        private float _elapsed;
        private int _spawned;
        private float _nextSpawnTime;
        private Vector2 _origin;
        private float _dirX;

        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _spawned = 0;
            _nextSpawnTime = ElfBossAI.SkillAttackWindup;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            _origin = (Vector2)ai.transform.position + new Vector2(_dirX * ai.waveStepX, 0f);

            ai.PlayAnim("Attack");
        }

        public override void Update(ElfBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            if (_spawned < ai.waveCount && _elapsed >= _nextSpawnTime)
            {
                if (_spawned == 0) ai.PlayAnim("Idle");
                if (ai.HasStateAuthority)
                {
                    float x = _origin.x + _dirX * ai.waveStepX * _spawned;
                    float y = _origin.y + WShapeY(_spawned, ai.waveAmplitudeY);
                    ai.SpawnAoE(ai.ThunderHitPrefab, new Vector2(x, y), ai.waveDamage);
                }
                _spawned++;
                _nextSpawnTime = _elapsed + ai.waveInterval;
            }

            // Xong chuỗi + đệm cho quả cuối nổ hết.
            if (_spawned >= ai.waveCount && _elapsed >= _nextSpawnTime + 0.5f)
                ai.ChangeState(ElfBossAI.RecoveryState);
        }

        /// <summary>
        /// Độ lệch Y cho điểm thứ i để vẽ chữ W. Chu kỳ 4: đỉnh ngoài → đáy → đỉnh GIỮA (thấp hơn) → đáy.
        /// Đỉnh giữa thấp hơn giúp mắt đọc ra chữ W thay vì hình răng cưa đều.
        /// </summary>
        private static float WShapeY(int i, float amp)
        {
            switch (i % 4)
            {
                case 0: return amp;          // đỉnh trái
                case 1: return 0f;           // đáy
                case 2: return amp * 0.55f;  // đỉnh giữa (thấp hơn)
                default: return 0f;          // đáy
            }
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }
}
