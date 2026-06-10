using UnityEngine;
using Fusion;
using Attrition.Core;

namespace Attrition.Data
{
    /// <summary>5 skill nguyên tố tương ứng 5 boss khu vực.</summary>
    public enum SkillElement { Fire, Wood, Earth, Thunder, Thrust }

    /// <summary>Cách skill gây sát thương: vùng tức thời, hoặc bắn đạn.</summary>
    public enum SkillDelivery { AreaInstant, Projectile }

    /// <summary>Hình dạng vùng đánh của skill (độc lập với enemy để tránh phụ thuộc assembly).</summary>
    public enum SkillHitShape { Cone, Circle, Rectangle }

    /// <summary>
    /// STATIC data — 1 skill chủ động. Nhận sau khi hạ boss khu vực; trang bị tối đa 1.
    /// MỖI skill tự định nghĩa hitbox + hiệu ứng riêng nên cast ra khác nhau.
    /// Cast khựng (không di chuyển) trong castTime giây — KHÔNG cần animation.
    ///
    /// Cải tiến theo game hành động nổi tiếng:
    ///  - activeStartFrac/activeEndFrac: hitbox chỉ "sống" trong 1 khoảng của castTime (active frames).
    ///  - tickInterval: skill kéo dài có thể gây nhiều hit (multi-hit / lingering AoE) thay vì 1 phát.
    ///  - sweetSpot: vùng lõi gây thêm % damage (thưởng người chơi căn tầm chuẩn).
    /// Tạo asset: Create → Attrition → Skill.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Skill", fileName = "Skill")]
    public class SkillSO : ItemSO
    {
        [Header("---- SKILL ----")]
        public SkillElement element = SkillElement.Fire;

        [Header("---- COST & TIMING ----")]
        public int manaCost = 20;
        [Tooltip("Thời gian khựng khi cast (0.5–1s). Không cần animation.")]
        public float castTime = 0.6f;
        public float cooldown = 2f;

        [Header("---- ACTIVE FRAMES (theo % castTime) ----")]
        [Tooltip("Hitbox bắt đầu sống ở mốc này (0..1 của castTime). VD 0.3 = sau 30% wind-up mới ra đòn.")]
        [Range(0f, 1f)] public float activeStartFrac = 0.35f;
        [Tooltip("Hitbox tắt ở mốc này (0..1). Khoảng [start,end] = active frames.")]
        [Range(0f, 1f)] public float activeEndFrac = 0.6f;

        [Header("---- DAMAGE ----")]
        public DamageType damageType = DamageType.Magic;
        [Tooltip("Sát thương gốc trước khi trừ RES/DEF mục tiêu.")]
        public int baseDamage = 30;
        [Tooltip("Hệ số nhân theo AP của người chơi (baseDamage + AP*scale).")]
        public float apScaling = 1f;
        public float knockbackForce = 7f;
        [Tooltip("Khoảng cách giữa 2 lần gây damage khi skill kéo dài (multi-hit/lingering). 0 = chỉ 1 hit.")]
        public float tickInterval = 0f;

        [Header("---- SWEET SPOT (vùng lõi thưởng damage) ----")]
        [Tooltip("Bán kính lõi quanh tâm hitbox; trúng trong đây được nhân damage. 0 = tắt.")]
        public float sweetSpotRadius = 0f;
        [Tooltip("Hệ số damage khi trúng sweet spot (vd 1.5 = +50%).")]
        public float sweetSpotMultiplier = 1.5f;

        [Header("---- DELIVERY (mỗi skill 1 kiểu) ----")]
        public SkillDelivery delivery = SkillDelivery.AreaInstant;

        [Header("• Area Instant")]
        public SkillHitShape hitShape = SkillHitShape.Cone;
        [Tooltip("Bán kính/tầm với vùng đánh.")]
        public float range = 2.5f;
        [Tooltip("Góc quạt (chỉ Cone).")]
        [Range(0, 360)] public float angle = 120f;
        [Tooltip("Kích thước hộp (chỉ Rectangle).")]
        public Vector2 rectSize = new Vector2(3f, 1.5f);
        [Tooltip("Offset tâm vùng đánh so với player (X tự lật theo hướng nhìn).")]
        public Vector2 hitboxOffset = new Vector2(1f, 0f);

        [Header("• Projectile")]
        [Tooltip("Prefab đạn (NetworkObject có EnemyProjectile/SpearProjectile, hitLayer = Enemy).")]
        public NetworkPrefabRef projectilePrefab;
        public float projectileSpeed = 12f;
        [Tooltip("Số đạn 1 lần (>1 = toả quạt theo spreadAngle).")]
        public int projectileCount = 1;
        public float spreadAngle = 20f;

        [Header("---- VFX (riêng từng skill) ----")]
        [Tooltip("Prefab hiệu ứng tại điểm cast. Bỏ trống = không.")]
        public GameObject castVfxPrefab;
        [Tooltip("Đời sống VFX (giây). 0 = không tự huỷ.")]
        public float vfxLifetime = 1.5f;

        public override ItemCategory Category => ItemCategory.Skill;

        /// <summary>Số lần tick damage trong active window (>=1).</summary>
        public int ComputeTickCount()
        {
            if (tickInterval <= 0f) return 1;
            float activeDur = Mathf.Max(0f, (activeEndFrac - activeStartFrac)) * castTime;
            return Mathf.Max(1, Mathf.FloorToInt(activeDur / tickInterval) + 1);
        }
    }
}
