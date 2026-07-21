using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Core;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI
{
    /// <summary>
    /// Character/Inventory (Tab) cho GameUIController.
    /// 3 tab: Equipment / Accessory / Skill. Click ô → detail panel; phải-click hoặc nút EQUIP để mặc.
    /// Cộng điểm tự do (Option 2) qua RpcRequestAllocate. Tất cả host-authoritative.
    /// </summary>
    public partial class GameUIController
    {
        private const int InvCols = 8;
        private ItemCategory _activeTab = ItemCategory.Equipment;
        private int _selectedSlot = -1;

        public enum SelectedSlotContext { None, InventoryGrid, EquippedHead, EquippedChest, EquippedLegs, EquippedBoots, EquippedAccessory, EquippedSkill }
        private SelectedSlotContext _selectedContext = SelectedSlotContext.None;

        private void BuildInventoryGrid()
        {
            var grid = _root?.Q<VisualElement>("inv-grid");
            if (grid == null) return;
            grid.Clear();

            SetupDragAndDrop(); // Tạo ghost element

            for (int i = 0; i < 40; i++)
            {
                int idx = i;
                var cell = new VisualElement { name = $"cell-{i}" };
                cell.AddToClassList("inv-cell");
                var icon = new VisualElement { name = $"cell-icon-{i}" };
                icon.AddToClassList("inv-cell-icon");
                cell.Add(icon);
                var count = new Label { name = $"cell-count-{i}", text = "" };
                count.AddToClassList("inv-cell-count");
                cell.Add(count);
                
                RegisterDragCallbacks(cell, SelectedSlotContext.InventoryGrid, idx);
                grid.Add(cell);
            }
        }

        private void SetupInventoryControls()
        {
            BindTab("inv-tab-equipment", ItemCategory.Equipment);
            BindTab("inv-tab-accessory", ItemCategory.Accessory);
            BindTab("inv-tab-skill", ItemCategory.Skill);

            BindButton("inv-detail-equip", EquipOrUnequipSelected);
            BindButton("inv-detail-drop", DropSelected);

            BindAlloc("alloc-MaxHP", StatType.MaxHP);
            BindAlloc("alloc-MaxMana", StatType.MaxMana);
            BindAlloc("alloc-AD", StatType.AD);
            BindAlloc("alloc-AP", StatType.AP);
            BindAlloc("alloc-DEF", StatType.DEF);
            BindAlloc("alloc-RES", StatType.RES);

            // Chọn trang bị đang mặc để hiển thị detail panel thay vì gỡ ngay lập tức
            BindButton("equip-head", () => OnEquipSlotClicked(SelectedSlotContext.EquippedHead));
            BindButton("equip-chest", () => OnEquipSlotClicked(SelectedSlotContext.EquippedChest));
            BindButton("equip-legs", () => OnEquipSlotClicked(SelectedSlotContext.EquippedLegs));
            BindButton("equip-boots", () => OnEquipSlotClicked(SelectedSlotContext.EquippedBoots));
            BindButton("equip-accessory", () => OnEquipSlotClicked(SelectedSlotContext.EquippedAccessory));
            BindButton("equip-skill", () => OnEquipSlotClicked(SelectedSlotContext.EquippedSkill));

            // Đăng ký kéo thả cho các ô trang bị
            if (_root.Q<Button>("equip-head") is Button headBtn) RegisterDragCallbacks(headBtn, SelectedSlotContext.EquippedHead);
            if (_root.Q<Button>("equip-chest") is Button chestBtn) RegisterDragCallbacks(chestBtn, SelectedSlotContext.EquippedChest);
            if (_root.Q<Button>("equip-legs") is Button legsBtn) RegisterDragCallbacks(legsBtn, SelectedSlotContext.EquippedLegs);
            if (_root.Q<Button>("equip-boots") is Button bootsBtn) RegisterDragCallbacks(bootsBtn, SelectedSlotContext.EquippedBoots);
            if (_root.Q<Button>("equip-accessory") is Button accBtn) RegisterDragCallbacks(accBtn, SelectedSlotContext.EquippedAccessory);
            if (_root.Q<Button>("equip-skill") is Button skillBtn) RegisterDragCallbacks(skillBtn, SelectedSlotContext.EquippedSkill);
        }



        private void BindTab(string name, ItemCategory cat)
        {
            var b = _root.Q<Button>(name);
            if (b != null) b.clicked += () => SwitchTab(cat);
        }

        private void BindButton(string name, System.Action act)
        {
            var b = _root.Q<Button>(name);
            if (b != null) b.clicked += act;
        }

        private void BindAlloc(string name, StatType stat)
        {
            var b = _root.Q<Button>(name);
            if (b != null) b.clicked += () => { if (_stats != null) _stats.RpcRequestAllocate((int)stat); };
        }

        private void SwitchTab(ItemCategory cat)
        {
            _activeTab = cat;
            _selectedSlot = -1;
            SetTabActive("inv-tab-equipment", cat == ItemCategory.Equipment);
            SetTabActive("inv-tab-accessory", cat == ItemCategory.Accessory);
            SetTabActive("inv-tab-skill", cat == ItemCategory.Skill);
            SetVisible(_root.Q<VisualElement>("inv-detail"), false);
            RefreshInventory();
        }

        private void SetTabActive(string name, bool active)
        {
            var b = _root.Q<Button>(name);
            if (b == null) return;
            if (active) { if (!b.ClassListContains("active")) b.AddToClassList("active"); }
            else b.RemoveFromClassList("active");
        }
    }
}
