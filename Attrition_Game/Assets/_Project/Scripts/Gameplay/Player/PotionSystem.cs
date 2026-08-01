using Fusion;
using UnityEngine;
using Attrition.Data;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Hệ thống bình HP/Mana (cơ chế Sekiro/Afterimage).
    /// - Q uống bình máu, E uống bình mana — tiêu 1 charge, hồi lượng từ ConsumableSO.
    /// - Số charge tối đa tăng khi giết elite/giải mission (IncreaseMaxHealthCharges...).
    /// - Rest hồi đầy lại số charge (RefillAll).
    /// Charges là [Networked] để host↔client đồng bộ. Chỉ host trừ/cộng charge.
    /// PlayerController gọi TryUseHealthPotion/TryUseManaPotion khi nhấn Q/E.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PotionSystem : NetworkBehaviour
    {
        [Header("---- STATIC DATA ----")]
        [Tooltip("SO định nghĩa lượng hồi bình máu.")]
        [SerializeField] private ConsumableSO healthPotion;
        [Tooltip("SO định nghĩa lượng hồi bình mana.")]
        [SerializeField] private ConsumableSO manaPotion;

        [Header("---- POTION CONFIG ----")]
        [SerializeField] private Attrition.Data.PotionConfigSO configSO;

        private int startingHealthCharges => configSO != null ? configSO.startingHealthCharges : 3;
        private int startingManaCharges => configSO != null ? configSO.startingManaCharges : 3;
        private int hardMaxHealthCharges => configSO != null ? configSO.hardMaxHealthCharges : 8;
        private int hardMaxManaCharges => configSO != null ? configSO.hardMaxManaCharges : 8;

        [Networked] public int MaxHealthCharges { get; set; }
        [Networked] public int MaxManaCharges { get; set; }
        [Networked] public int HealthCharges { get; set; }
        [Networked] public int ManaCharges { get; set; }

        private PlayerStats _stats;

        public override void Spawned()
        {
            _stats = GetComponent<PlayerStats>();

            if (HasStateAuthority)
            {
                if (MaxHealthCharges <= 0)
                {
                    MaxHealthCharges = startingHealthCharges;
                    HealthCharges = MaxHealthCharges;
                }
                if (MaxManaCharges <= 0)
                {
                    MaxManaCharges = startingManaCharges;
                    ManaCharges = MaxManaCharges;
                }
            }
        }

        /// <summary>Uống bình máu nếu còn charge. Trả về true nếu đã dùng. Chỉ host.</summary>
        public bool TryUseHealthPotion()
        {
            if (!HasStateAuthority || _stats == null) return false;
            if (HealthCharges <= 0) return false;
            if (_stats.CurrentHP <= 0) return false;

            int restore = healthPotion != null ? healthPotion.ComputeRestore(_stats.MaxHP) : _stats.MaxHP / 2;
            var fx = GetComponent<AccessoryEffects>();
            if (fx != null) restore = Mathf.RoundToInt(restore * fx.PotionHealMultiplier);
            _stats.RestoreHP(restore);
            HealthCharges--;

            // acc_potion (AttackBuff) KHÔNG kích ở đây nữa: buff bật lúc TRANG BỊ accessory và chạy 60s
            // (xem AccessoryEffects.ArmBuffsOnEquip). Uống bình chỉ còn việc hồi máu.
            return true;
        }

        /// <summary>Uống bình mana nếu còn charge. Trả về true nếu đã dùng. Chỉ host.</summary>
        public bool TryUseManaPotion()
        {
            if (!HasStateAuthority || _stats == null) return false;
            if (ManaCharges <= 0) return false;

            int restore = manaPotion != null ? manaPotion.ComputeRestore(_stats.MaxMana) : _stats.MaxMana / 2;
            _stats.RestoreMana(restore);
            ManaCharges--;
            return true;
        }

        /// <summary>Tiêu 1 bình máu mà KHÔNG hồi máu (dùng để trả giá hồi sinh đồng đội — người cứu chỉ
        /// mất bình, không được hưởng lượng hồi của bình đó). Trả về true nếu đã trừ. Chỉ host.</summary>
        public bool TryConsumeHealthPotionNoHeal()
        {
            if (!HasStateAuthority) return false;
            if (HealthCharges <= 0) return false;
            HealthCharges--;
            return true;
        }

        /// <summary>Rest tại checkpoint: hồi đầy số charge. Chỉ host.</summary>
        public void RefillAll()
        {
            if (!HasStateAuthority) return;
            HealthCharges = MaxHealthCharges;
            ManaCharges = MaxManaCharges;
        }

        /// <summary>Tổng số bình (máu + mana) — đơn vị "sức chứa" dùng để so sánh giữa 2 player.</summary>
        public int TotalCharges => MaxHealthCharges + MaxManaCharges;

        /// <summary>
        /// COOP: cân bằng số bình giữa mọi player tại thời điểm REST.
        ///
        /// Bình HP giấu trong map là pickup ĐƠN: chỉ người chạm vào được +1 cap ngay lúc nhặt, và pickup
        /// despawn cho cả phòng nên người kia KHÔNG thể tự nhặt bản của mình. Theo yêu cầu "1 người nhặt
        /// thì khi rest cả 2 đều tăng", rest sẽ bù cho ai đang thiếu cho bằng người có sức chứa cao nhất.
        ///
        /// So theo TỔNG (máu + mana), KHÔNG so riêng MaxHealthCharges: player tự đổi tỷ lệ máu/mana được
        /// qua <see cref="RpcReallocateFlasks"/> (tổng giữ nguyên). Nếu so riêng thì người đã dồn bình sang
        /// mana sẽ được nâng máu lên bằng người kia → tổng phồng lên, thành lỗi nhân bình mỗi lần rest.
        ///
        /// Phần bù cộng vào bình MÁU (đúng yêu cầu), giữ nguyên phần mana người đó đã tự chọn.
        /// Chỉ host gọi, và gọi TRƯỚC RefillAll để bình vừa bù cũng được rót đầy ngay lượt rest này.
        /// </summary>
        public static void ShareFlaskCapacityOnRest()
        {
            var all = FindObjectsByType<PotionSystem>(FindObjectsSortMode.None);
            if (all == null || all.Length < 2) return;   // solo: không có gì để chia sẻ

            int best = 0;
            foreach (var p in all)
                if (p != null && p.TotalCharges > best) best = p.TotalCharges;

            foreach (var p in all)
            {
                if (p == null || !p.HasStateAuthority) continue;
                int deficit = best - p.TotalCharges;
                if (deficit <= 0) continue;
                p.MaxHealthCharges = Mathf.Min(p.hardMaxHealthCharges, p.MaxHealthCharges + deficit);
            }
        }

        /// <summary>Tăng cap bình máu (giết elite/giải mission). Clamp về hardMax. Chỉ host.</summary>
        public void IncreaseMaxHealthCharges(int by = 1)
        {
            if (!HasStateAuthority) return;
            MaxHealthCharges = Mathf.Min(hardMaxHealthCharges, MaxHealthCharges + by);
        }

        /// <summary>Tăng cap bình mana. Clamp về hardMax. Chỉ host.</summary>
        public void IncreaseMaxManaCharges(int by = 1)
        {
            if (!HasStateAuthority) return;
            MaxManaCharges = Mathf.Min(hardMaxManaCharges, MaxManaCharges + by);
        }

        /// <summary>Thay đổi tỷ lệ bình (chỉ cho phép nếu tổng không đổi). Chỉ client gửi cho host.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcReallocateFlasks(int newHealth, int newMana)
        {
            if (newHealth + newMana == MaxHealthCharges + MaxManaCharges)
            {
                MaxHealthCharges = newHealth;
                MaxManaCharges = newMana;
                RefillAll();

                // Lưu ngay sau khi đổi tỷ lệ bình (host lo lưu cho mọi player). Đổi bình = 1 mốc save
                // như rest → lần sau vào game giữ đúng số bình đã phân bổ.
                Attrition.Gameplay.Persistence.GameSaveService.EnsureExists()
                    .Save(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Rest);
            }
        }
    }
}
