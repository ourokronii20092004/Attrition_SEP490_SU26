using System;
using UnityEngine;
using Attrition.Core;

namespace Attrition.Data
{
    public enum EquipmentSlot { Head, Chest, Legs, Boots }

    /// <summary>Một dòng cộng chỉ số: chỉ rõ stat nào +bao nhiêu.</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public int amount;
    }

    /// <summary>
    /// STATIC data — 1 món trang bị (mũ/giáp/quần/giày). Cộng def/res/ad/ap/movespd/atkspd
    /// tùy danh sách modifiers. Tạo asset: Create → Attrition → Equipment.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Equipment", fileName = "Equipment")]
    public class EquipmentSO : ScriptableObject
    {
        [Header("---- IDENTITY ----")]
        public string itemId = "iron_helm";
        public string displayName = "Iron Helm";
        [TextArea] public string description;
        public Sprite icon;
        public EquipmentSlot slot = EquipmentSlot.Head;

        [Header("---- STAT BONUSES ----")]
        public StatModifier[] modifiers = Array.Empty<StatModifier>();
    }
}
