using System;
using UnityEngine;

namespace Attrition.Data
{
    /// <summary>
    /// Hai dạng accessory theo concept:
    /// - DamageEffect: gây hiệu ứng liên quan sát thương, PHẢI trang bị (chỉ 1 ô).
    /// - AbilityGrant: cấp kỹ năng nền (double jump, shadow dash...), KHÔNG cần trang bị —
    ///   sở hữu là tự áp dụng vào skill ban đầu của nhân vật.
    /// </summary>
    public enum AccessoryKind { DamageEffect, AbilityGrant }

    /// <summary>Kỹ năng nền mà AbilityGrant accessory có thể mở khóa.</summary>
    public enum GrantedAbility { None, DoubleJump, ShadowDash }

    /// <summary>
    /// STATIC data — accessory. Tạo asset: Create → Attrition → Accessory.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Accessory", fileName = "Accessory")]
    public class AccessorySO : ScriptableObject
    {
        [Header("---- IDENTITY ----")]
        public string accessoryId = "double_jump_charm";
        public string displayName = "Double Jump Charm";
        [TextArea] public string description;
        public Sprite icon;

        [Header("---- KIND ----")]
        public AccessoryKind kind = AccessoryKind.AbilityGrant;

        [Tooltip("Chỉ dùng khi kind = AbilityGrant.")]
        public GrantedAbility grantedAbility = GrantedAbility.DoubleJump;

        [Header("---- DAMAGE EFFECT (kind = DamageEffect) ----")]
        [Tooltip("Modifiers cộng thêm khi accessory dạng damage được trang bị.")]
        public StatModifier[] modifiers = Array.Empty<StatModifier>();
    }
}
