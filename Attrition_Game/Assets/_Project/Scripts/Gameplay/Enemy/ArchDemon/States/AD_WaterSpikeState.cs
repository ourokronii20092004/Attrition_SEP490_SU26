using UnityEngine;

namespace Attrition.Gameplay.Enemy.ArchDemon.States
{
    /// <summary>
    /// SKILL 4: Water Spike — mọc `spikeCount` (6-7) cọc nước TỪ DƯỚI LÊN, lần lượt chạy tới cuối phòng,
    /// mỗi cọc cách nhau `spikeSpacing`. Trước mỗi cọc có dấu báo (`waterStartup1Prefab`) hiện ra
    /// `spikeStartupLead` giây để player biết chỗ nào sắp mọc mà rời đi.
    ///
    /// Cơ chế báo trước = spawn dấu SỚM HƠN cọc đúng một khoảng: mỗi vị trí đi qua 2 mốc — dấu hiện, rồi
    /// (sau lead) cọc mọc. Nhờ vậy hàng cọc vẫn chạy liên tục ra xa mà player luôn thấy trước một nhịp.
    ///
    /// Cọc dừng ở biên phòng (BossRoomBounds) nên không mọc xuyên tường.
    /// </summary>
    public class AD_WaterSpikeState : ArchDemonBossState
    {
        private float _elapsed;
        private int _startupSpawned;   // số dấu báo đã hiện
        private int _spikeSpawned;     // số cọc đã mọc
        private float _nextStartupTime;
        private float _dirX;
        private float _minX, _maxX;
        private int _total;

        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _startupSpawned = 0;
            _spikeSpawned = 0;
            _nextStartupTime = 0.2f;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            BossRoomBounds.GetHorizontal(ai.transform.position, ai.roomFallbackHalfWidth,
                                         out _minX, out _maxX);

            // Số cọc thực tế: kẹp theo chiều dài còn lại của phòng để không mọc ngoài tường.
            float bossX = ai.transform.position.x;
            float room = _dirX > 0 ? Mathf.Max(0f, _maxX - bossX - ai.spikeFirstOffset)
                                   : Mathf.Max(0f, bossX - _minX - ai.spikeFirstOffset);
            int fit = Mathf.FloorToInt(room / Mathf.Max(0.5f, ai.spikeSpacing)) + 1;
            _total = Mathf.Clamp(fit, 1, Mathf.Max(1, ai.spikeCount));

            ai.PlayAttackAnimNoOrb();   // cắt clip trước frame 8 — skill nước không kèm cầu bóng tối
        }

        public override void Update(ArchDemonBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // ── Dấu báo: hiện trước, rải theo nhịp spikeInterval ──
            if (_startupSpawned < _total && _elapsed >= _nextStartupTime)
            {
                ai.SpawnAoE(ai.WaterStartup1Prefab, PositionAt(ai, _startupSpawned), 0);
                _startupSpawned++;
                _nextStartupTime = _elapsed + ai.spikeInterval;
            }

            // ── Cọc mọc: đi sau dấu đúng spikeStartupLead giây ──
            if (_spikeSpawned < _startupSpawned)
            {
                // Mốc dấu thứ i xuất hiện ở 0.2 + i*interval → cọc thứ i mọc ở mốc đó + lead.
                float dueTime = 0.2f + _spikeSpawned * ai.spikeInterval + ai.spikeStartupLead;
                if (_elapsed >= dueTime)
                {
                    ai.SpawnAoE(ai.WaterSpikePrefab, PositionAt(ai, _spikeSpawned), ai.spikeDamage);
                    _spikeSpawned++;
                }
            }

            if (_spikeSpawned >= _total && _elapsed >= _nextStartupTime + ai.spikeStartupLead + 0.35f)
                ai.ChangeState(ArchDemonBossAI.RecoveryState);
        }

        /// <summary>Vị trí cọc/dấu thứ i — kẹp trong biên phòng cho chắc.</summary>
        private Vector2 PositionAt(ArchDemonBossAI ai, int i)
        {
            float x = ai.transform.position.x + _dirX * (ai.spikeFirstOffset + ai.spikeSpacing * i);
            return new Vector2(Mathf.Clamp(x, _minX, _maxX), ai.transform.position.y);
        }

        public override void Exit(ArchDemonBossAI ai) => ai.StopMovement();
    }
}
