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

        // ── Build grid 1 lần khi OnEnable ──
        private void BuildInventoryGrid()
        {
            var grid = _root?.Q<VisualElement>("inv-grid");
            if (grid == null) return;
            grid.Clear();

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
                cell.RegisterCallback<ClickEvent>(_ => OnCellClicked(idx));
                grid.Add(cell);
            }
        }

        private void SetupInventoryControls()
        {
            BindTab("inv-tab-equipment", ItemCategory.Equipment);
            BindTab("inv-tab-accessory", ItemCategory.Accessory);
            BindTab("inv-tab-skill", ItemCategory.Skill);

            BindButton("inv-detail-equip", EquipSelected);
            BindButton("inv-detail-drop", DropSelected);

            BindAlloc("alloc-MaxHP", StatType.MaxHP);
            BindAlloc("alloc-MaxMana", StatType.MaxMana);
            BindAlloc("alloc-AD", StatType.AD);
            BindAlloc("alloc-AP", StatType.AP);
            BindAlloc("alloc-DEF", StatType.DEF);
            BindAlloc("alloc-RES", StatType.RES);

            // Gỡ trang bị khi click vào ô trang bị
            BindButton("equip-head", () => { if (_inventory != null) _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Head); });
            BindButton("equip-chest", () => { if (_inventory != null) _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Chest); });
            BindButton("equip-legs", () => { if (_inventory != null) _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Legs); });
            BindButton("equip-boots", () => { if (_inventory != null) _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Boots); });
            BindButton("equip-accessory", () => { if (_inventory != null) _inventory.RpcRequestUnequipAccessory(); });
            BindButton("equip-skill", () => { if (_inventory != null) _inventory.RpcRequestUnequipSkill(); });
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
