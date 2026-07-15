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

        public float DashStaminaCost => baseStats != null ? baseStats.dashStaminaCost : 20f;
        public float StaminaRegenPerSecond => baseStats != null ? baseStats.staminaRegenPerSecond : 10f;

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

                // Áp tiến trình đã lưu (chỉ local player, chỉ solo — online hydrate ở chỗ khác).
                if (HasStateAuthority)
                    ApplyLoadedProgress();
            }
            else
            {
                SyncAllocatedToSheet();
            }
        }

        /// <summary>
        /// Nạp tiến trình từ save slot cục bộ (solo) vào player vừa spawn: level, điểm cộng,
        /// HP/Mana hiện tại, max bình, và dịch chuyển về checkpoint đã lưu. Host-side.
        /// </summary>
        private void ApplyLoadedProgress()
        {
            if (Attrition.Persistence.GameLaunch.IsOnline) return; // online: server là nguồn, không nạp local
            var data = Attrition.Persistence.SaveManager.LoadSlot(Attrition.Persistence.GameLaunch.SelectedSlot);
            if (data == null) return;

            // Level + điểm cộng
            var prog = GetComponent<PlayerProgression>();
            if (data.level > 0)
            {
                Level = data.level;
                _sheet?.SetLevel(Level);
                if (prog != null)
                {
                    prog.Level = data.level;
                    prog.CurrentExp = data.currentExp;
                }
            }
            if (data.allocatedPoints != null)
            {
                for (int i = 0; i < data.allocatedPoints.Length && i < AllocatedPoints.Length; i++)
                    AllocatedPoints.Set(i, data.allocatedPoints[i]);
                SyncAllocatedToSheet();
            }

            // Bình máu/mana tối đa đã mở và số bình hiện tại
            var potions = GetComponent<PotionSystem>();
            if (potions != null)
            {
                if (data.potionMaxFlasks > 0) potions.MaxHealthCharges = data.potionMaxFlasks;
                if (data.potionMaxManaFlasks > 0) potions.MaxManaCharges = data.potionMaxManaFlasks;
                
                // Nạp số bình hiện tại
                potions.HealthCharges = data.healthCharges > 0 ? Mathf.Min(data.healthCharges, potions.MaxHealthCharges) : potions.MaxHealthCharges;
                potions.ManaCharges = data.manaCharges > 0 ? Mathf.Min(data.manaCharges, potions.MaxManaCharges) : potions.MaxManaCharges;
            }

            // HP/Mana hiện tại (chưa clamp vội vì đồ đạc từ PlayerInventory có thể chưa đắp vào người làm tăng MaxHP)
            CurrentHP = data.currentHP > 0 ? data.currentHP : MaxHP;
            CurrentMana = data.currentMana > 0 ? data.currentMana : MaxMana;
            CurrentStamina = MaxStamina;

            // Dịch chuyển về checkpoint đã lưu (nếu có)
            // (Đã chuyển về NetworkSpawner để nạp trực tiếp qua spawnPos, giúp mượt hơn ở màn hình chờ)

            // Nạp playtime nền để session cộng dồn
            Attrition.Gameplay.Persistence.GameSaveService.EnsureExists().SetBasePlaytime(data.playtimeSeconds);
            OnStatsChanged?.Invoke();
        }

        /// <summary>
        /// COOP: hydrate stat từ DTO session (host fetch từ server). Đối xứng ApplyLoadedProgress của
        /// solo nhưng nguồn là server, không phải save slot local. Gọi bởi PlayerInventory sau khi
        /// session load xong (cache SessionStatsByChar sẵn sàng). Chỉ host (StateAuthority trên player này).
        /// </summary>
        public void HydrateFromCoopSession(APIManager.CharacterSessionDto cs)
        {
            if (!HasStateAuthority || cs == null) return;

            // Level + điểm cộng
            var prog = GetComponent<PlayerProgression>();
            if (cs.currentLevel > 0)
            {
                Level = cs.currentLevel;
                _sheet?.SetLevel(Level);
                if (prog != null) { prog.Level = cs.currentLevel; prog.CurrentExp = cs.currentExp; }
            }
            if (!string.IsNullOrEmpty(cs.allocatedPointsJson))
            {
                try
                {
                    var pts = Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>(cs.allocatedPointsJson);
                    if (pts != null)
                    {
                        for (int i = 0; i < pts.Length && i < AllocatedPoints.Length; i++)
                            AllocatedPoints.Set(i, pts[i]);
                        SyncAllocatedToSheet();
                    }
                }
                catch { /* JSON hỏng → bỏ qua điểm cộng, giữ mặc định */ }
            }

            // Số bình máu/mana tối đa đã mở. Chỉ áp khi record có tổng bình > 0 (record đã lưu hợp lệ);
            // dùng chính giá trị server kể cả khi 1 loại = 0 (vd 5 máu / 0 mana là hợp lệ — trước đây
            // guard 'mana > 0' làm 0 mana bị bỏ qua → giữ mặc định 2, sai). Record trống (0+0 = char
            // chưa lưu bình) → giữ mặc định từ PotionSystem.Spawned.
            var potions = GetComponent<PotionSystem>();
            if (potions != null && (cs.potionMaxFlasks + cs.potionMaxManaFlasks) > 0)
            {
                potions.MaxHealthCharges = cs.potionMaxFlasks;
                potions.MaxManaCharges = cs.potionMaxManaFlasks;
                potions.HealthCharges = potions.MaxHealthCharges;
                potions.ManaCharges = potions.MaxManaCharges;
                Debug.Log($"[Hydrate] char={cs.characterId} nạp từ server: hpFlask={cs.potionMaxFlasks} manaFlask={cs.potionMaxManaFlasks} "
                          + $"→ áp Max HP={potions.MaxHealthCharges} Mana={potions.MaxManaCharges}");
            }

            // HP/Mana hiện tại (clamp sau khi đồ đã đắp có thể đổi MaxHP; ReviveFull/rest sẽ hồi đầy).
            CurrentHP = cs.currentHp > 0 ? cs.currentHp : MaxHP;
            CurrentMana = cs.currentMana > 0 ? cs.currentMana : MaxMana;
            CurrentStamina = MaxStamina;
            OnStatsChanged?.Invoke();
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

        // CLIENT: Level + AllocatedPoints là [Networked] nhưng StatSheet (_level, _allocated) chỉ được
        // đồng bộ trong path host-only (TryAllocatePoint/SetLevelFromProgression) hoặc 1 lần lúc Spawned.
        // → client cộng điểm / lên cấp thì MaxHP/DEF/UnspentPoints KHÔNG cập nhật. Dò đổi mỗi frame ở
        // client rồi re-load sheet (đối xứng PlayerInventory.Render). Host tự xử lý nên bỏ qua.
        private int _lastStatChecksum = int.MinValue;

        public override void Render()
        {
            if (HasStateAuthority || _sheet == null) return;

            int sum = Level * 397;
            for (int i = 0; i < AllocatedPoints.Length; i++) sum = sum * 31 + AllocatedPoints.Get(i);
            if (sum == _lastStatChecksum) return;
            _lastStatChecksum = sum;

            _sheet.SetLevel(Level);       // cấp đổi → UnspentPoints + scale theo cấp đúng
            SyncAllocatedToSheet();       // điểm cộng đổi → MaxHP/DEF/AD... đúng + OnStatsChanged refresh UI
        }

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
            var leveling = progression != null ? progression.GetLevelingConfig() : null;
            
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
                return progression != null && progression.GetLevelingConfig() != null ? progression.GetLevelingConfig().maxLevel : 21;
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
            // Lưu Max CŨ trước khi dựng lại gear để tính DELTA khi đồ đổi max.
            int oldMaxHp = MaxHP, oldMaxMana = MaxMana;
            _sheet.SetLevel(level);
            _sheet.RebuildGear(equipped, damageAccessories, BuildItemModifierOverrides(equipped, damageAccessories));
            if (HasStateAuthority)
            {
                Level = level;
                // CỘNG delta max vào current: mặc đồ +HP thì current tăng ĐÚNG lượng đó (full→full = 110/110,
                // đang thương 50/100 + đồ +10 → 60/110, không tự hồi phần thiếu). Tháo đồ giảm max thì
                // current giảm theo rồi clamp. CurrentHP<=0 (chưa init) → set đầy.
                if (CurrentHP <= 0) CurrentHP = MaxHP;
                else CurrentHP = Mathf.Clamp(CurrentHP + (MaxHP - oldMaxHp), 1, MaxHP);
                if (CurrentMana <= 0) CurrentMana = MaxMana;
                else CurrentMana = Mathf.Clamp(CurrentMana + (MaxMana - oldMaxMana), 0, MaxMana);
            }
            OnStatsChanged?.Invoke();
        }

        /// <summary>
        /// Gom modifiers item admin sửa trên web (ItemConfigProvider) thành map itemId → StatModifier[]
        /// cho StatSheet, CHỈ cho các món đang mặc. Null nếu provider chưa sẵn/offline → dùng SO mặc định.
        /// </summary>
        private System.Collections.Generic.Dictionary<string, StatModifier[]> BuildItemModifierOverrides(
            EquipmentSO[] equipped, AccessorySO[] damageAccessories)
        {
            var prov = Attrition.Persistence.ItemConfigProvider.Instance;
            if (prov == null || !prov.IsReady) return null;

            var map = new System.Collections.Generic.Dictionary<string, StatModifier[]>();
            void TryAdd(string itemId)
            {
                if (string.IsNullOrEmpty(itemId) || map.ContainsKey(itemId)) return;
                var ov = prov.GetOverride(itemId);
                if (ov?.modifiers == null) return;
                var mods = new System.Collections.Generic.List<StatModifier>();
                foreach (var (stat, amount) in ov.modifiers)
                    if (System.Enum.TryParse<Attrition.Core.StatType>(stat, out var st))
                        mods.Add(new StatModifier { stat = st, amount = amount });
                if (mods.Count > 0) map[itemId] = mods.ToArray();
            }

            if (equipped != null) foreach (var e in equipped) if (e != null) TryAdd(e.itemId);
            if (damageAccessories != null) foreach (var a in damageAccessories) if (a != null) TryAdd(a.itemId);
            return map.Count > 0 ? map : null;
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
            CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegenPerSecond * deltaTime);
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

        /// <summary>Đủ mana để dùng skill không?</summary>
        public bool HasMana(int cost) => CurrentMana >= cost;

        /// <summary>Trừ mana nếu đủ (BR-16). Trả false nếu thiếu. Chỉ host.</summary>
        public bool TryConsumeMana(int cost)
        {
            if (cost <= 0) return true;
            if (CurrentMana < cost) return false;
            CurrentMana -= cost;
            return true;
        }

        /// <summary>Rest tại checkpoint: hồi đầy HP/Mana/Stamina. Không hồi sinh nếu đã chết. Chỉ host.</summary>
        public void RestoreFull()
        {
            if (!HasStateAuthority || CurrentHP <= 0) return;
            CurrentHP = MaxHP;
            CurrentMana = MaxMana;
            CurrentStamina = MaxStamina;
        }

        /// <summary>
        /// Hồi sinh từ trạng thái chết: hồi đầy HP/Mana/Stamina KHÔNG guard CurrentHP<=0.
        /// RestoreFull bị guard nên không dùng được cho respawn — gọi nó khi HP âm sẽ không hồi,
        /// để lại HP âm khiến bình máu (chặn khi CurrentHP<=0) vĩnh viễn vô dụng. Chỉ host.
        /// </summary>
        public void ReviveFull()
        {
            if (!HasStateAuthority) return;
            CurrentHP = MaxHP;
            CurrentMana = MaxMana;
            CurrentStamina = MaxStamina;
            OnStatsChanged?.Invoke();
        }
    }
}
