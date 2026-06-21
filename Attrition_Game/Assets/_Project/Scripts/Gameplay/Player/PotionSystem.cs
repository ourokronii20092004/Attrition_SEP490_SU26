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

        [Header("---- CHARGE CAPS ----")]
        [Tooltip("Số bình máu khởi đầu (concept: bắt đầu 3).")]
        [SerializeField] private int startingHealthCharges = 3;
        [Tooltip("Số bình mana khởi đầu.")]
        [SerializeField] private int startingManaCharges = 3;
        [Tooltip("Cap tuyệt đối số bình máu (concept: 7-8).")]
        [SerializeField] private int hardMaxHealthCharges = 8;
        [Tooltip("Cap tuyệt đối số bình mana.")]
        [SerializeField] private int hardMaxManaCharges = 8;

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
                if (MaxHealthCharges <= 0) MaxHealthCharges = startingHealthCharges;
                if (MaxManaCharges <= 0) MaxManaCharges = startingManaCharges;
                HealthCharges = MaxHealthCharges;
                ManaCharges = MaxManaCharges;
            }
        }

        /// <summary>Uống bình máu nếu còn charge. Trả về true nếu đã dùng. Chỉ host.</summary>
        public bool TryUseHealthPotion()
        {
            if (!HasStateAuthority || _stats == null) return false;
            if (HealthCharges <= 0) return false;
            if (_stats.CurrentHP <= 0) return false;

            int restore = healthPotion != null ? healthPotion.ComputeRestore(_stats.MaxHP) : _stats.MaxHP / 2;
            _stats.RestoreHP(restore);
            HealthCharges--;
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

        /// <summary>Rest tại checkpoint: hồi đầy số charge. Chỉ host.</summary>
        public void RefillAll()
        {
            if (!HasStateAuthority) return;
            HealthCharges = MaxHealthCharges;
            ManaCharges = MaxManaCharges;
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
            }
        }
    }
}
