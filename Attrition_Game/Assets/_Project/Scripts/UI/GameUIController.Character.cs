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
            if (_stats == null || _stats.Object == null || !_stats.Object.IsValid) return;

            // Tên nhân vật: UXML để sẵn placeholder "ARTORIAS" và TRƯỚC ĐÂY không có code nào ghi đè,
            // nên mọi nhân vật đều hiện "ARTORIAS". Lấy tên thật từ GameLaunch (set lúc chọn/tạo slot).
            string charName = Attrition.Persistence.GameLaunch.CharacterName;
            SetText("inv-char-name", string.IsNullOrEmpty(charName) ? "Wanderer" : charName);
            SetText("inv-char-level", $"LEVEL {_stats.Level}");
            SetText("stat-def", _stats.DEF.ToString());
            SetText("stat-res", _stats.RES.ToString());
            SetText("stat-ad", _stats.AD.ToString());
            SetText("stat-ap", _stats.AP.ToString());

            // Bars
            SetFill("inv-hp-fill", _stats.CurrentHP, _stats.MaxHP);
            SetText("inv-hp-label", $"{_stats.CurrentHP}/{_stats.MaxHP}");

            SetFill("inv-mana-fill", _stats.CurrentMana, _stats.MaxMana);
            SetText("inv-mana-label", $"{_stats.CurrentMana}/{_stats.MaxMana}");

            int sta = Mathf.FloorToInt(_stats.CurrentStamina);
            SetFill("inv-stamina-fill", sta, _stats.MaxStamina);
            SetText("inv-stamina-label", $"{sta}/{_stats.MaxStamina}");

            var prog = _stats.GetComponent<Attrition.Gameplay.Player.PlayerProgression>();
            if (prog != null)
            {
                SetFill("inv-exp-fill", prog.CurrentExp, prog.ExpToNext);
                SetText("inv-exp-label", $"EXP {prog.CurrentExp}/{prog.ExpToNext}");
            }

            RefreshEquipSlots();
        }

        public void RefreshAllocPoints()
        {
            // Now handled entirely by the provisional Level Up menu logic in GameUIController.FastTravel.cs
            // to prevent overwriting provisional points during real-time allocation.
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
