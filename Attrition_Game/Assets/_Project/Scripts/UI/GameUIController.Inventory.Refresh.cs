using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Core;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI
{
    /// <summary>Refresh + detail + equip/drop cho Character/Inventory.</summary>
    public partial class GameUIController
    {
        private void RefreshInventory()
        {
            if (_inventory == null || _db == null) return;
            var grid = _root?.Q<VisualElement>("inv-grid");
            if (grid == null) return;

            int count = _activeTab == ItemCategory.Accessory ? 10 : 40;
            int filled = 0;

            for (int i = 0; i < 40; i++)
            {
                var cell = grid.Q<VisualElement>($"cell-{i}");
                if (cell == null) continue;
                bool active = i < count;
                SetVisible(cell, active);
                if (!active) continue;

                var slot = GetSlot(i);
                var icon = cell.Q<VisualElement>($"cell-icon-{i}");
                var label = cell.Q<Label>($"cell-count-{i}");

                if (!slot.IsEmpty && _db.GetItem(slot.ItemIndex) is ItemSO item && IsInTab(item))
                {
                    if (icon != null) icon.style.backgroundImage = item.icon != null ? new StyleBackground(item.icon) : (StyleBackground)StyleKeyword.None;
                    if (label != null) label.text = slot.Amount > 1 ? slot.Amount.ToString() : "";
                    filled++;
                }
                else
                {
                    if (icon != null) icon.style.backgroundImage = StyleKeyword.None;
                    if (label != null) label.text = "";
                }
                cell.RemoveFromClassList("selected");
            }

            SetText("inv-weight-label", $"{filled}/{count}");
            SetFill("inv-weight-fill", filled, count);
        }

        private bool IsInTab(ItemSO item)
        {
            switch (_activeTab)
            {
                case ItemCategory.Equipment: return item is EquipmentSO;
                case ItemCategory.Accessory: return item is AccessorySO;
                case ItemCategory.Skill: return item is SkillSO;
                default: return false;
            }
        }

        private InventorySlot GetSlot(int i)
        {
            if (_activeTab == ItemCategory.Accessory) return _inventory.AccessorySlots.Get(i);
            return _inventory.EquipmentSlots.Get(i); // Equipment + Skill cùng mảng
        }

        private void OnCellClicked(int i)
        {
            var slot = GetSlot(i);
            if (slot.IsEmpty) { SetVisible(_root.Q<VisualElement>("inv-detail"), false); _selectedSlot = -1; return; }
            _selectedSlot = i;
            ShowDetail(slot);

            var grid = _root.Q<VisualElement>("inv-grid");
            for (int k = 0; k < 40; k++) grid.Q<VisualElement>($"cell-{k}")?.RemoveFromClassList("selected");
            grid.Q<VisualElement>($"cell-{i}")?.AddToClassList("selected");
        }

        private void ShowDetail(InventorySlot slot)
        {
            var item = _db.GetItem(slot.ItemIndex);
            if (item == null) return;
            SetVisible(_root.Q<VisualElement>("inv-detail"), true);
            SetText("inv-detail-name", item.displayName);
            SetText("inv-detail-desc", item.description);

            var mods = _root.Q<VisualElement>("inv-detail-mods");
            if (mods != null)
            {
                mods.Clear();
                AppendMods(mods, item);
            }

            // Key item không drop được (BR-45)
            var dropBtn = _root.Q<Button>("inv-detail-drop");
            if (dropBtn != null) dropBtn.SetEnabled(!item.isKeyItem);
        }

        private void AppendMods(VisualElement parent, ItemSO item)
        {
            StatModifier[] arr = null;
            if (item is EquipmentSO eq) arr = eq.modifiers;
            else if (item is AccessorySO acc && acc.kind == AccessoryKind.DamageEffect) arr = acc.modifiers;
            else if (item is SkillSO sk)
            {
                parent.Add(MakeModLabel($"Mana {sk.manaCost}  ·  Cast {sk.castTime:0.0}s"));
                parent.Add(MakeModLabel($"Base DMG {sk.baseDamage}  ({sk.element})"));
                return;
            }
            if (arr == null) return;
            foreach (var m in arr) parent.Add(MakeModLabel($"{m.stat} +{m.amount}"));
        }

        private Label MakeModLabel(string text)
        {
            var l = new Label(text);
            l.AddToClassList("detail-mod");
            return l;
        }

        private void EquipSelected()
        {
            if (_inventory == null || _selectedSlot < 0) return;
            switch (_activeTab)
            {
                case ItemCategory.Equipment: _inventory.RpcRequestEquip(_selectedSlot); break;
                case ItemCategory.Skill: _inventory.RpcRequestEquipSkill(_selectedSlot); break;
                case ItemCategory.Accessory: _inventory.RpcRequestEquipAccessory(_selectedSlot); break;
            }
            SetVisible(_root.Q<VisualElement>("inv-detail"), false);
            _selectedSlot = -1;
        }

        private void DropSelected()
        {
            if (_inventory == null || _selectedSlot < 0) return;
            _inventory.RpcRequestDrop((int)_activeTab, _selectedSlot);
            SetVisible(_root.Q<VisualElement>("inv-detail"), false);
            _selectedSlot = -1;
        }
    }
}
