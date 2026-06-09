using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Core;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI
{
    /// <summary>Left panel: equip slots + stats + level + unspent points.</summary>
    public partial class GameUIController
    {
        private void RefreshCharacterPanel()
        {
            if (_stats == null) return;

            SetText("inv-char-level", $"LEVEL {_stats.Level}");
            SetText("stat-def", _stats.DEF.ToString());
            SetText("stat-res", _stats.RES.ToString());
            SetText("stat-ad", _stats.AD.ToString());
            SetText("stat-ap", _stats.AP.ToString());

            int unspent = _stats.UnspentPoints;
            SetText("inv-points", $"UNSPENT POINTS: {unspent}");
            SetAllocEnabled(unspent > 0);

            RefreshEquipSlots();
        }

        private void SetAllocEnabled(bool on)
        {
            string[] names = { "alloc-MaxHP", "alloc-MaxMana", "alloc-AD", "alloc-AP", "alloc-DEF", "alloc-RES" };
            foreach (var n in names) _root.Q<Button>(n)?.SetEnabled(on);
        }

        private void RefreshEquipSlots()
        {
            if (_inventory == null || _db == null) return;
            SetEquipIcon("equip-head-icon", _inventory.EquippedHead);
            SetEquipIcon("equip-chest-icon", _inventory.EquippedChest);
            SetEquipIcon("equip-legs-icon", _inventory.EquippedLegs);
            SetEquipIcon("equip-boots-icon", _inventory.EquippedBoots);
            SetEquipIcon("equip-accessory-icon", _inventory.EquippedAccessory);
            SetEquipIcon("equip-skill-icon", _inventory.EquippedSkill);
        }

        private void SetEquipIcon(string iconName, InventorySlot slot)
        {
            var icon = _root.Q<VisualElement>(iconName);
            if (icon == null) return;
            if (!slot.IsEmpty && _db.GetItem(slot.ItemIndex) is ItemSO item && item.icon != null)
                icon.style.backgroundImage = new StyleBackground(item.icon);
            else
                icon.style.backgroundImage = StyleKeyword.None;
        }
    }
}
