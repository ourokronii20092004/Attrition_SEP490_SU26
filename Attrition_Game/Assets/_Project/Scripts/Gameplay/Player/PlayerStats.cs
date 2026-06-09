using Fusion;
using UnityEngine;
using Attrition.Core;
using Attrition.Data;
using Attrition.Systems;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Nguồn chỉ số DUY NHẤT của player lúc runtime. Ôm StatSheet (base SO + điểm tự cộng + trang bị).
    /// HP/Mana/Stamina hiện tại là [Networked] để đồng bộ host↔client.
    /// PlayerController / PlayerCombat đọc qua đây thay vì hard-code maxHP/attackDamage.
    /// Additive: nếu baseStats chưa gán, fallback giá trị mặc định để prefab cũ không vỡ.
    /// </summary>
    public class PlayerStats : NetworkBehaviour
    {
        [Header("---- STATIC DATA ----")]
        [Tooltip("ScriptableObject chỉ số gốc. Bỏ trống = dùng mặc định fallback.")]
        [SerializeField] private CharacterBaseStatsSO baseStats;

        [Networked] public int CurrentHP { get; set; }
        [Networked] public int CurrentMana { get; set; }
        [Networked] public float CurrentStamina { get; set; }
        [Networked] public int Level { get; set; }

        // Điểm tự cộng (Option 2) — host-authoritative, sync xuống client.
        // Index = (int)StatType (0..6). UI đọc để hiện, gọi RpcRequestAllocate để cộng.
        [Networked, Capacity(7)] public NetworkArray<int> AllocatedPoints { get; }

        private StatSheet _sheet;
        public StatSheet Sheet => _sheet;

        /// <summary>Event UI lắng nghe để refresh bảng chỉ số khi level/điểm/đồ đổi.</summary>
        public event System.Action OnStatsChanged;

        // Fallback khi chưa gán SO (giữ tương thích prefab cũ)
        private const int FallbackHP = 100, FallbackMana = 100, FallbackStamina = 100;

        [Header("---- STAMINA ----")]
        [Tooltip("Stamina tiêu hao mỗi lần dash.")]
        [SerializeField] private float dashStaminaCost = 20f;
        [Tooltip("Stamina hồi lại mỗi giây.")]
        [SerializeField] private float staminaRegenPerSecond = 10f;

        public float DashStaminaCost => dashStaminaCost;

        public override void Spawned()
        {
            BuildSheet();

            if (HasStateAuthority)
            {
                if (Level <= 0) Level = 1;
                _sheet?.SetLevel(Level);
                SyncAllocatedToSheet();
                CurrentHP = MaxHP;
                CurrentMana = MaxMana;
                CurrentStamina = MaxStamina;
            }
            else
            {
                SyncAllocatedToSheet();
            }
        }

        /// <summary>Nạp NetworkArray điểm cộng vào StatSheet (gọi sau mỗi thay đổi level/điểm/khi spawn proxy).</summary>
        private void SyncAllocatedToSheet()
        {
            if (_sheet == null) return;
            var map = new System.Collections.Generic.Dictionary<Attrition.Core.StatType, int>();
            for (int i = 0; i < AllocatedPoints.Length; i++)
                if (AllocatedPoints.Get(i) > 0) map[(Attrition.Core.StatType)i] = AllocatedPoints.Get(i);
            _sheet.LoadAllocated(map);
            OnStatsChanged?.Invoke();
        }

        /// <summary>Số điểm chưa tiêu (đọc từ sheet). 0 nếu chưa có SO.</summary>
        public int UnspentPoints => _sheet?.UnspentPoints ?? 0;

        /// <summary>Cộng 1 điểm vào stat (Option 2). Chỉ host; clamp theo điểm còn lại. Trả false nếu hết điểm.</summary>
        public bool TryAllocatePoint(Attrition.Core.StatType stat)
        {
            if (!HasStateAuthority || _sheet == null) return false;
            if (!_sheet.AllocatePoint(stat)) return false;

            int idx = (int)stat;
            if (idx >= 0 && idx < AllocatedPoints.Length)
                AllocatedPoints.Set(idx, AllocatedPoints.Get(idx) + 1);

            // Cộng điểm có thể nâng Max → clamp current không vượt max mới (không tự hồi đầy).
            CurrentHP = Mathf.Min(CurrentHP, MaxHP);
            CurrentMana = Mathf.Min(CurrentMana, MaxMana);
            CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);
            OnStatsChanged?.Invoke();
            return true;
        }

        /// <summary>Client gửi yêu cầu cộng điểm lên host.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestAllocate(int statTypeInt) => TryAllocatePoint((Attrition.Core.StatType)statTypeInt);

        private void BuildSheet()
        {
            var progression = GetComponent<PlayerProgression>();
            var leveling = progression != null ? progression.GetLevelingConfig() : new LevelingConfig();
            
            if (baseStats != null)
                _sheet = new StatSheet(baseStats, leveling);
        }

        // ─── Chỉ số gộp (đọc từ sheet, fallback nếu chưa có SO) ───
        public int MaxHP => _sheet?.MaxHP ?? FallbackHP;
        public int MaxMana => _sheet?.MaxMana ?? FallbackMana;
        public int MaxStamina => _sheet != null ? _sheet.MaxStamina : FallbackStamina;
        public int AD => _sheet?.AD ?? 10;
        public int AP => _sheet?.AP ?? 10;
        public int DEF => _sheet?.DEF ?? 10;
        public int RES => _sheet?.RES ?? 10;

        public float MoveSpeed => baseStats != null ? baseStats.moveSpeed : 10f;
        public float DashSpeed => baseStats != null ? baseStats.dashSpeed : 25f;
        public float SlideSpeed => baseStats != null ? baseStats.slideSpeed : 15f;
        public float JumpForce => baseStats != null ? baseStats.jumpForce : 15f;
        public float DoubleJumpForce => baseStats != null ? baseStats.doubleJumpForce : 8f;
        public float AttackSpeed => baseStats != null ? baseStats.attackSpeed : 1f;
        public float ChargeDamageMultiplier => baseStats != null ? baseStats.chargeDamageMultiplier : 2f;

        /// <summary>Cấp tối đa từ hệ thống (fallback 21 nếu chưa cấu hình).</summary>
        public int MaxLevel 
        {
            get 
            {
                var progression = GetComponent<PlayerProgression>();
                return progression != null ? progression.maxLevel : 21;
            }
        }

        /// <summary>
        /// Gọi bởi PlayerProgression khi lên cấp: áp level mới vào sheet (mở thêm điểm tự cộng),
        /// cập nhật max stats và hồi đầy HP/Mana như phần thưởng lên cấp. Chỉ host.
        /// </summary>
        public void SetLevelFromProgression(int level)
        {
            if (_sheet != null) _sheet.SetLevel(level);
            if (!HasStateAuthority) return;
            Level = level;
            CurrentHP = MaxHP;
            CurrentMana = MaxMana;
            CurrentStamina = MaxStamina;
            OnStatsChanged?.Invoke();
        }

        /// <summary>Áp lại level + rebuild trang bị (gọi khi load session hoặc đổi đồ).</summary>
        public void ApplyLoadout(int level, EquipmentSO[] equipped, AccessorySO[] damageAccessories)
        {
            if (_sheet == null) return;
            _sheet.SetLevel(level);
            _sheet.RebuildGear(equipped, damageAccessories);
            if (HasStateAuthority)
            {
                Level = level;
                CurrentHP = Mathf.Min(CurrentHP <= 0 ? MaxHP : CurrentHP, MaxHP);
                CurrentMana = Mathf.Min(CurrentMana <= 0 ? MaxMana : CurrentMana, MaxMana);
            }
            OnStatsChanged?.Invoke();
        }

        /// <summary>Sát thương phòng thủ-aware lên 1 mục tiêu. Dùng DamageCalculator chung.</summary>
        public int ComputeOutgoing(DamageType type, int targetDef, int targetRes)
        {
            int raw = type == DamageType.Magic ? AP : AD;
            return DamageCalculator.Compute(type, raw, targetDef, targetRes);
        }

        // ─── STAMINA (chỉ host/state-authority được phép sửa) ───

        public bool HasStamina(float amount) => CurrentStamina >= amount;

        /// <summary>Trừ stamina nếu đủ. Trả về false (không trừ) nếu thiếu. Hỗ trợ Client Prediction.</summary>
        public bool TryConsumeStamina(float amount)
        {
            if (CurrentStamina < amount) return false;
            // Chỉ Host mới được ghi đè biến [Networked]
            if (HasStateAuthority)
            {
                CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
            }
            return true;
        }

        /// <summary>Hồi stamina theo thời gian. Gọi mỗi tick trên state authority.</summary>
        public void RegenStamina(float deltaTime)
        {
            if (!HasStateAuthority) return;
            if (CurrentStamina >= MaxStamina) return;
            CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + staminaRegenPerSecond * deltaTime);
        }

        // ─── HỒI HP / MANA (chỉ host) ───

        /// <summary>Hồi HP, clamp về MaxHP. Bỏ qua nếu đã chết. Chỉ chạy trên state authority.</summary>
        public void RestoreHP(int amount)
        {
            if (!HasStateAuthority || amount <= 0) return;
            if (CurrentHP <= 0) return;
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        }

        /// <summary>Hồi Mana, clamp về MaxMana. Chỉ chạy trên state authority.</summary>
        public void RestoreMana(int amount)
        {
            if (!HasStateAuthority || amount <= 0) return;
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        }

        /// <summary>Rest tại checkpoint: hồi đầy HP/Mana/Stamina. Không hồi sinh nếu đã chết. Chỉ host.</summary>
        public void RestoreFull()
        {
            if (!HasStateAuthority || CurrentHP <= 0) return;
            CurrentHP = MaxHP;
            CurrentMana = MaxMana;
            CurrentStamina = MaxStamina;
        }
    }
}
