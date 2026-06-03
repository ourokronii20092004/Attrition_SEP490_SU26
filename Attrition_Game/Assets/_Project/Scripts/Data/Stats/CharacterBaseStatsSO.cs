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

        [Header("---- MOVEMENT & COMBAT ----")]
        public float moveSpeed = 10f;
        public float dashSpeed = 20f;
        public float slideSpeed = 15f;
        public float jumpForce = 15f;
        public float doubleJumpForce = 8f;
        public float attackSpeed = 1f;
        [Tooltip("Hệ số nhân sát thương khi Charge Attack (ví dụ: 2.0 = gấp đôi sát thương cơ bản)")]
        public float chargeDamageMultiplier = 2f;

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
    }
}
