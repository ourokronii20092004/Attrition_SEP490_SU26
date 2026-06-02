using UnityEngine;
using Attrition.Core;

namespace Attrition.Data
{
    /// <summary>
    /// STATIC data — chỉ số gốc của nhân vật + tăng trưởng mỗi level (Option 2: tự cộng điểm).
    /// Ship theo build, chơi offline vẫn có. Backend chỉ override DYNAMIC (điểm đã cộng), không sửa file này.
    /// Tạo asset: Create → Attrition → Character Base Stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Character Base Stats", fileName = "CharacterBaseStats")]
    public class CharacterBaseStatsSO : ScriptableObject
    {
        [Header("---- BASE STATS (Level 1) ----")]
        public int baseHP = 100;
        public int baseMana = 100;
        public int baseStamina = 100;
        public int baseAD = 10;
        public int baseAP = 10;
        public int baseDEF = 10;
        public int baseRES = 10;

        [Header("---- LEVELING (Option 2 — tự phân bổ) ----")]
        [Tooltip("Cấp tối đa.")]
        public int maxLevel = 21;
        [Tooltip("Số điểm chỉ số nhận mỗi lần lên cấp, người chơi tự cộng.")]
        public int statPointsPerLevel = 5;

        [Header("---- ĐỘ LỚN MỖI ĐIỂM CỘNG ----")]
        [Tooltip("1 điểm vào HP = +bao nhiêu HP tối đa.")]
        public int hpPerPoint = 20;
        public int manaPerPoint = 10;
        public int staminaPerPoint = 5;
        public int adPerPoint = 2;
        public int apPerPoint = 2;
        public int defPerPoint = 1;
        public int resPerPoint = 1;

        [Header("---- POTION ----")]
        public int startingPotions = 3;
        public int maxPotionsCap = 8;

        /// <summary>Giá trị gốc của 1 stat ở level 1 (chưa cộng điểm, chưa trang bị).</summary>
        public int GetBase(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHP: return baseHP;
                case StatType.MaxMana: return baseMana;
                case StatType.MaxStamina: return baseStamina;
                case StatType.AD: return baseAD;
                case StatType.AP: return baseAP;
                case StatType.DEF: return baseDEF;
                case StatType.RES: return baseRES;
                default: return 0;
            }
        }

        /// <summary>Số điểm chỉ số tích lũy được tới level đã cho (level 1 = 0 điểm).</summary>
        public int TotalStatPointsAtLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 1, maxLevel);
            return (clamped - 1) * statPointsPerLevel;
        }
    }
}
