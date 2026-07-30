using System;
using UnityEngine;

namespace Attrition.Data
{
    /// <summary>
    /// Hai dạng accessory theo concept:
    /// - DamageEffect: gây hiệu ứng liên quan sát thương, PHẢI trang bị (chỉ 1 ô).
    /// - AbilityGrant: cấp kỹ năng nền (double jump, shadow dash...), KHÔNG cần trang bị —
    ///   sở hữu là tự áp dụng vào skill ban đầu của nhân vật ( bên phần excel))
    /// </summary>
    public enum AccessoryKind { DamageEffect, AbilityGrant }

    /// <summary>Kỹ năng nền mà AbilityGrant accessory có thể mở khóa.</summary>
    public enum GrantedAbility { None, DoubleJump, ShadowDash }

    /// <summary>
    /// Hiệu ứng đặc biệt của accessory DamageEffect (mỗi accessory 1 loại). Chưa có asset nên chỉ là
    /// data + logic; AccessoryEffects (trên player) đọc effect đang trang bị rồi thực thi.
    /// </summary>
    public enum DamageEffectType
    {
        None,
        Burn,             // Thiêu đốt: đánh trúng quái → gây sát thương theo thời gian.
        Slow,             // Làm chậm: đánh trúng quái → giảm tốc di chuyển quái trong effectDuration.
        Lifesteal,        // Hút máu: đánh trúng quái → hồi HP theo % sát thương gây ra.
        HealthRegen,      // Hồi máu theo NHỊP: cứ effectCooldown giây hồi effectMagnitude HP.
        PotionBoost,      // Tăng hiệu quả hồi HP khi uống bình máu (% cộng thêm).
        DamageShield,     // Gây sát thương → tạo lá chắn tạm (có cooldown).
        PostSkillDamage   // Sau khi dùng skill → đòn đánh KẾ TIẾP tăng sát thương.
    }

    /// <summary>
    /// STATIC data — accessory. Tạo asset: Create → Attrition → Accessory.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Accessory", fileName = "Accessory")]
    public class AccessorySO : ItemSO
    {
        [Header("---- KIND ----")]
        public AccessoryKind kind = AccessoryKind.AbilityGrant;

        [Tooltip("Chỉ dùng khi kind = AbilityGrant.")]
        public GrantedAbility grantedAbility = GrantedAbility.DoubleJump;

        [Header("---- DAMAGE EFFECT (kind = DamageEffect) ----")]
        [Tooltip("Modifiers cộng thêm khi accessory dạng damage được trang bị.")]
        public StatModifier[] modifiers = Array.Empty<StatModifier>();

        [Tooltip("Loại hiệu ứng đặc biệt khi trang bị (mỗi accessory 1 loại). None = chỉ cộng modifiers.")]
        public DamageEffectType effect = DamageEffectType.None;

        [Header("---- THAM SỐ HIỆU ỨNG (dùng theo effect) ----")]
        [Tooltip("Burn: tổng sát thương thiêu đốt / Slow: (không dùng) / Lifesteal: % máu hút (0.2 = 20%) / "
               + "HealthRegen: HP hồi MỖI NHỊP (nhịp = effectCooldown giây) / PotionBoost: % tăng hồi bình (0.3 = +30%) / "
               + "DamageShield: lượng lá chắn / PostSkillDamage: hệ số nhân (1.5 = +50%).")]
        public float effectMagnitude = 0f;

        [Tooltip("Thời lượng hiệu ứng (giây). Burn/Slow: thời gian hiệu lực. DamageShield: thời gian lá chắn tồn tại. "
               + "Không dùng cho Lifesteal/PostSkillDamage.")]
        public float effectDuration = 3f;

        [Tooltip("Ngưỡng kích hoạt (tỉ lệ 0..1 của Max HP). HIỆN KHÔNG DÙNG — HealthRegen đã đổi sang hồi "
               + "theo nhịp cố định, không phụ thuộc HP còn lại.")]
        public float effectThreshold = 0.5f;

        [Tooltip("HIỆN KHÔNG DÙNG (xem effectThreshold).")]
        public float effectThresholdStop = 0.8f;

        [Tooltip("Cooldown hồi hiệu ứng (giây). DamageShield: giãn cách giữa 2 lần tạo khiên. "
               + "HealthRegen: giãn cách giữa 2 nhịp hồi HP.")]
        public float effectCooldown = 8f;

        public override ItemCategory Category => ItemCategory.Accessory;
    }
}
