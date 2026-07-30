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
    public class PlayerProgression : NetworkBehaviour
    {
        [Header("---- LEVELING CONFIG ----")]
        [Tooltip("SO chứa toàn bộ cấu hình EXP và thăng cấp")]
        [SerializeField] private Attrition.Data.LevelingConfigSO levelingConfig;

        public Attrition.Data.LevelingConfigSO GetLevelingConfig()
        {
            return levelingConfig;
        }

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
        public int ExpToNext => levelingConfig != null ? levelingConfig.baseExp + (Mathf.Max(1, Level) - 1) * levelingConfig.perLevelExp : 100;

        /// <summary>Cộng EXP (chỉ host). Tự xử lý lên cấp, kể cả nhiều cấp 1 lần.</summary>
        public void GainExp(int amount)
        {
            if (!HasStateAuthority || amount <= 0) return;
            if (_stats == null) _stats = GetComponent<PlayerStats>();

            int maxLevel = levelingConfig != null ? levelingConfig.maxLevel : 21;
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

            // Hiệu ứng lên cấp hiện giữa người. PlayerVfx tự broadcast RPC (hàm này host-only) nên cả
            // client cũng thấy; no-op nếu prefab chưa gắn component.
            var vfx = GetComponent<PlayerVfx>();
            if (vfx != null) vfx.PlayLevelUp();
        }

        /// <summary>
        /// Khi chết: mất thanh EXP đang tích dồn cho cấp kế tiếp (CurrentExp về 0), GIỮ NGUYÊN Level
        /// và mọi điểm cộng đã có. Chỉ host. Không reset nếu đã ở max level (CurrentExp vốn = 0).
        /// </summary>
        public void ResetExpProgressOnDeath()
        {
            if (!HasStateAuthority) return;
            CurrentExp = 0;
        }
    }
}
