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

        [Header("---- PHASES (ngưỡng % HP kích hoạt, giảm dần) ----")]
        [Tooltip("Ví dụ 0.66, 0.33 = 3 phase. Mỗi lần HP rớt dưới ngưỡng → sang phase mới.")]
        [SerializeField] private float[] phaseThresholds = { 0.66f, 0.33f };
        [Tooltip("Hệ số nhân tốc độ di chuyển/đánh cộng dồn mỗi phase (1.0 = giữ nguyên).")]
        [SerializeField] private float speedMultiplierPerPhase = 1.15f;

        [Networked] public int CurrentPhase { get; set; }

        private EnemyController _enemy;
        private EnemyStats _stats;
        private EliteEnemySkills _skills;
        private Attrition.Gameplay.Enemy.SeveredFang.SeveredFangAI _sfAI;
        private int _maxHp;
        private bool _barShown;
        private int _lastShownHp = -1;

        public override void Spawned()
        {
            _enemy = GetComponent<EnemyController>();
            _stats = GetComponent<EnemyStats>();
            _skills = GetComponent<EliteEnemySkills>();
            _sfAI = GetComponent<Attrition.Gameplay.Enemy.SeveredFang.SeveredFangAI>();
            _maxHp = Mathf.Max(1, _enemy.maxHealth);

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
            bool encounterStarted = _sfAI == null || _sfAI.EncounterStarted;
            if (!encounterStarted) return;

            if (!_barShown)
            {
                BossEvents.RaiseSpawned(bossDisplayName, _maxHp);
                _barShown = true;
            }

            if (_enemy.IsDead)
            {
                BossEvents.RaiseDespawned();
                return;
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
        }
    }
}
