using UnityEngine;
using Attrition.Core;

namespace Attrition.Data
{
    /// <summary>
    /// STATIC data — chỉ số mặc định của 1 loại quái (thường / elite / boss).
    /// Backend có thể override các giá trị này (admin chỉnh trên web) — xem EnemyStatOverride.
    /// enemyId là khóa khớp với bản ghi trên Postgres/Redis.
    /// Tạo asset: Create → Attrition → Enemy Stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Enemy Stats", fileName = "EnemyStats")]
    public class EnemyStatsSO : ScriptableObject
    {
        [Header("---- IDENTITY ----")]
        [Tooltip("Khóa duy nhất khớp với DB (web sửa stats theo id này).")]
        public string enemyId = "skeleton_sword";
        public EnemyTier tier = EnemyTier.Normal;

        [Header("---- COMBAT STATS ----")]
        public int maxHP = 30;
        public int ad = 10;
        public int ap = 0;
        public int def = 0;
        public int res = 0;

        [Header("---- ELITE / BOSS ----")]
        [Tooltip("Poise: elite không có hit-stun thường; nhận sát thương trừ poise, =0 mới choáng. 0 = bỏ qua.")]
        public int poise = 0;

        [Header("---- REWARD ----")]
        [Tooltip("EXP cộng cho MỖI player khi quái này chết (coop: như nhau cho cả 2).")]
        public int expReward = 10;

        [Header("---- COOP SCALING ----")]
        [Tooltip("Hệ số nhân chỉ số khi chơi 2 người (HP/AD...). 1 = không đổi.")]
        public float coopHpMultiplier = 1.6f;
        public float coopDamageMultiplier = 1.2f;

        public int GetBase(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHP: return maxHP;
                case StatType.AD: return ad;
                case StatType.AP: return ap;
                case StatType.DEF: return def;
                case StatType.RES: return res;
                default: return 0;
            }
        }
    }

    public enum EnemyTier { Normal, Elite, Boss }
}
