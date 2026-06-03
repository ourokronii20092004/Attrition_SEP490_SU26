using UnityEngine;
using Attrition.Core;

namespace Attrition.Data
{
    /// <summary>5 skill nguyên tố tương ứng 5 boss khu vực.</summary>
    public enum SkillElement { Fire, Wood, Earth, Thunder, Thrust }

    /// <summary>
    /// STATIC data — 1 skill chủ động. Nhận sau khi hạ boss khu vực; trang bị tối đa 1.
    /// Cast khựng (không di chuyển) trong castTime giây. Tạo asset: Create → Attrition → Skill.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Skill", fileName = "Skill")]
    public class SkillSO : ItemSO
    {
        [Header("---- SKILL ----")]
        public SkillElement element = SkillElement.Fire;

        [Header("---- COST & TIMING ----")]
        public int manaCost = 20;
        [Tooltip("Thời gian khựng khi cast (0.5–1s tùy skill).")]
        public float castTime = 0.6f;
        public float cooldown = 2f;

        [Header("---- DAMAGE ----")]
        public DamageType damageType = DamageType.Magic;
        [Tooltip("Sát thương gốc trước khi trừ RES/DEF mục tiêu.")]
        public int baseDamage = 30;
        [Tooltip("Hệ số nhân theo AP của người chơi (baseDamage + AP*scale).")]
        public float apScaling = 1f;

        public override ItemCategory Category => ItemCategory.Skill;
    }
}
