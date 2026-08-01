using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Enemy;

namespace Attrition.Controllers
{
    /// <summary>
    /// Điều phối Boss khu vực: thanh máu HUD + chuyển phase theo % HP.
    /// Gắn cùng GameObject với EnemyController (tier = Boss).
    /// - Thanh máu: mỗi máy tự đọc Health networked → cập nhật GameUIController cục bộ.
    /// - Phase: host tính theo ngưỡng HP, mở dần khả năng (tăng tốc, mở elite skill).
    /// Boss cuối (không rơi skill) chỉ cần tier=Boss, dropsSkillId rỗng.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    [RequireComponent(typeof(EnemyStats))]
    public class BossController : NetworkBehaviour
    {
        [Header("---- IDENTITY ----")]
        [SerializeField] private string bossDisplayName = "Nameless Tyrant";
        [Tooltip("Nhạc riêng của phòng/boss này. Trống = dùng bossBgmClip của SceneMusicController.")]
        [SerializeField] private AudioClip bossMusic;

        [Header("---- PHASES (ngưỡng % HP kích hoạt, giảm dần) ----")]
        [Tooltip("Ví dụ 0.66, 0.33 = 3 phase. Mỗi lần HP rớt dưới ngưỡng → sang phase mới.")]
        [SerializeField] private float[] phaseThresholds = { 0.66f, 0.33f };
        [Tooltip("Hệ số nhân tốc độ di chuyển/đánh cộng dồn mỗi phase (1.0 = giữ nguyên).")]
        [SerializeField] private float speedMultiplierPerPhase = 1.15f;

        [Networked] public int CurrentPhase { get; set; }

        private EnemyController _enemy;
        private EnemyStats _stats;
        private EliteEnemySkills _skills;
        // Bất kỳ AI boss nào implement IBossEncounter (SF/Druid/Elf/DemonKin/ArchDemon) — trước đây phải
        // giữ 1 field cho mỗi loại AI rồi if-else, thêm boss là thêm nhánh ở mọi chỗ.
        private Attrition.Core.IBossEncounter _bossEncounter;
        private int _maxHp;
        private bool _barShown;
        private int _lastShownHp = -1;

        /// <summary>Encounter đang diễn ra? Boss chờ trigger (hoặc đã reset sau wipe) = false → ẩn thanh máu.
        /// Boss không có AI đặc thù (không gate được) coi như luôn active.</summary>
        private bool EncounterActive
        {
            get
            {
                if (_bossEncounter != null) return _bossEncounter.EncounterStarted;
                return true;
            }
        }

        /// <summary>Host reset phase về 0 + trả lại tốc độ gốc khi đánh lại boss sau wipe.
        /// EnemyStats.PatrolSpeed/ChaseSpeed/AttackSpeed đã bị nhân dồn mỗi phase nên phải build lại từ SO.</summary>
        public void ResetPhases()
        {
            if (!HasStateAuthority) return;
            CurrentPhase = 0;
            if (_stats != null) _stats.RebuildBaseSpeeds();
            _lastShownHp = -1;
        }

        public override void Spawned()
        {
            _enemy = GetComponent<EnemyController>();
            _stats = GetComponent<EnemyStats>();
            _skills = GetComponent<EliteEnemySkills>();

            _bossEncounter = GetComponent<Attrition.Core.IBossEncounter>();

            _maxHp = _stats != null && _stats.MaxHP > 0 ? _stats.MaxHP : Mathf.Max(1, _enemy.maxHealth);


            if (HasStateAuthority) CurrentPhase = 0;

            // KHÔNG hiện thanh máu ngay — boss có thể đặt sẵn trong scene và đứng chờ trigger.
            // Render() sẽ hiện khi encounter thực sự bắt đầu (EncounterStarted).
            _barShown = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _enemy == null) return;
            if (_enemy.IsDead) return;

            float frac = (float)_enemy.CurrentHealth / _maxHp;
            int target = CurrentPhase;
            for (int i = 0; i < phaseThresholds.Length; i++)
                if (frac <= phaseThresholds[i]) target = i + 1;

            if (target > CurrentPhase)
            {
                CurrentPhase = target;
                OnPhaseEnter(target);
            }
        }

        private void OnPhaseEnter(int phase)
        {
            // Mỗi phase: tăng nhịp độ + mở elite skill nếu có.
            if (_stats != null) _stats.ApplyPhaseSpeedMultiplier(speedMultiplierPerPhase);
            if (_skills != null) _skills.EscalateForPhase(phase);
        }

        public override void Render()
        {
            // Cập nhật thanh máu trên mọi máy (đọc networked Health).
            if (_enemy == null) return;

            // Chỉ hiện thanh máu khi encounter đã bắt đầu (player đã vào phòng kích hoạt boss).
            // Boss đặt sẵn trong scene + chờ trigger → KHÔNG hiện thanh máu trước đó.
            bool encounterStarted = EncounterActive;
            if (!encounterStarted)
            {
                // Wipe → encounter reset về chờ trigger: ẩn thanh máu và cho phép hiện LẠI khi đánh lại.
                // Không reset _barShown thì lần vào phòng thứ hai sẽ không có thanh máu.
                if (_barShown)
                {
                    _barShown = false;
                    _lastShownHp = -1;
                    BossEvents.RaiseDespawned();
                    Attrition.Systems.SceneMusicController.NotifyBossEnded();
                }
                return;
            }
            // MaxHP networked có thể tới SAU Spawned() trên client → refresh khi đã có giá trị thật,
            // tránh thanh máu boss tính theo _maxHp=1 (fallback) làm kẹt đầy.
            if (_stats != null && _stats.MaxHP > 0 && _maxHp != _stats.MaxHP)
            {
                _maxHp = _stats.MaxHP;
                _lastShownHp = -1; // ép phát lại HpChanged với mẫu số đúng
            }

            if (_enemy.IsDead)
            {
                if (_barShown)
                {
                    _barShown = false;
                    BossEvents.RaiseDespawned();
                    Attrition.Systems.SceneMusicController.NotifyBossEnded();
                }
                return;
            }

            if (!_barShown)
            {
                BossEvents.RaiseSpawned(bossDisplayName, _maxHp);
                Attrition.Systems.SceneMusicController.NotifyBossStarted(bossMusic);
                _barShown = true;
            }

            int hp = _enemy.CurrentHealth;
            if (hp != _lastShownHp)
            {
                BossEvents.RaiseHpChanged(hp, _maxHp);
                _lastShownHp = hp;
            }
        }

        private void OnDestroy()
        {
            BossEvents.RaiseDespawned();
            Attrition.Systems.SceneMusicController.NotifyBossEnded();
        }
    }
}
