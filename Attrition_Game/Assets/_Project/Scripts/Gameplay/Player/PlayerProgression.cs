using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Tiến trình nhân vật: Level + EXP (Option 2 — mỗi level cho điểm tự cộng).
    /// EXP/Level là [Networked] → host tính, sync xuống client.
    /// Coop: quái chết cộng EXP NHƯ NHAU cho mọi player (gọi GainExp trực tiếp, không qua orb nhặt).
    ///
    /// Đường cong EXP: expToNext(level) = baseExp + (level-1) * perLevelExp (tuyến tính, dễ chỉnh).
    /// Khi đủ EXP → lên cấp, gọi PlayerStats.SetLevelFromProgression để mở thêm điểm cộng.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerProgression : NetworkBehaviour
    {
        [Header("---- EXP CURVE ----")]
        [Tooltip("EXP cần để lên level 2 (mốc đầu).")]
        [SerializeField] private int baseExp = 100;
        [Tooltip("EXP cần tăng thêm mỗi level (về sau cày lâu hơn).")]
        [SerializeField] private int perLevelExp = 50;

        [Networked] public int Level { get; set; }
        [Networked] public int CurrentExp { get; set; }

        private PlayerStats _stats;

        public override void Spawned()
        {
            _stats = GetComponent<PlayerStats>();

            if (HasStateAuthority && Level <= 0)
            {
                Level = 1;
                CurrentExp = 0;
            }
        }

        /// <summary>EXP cần để lên cấp kế tiếp (từ Level hiện tại).</summary>
        public int ExpToNext => baseExp + (Mathf.Max(1, Level) - 1) * perLevelExp;

        /// <summary>Cộng EXP (chỉ host). Tự xử lý lên cấp, kể cả nhiều cấp 1 lần.</summary>
        public void GainExp(int amount)
        {
            if (!HasStateAuthority || amount <= 0) return;
            if (_stats == null) _stats = GetComponent<PlayerStats>();

            int maxLevel = _stats != null ? _stats.MaxLevel : 21;
            if (Level >= maxLevel) return; // đã max, không cộng nữa

            CurrentExp += amount;

            while (Level < maxLevel && CurrentExp >= ExpToNext)
            {
                CurrentExp -= ExpToNext;
                Level++;
                OnLevelUp();
            }

            if (Level >= maxLevel) CurrentExp = 0; // chốt ở max
        }

        private void OnLevelUp()
        {
            // PlayerStats áp level mới → mở thêm điểm tự cộng (Option 2) và cập nhật MaxHP/Mana.
            if (_stats != null) _stats.SetLevelFromProgression(Level);
        }
    }
}
