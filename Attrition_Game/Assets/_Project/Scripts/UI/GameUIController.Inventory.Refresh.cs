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
        // Map ô hiển thị (cell) → index THẬT trong NetworkArray.
        // Tab Equipment và Skill dùng CHUNG mảng EquipmentSlots, nên nếu vẽ theo index thô thì ô chứa
        // item của tab khác bị xoá icon nhưng VẪN hiện và VẪN bấm được: bấm vào tưởng chọn skill,
        // thực ra chọn giáp → RpcRequestEquipSkill bị chặn ở "is not SkillSO" và im lặng không làm gì.
        // Dồn item của tab về đầu grid, ghi index thật vào đây để equip/drop/swap tác động đúng ô mảng.
        private readonly int[] _cellToSlot = new int[40];

        private void RefreshInventory()
        {
            if (_inventory == null || _db == null || _inventory.Object == null || !_inventory.Object.IsValid) return;
            var grid = _root?.Q<VisualElement>("inv-grid");
            if (grid == null) return;

            int capacity = _activeTab == ItemCategory.Accessory ? 10 : 40;
            int filled = 0;

            for (int k = 0; k < 40; k++) _cellToSlot[k] = -1;

            // 1) Ô có item thuộc tab hiện tại → dồn về đầu grid.
            for (int src = 0; src < capacity && filled < 40; src++)
            {
                var s = GetRawSlot(src);
                if (s.IsEmpty) continue;
                if (_db.GetItem(s.ItemIndex) is not ItemSO it || !IsInTab(it)) continue;

                PaintCell(grid, filled, it, s.Amount);
                _cellToSlot[filled] = src;
                filled++;
            }

            // 2) Ô còn lại: trống, nhưng vẫn map tới index THẬT đang trống để kéo-thả vào không lệch ô.
            int nextFree = 0;
            for (int c = filled; c < 40; c++)
            {
                if (c < capacity)
                {
                    while (nextFree < capacity && !GetRawSlot(nextFree).IsEmpty) nextFree++;
                    if (nextFree < capacity) _cellToSlot[c] = nextFree++;
                }
                PaintCell(grid, c, null, 0, visible: c < capacity);
            }

            SetText("inv-weight-label", $"{filled}/{capacity}");
            SetFill("inv-weight-fill", filled, capacity);

            // Ô trang bị bên trái đọc từ EquippedHead/Skill/... — cũng đổi khi equip/unequip.
            // OnInventoryChanged chỉ gọi RefreshInventory, nên nếu không refresh ở đây thì icon ô
            // đã trang bị chỉ hiện sau khi đóng/mở lại panel (RefreshCharacterPanel).
            RefreshEquipSlots();
        }

        private void PaintCell(VisualElement grid, int cellIndex, ItemSO item, int amount, bool visible = true)
        {
            var cell = grid.Q<VisualElement>($"cell-{cellIndex}");
            if (cell == null) return;
            SetVisible(cell, visible);

            var icon = cell.Q<VisualElement>($"cell-icon-{cellIndex}");
            var label = cell.Q<Label>($"cell-count-{cellIndex}");
            if (icon != null)
                icon.style.backgroundImage = item != null && item.icon != null
                    ? new StyleBackground(item.icon)
                    : (StyleBackground)StyleKeyword.None;
            if (label != null) label.text = amount > 1 ? amount.ToString() : "";
            cell.RemoveFromClassList("selected");
        }

        /// <summary>Index THẬT trong NetworkArray của ô hiển thị thứ <paramref name="cellIndex"/>; -1 = không map.</summary>
        private int MapCell(int cellIndex)
            => cellIndex >= 0 && cellIndex < 40 ? _cellToSlot[cellIndex] : -1;

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

        /// <summary>Đọc slot theo index THẬT trong mảng (không qua map ô hiển thị).</summary>
        private InventorySlot GetRawSlot(int i)
        {
            if (_activeTab == ItemCategory.Accessory) return _inventory.AccessorySlots.Get(i);
            return _inventory.EquipmentSlots.Get(i); // Equipment + Skill cùng mảng
        }

        /// <summary>Đọc slot theo ô hiển thị trên grid (đã dồn item của tab về đầu).</summary>
        private InventorySlot GetSlot(int cellIndex)
        {
            int real = MapCell(cellIndex);
            return real < 0 ? InventorySlot.Empty : GetRawSlot(real);
        }

        private void OnCellClicked(int i)
        {
            var slot = GetSlot(i);
            if (slot.IsEmpty) { SetVisible(_root.Q<VisualElement>("inv-detail"), false); _selectedSlot = -1; _selectedContext = SelectedSlotContext.None; return; }
            // Lưu index THẬT: equip/drop gửi thẳng qua RPC nên phải là index trong mảng, không phải số ô.
            _selectedSlot = MapCell(i);
            _selectedContext = SelectedSlotContext.InventoryGrid;
            ShowDetail(slot);

            var grid = _root.Q<VisualElement>("inv-grid");
            for (int k = 0; k < 40; k++) grid.Q<VisualElement>($"cell-{k}")?.RemoveFromClassList("selected");
            grid.Q<VisualElement>($"cell-{i}")?.AddToClassList("selected");
        }

        private void OnEquipSlotClicked(SelectedSlotContext context)
        {
            if (_inventory == null || _db == null) return;
            InventorySlot slot = InventorySlot.Empty;
            switch (context)
            {
                case SelectedSlotContext.EquippedHead: slot = _inventory.EquippedHead; break;
                case SelectedSlotContext.EquippedChest: slot = _inventory.EquippedChest; break;
                case SelectedSlotContext.EquippedLegs: slot = _inventory.EquippedLegs; break;
                case SelectedSlotContext.EquippedBoots: slot = _inventory.EquippedBoots; break;
                case SelectedSlotContext.EquippedAccessory: slot = _inventory.EquippedAccessory; break;
                case SelectedSlotContext.EquippedSkill: slot = _inventory.EquippedSkill; break;
            }

            if (slot.IsEmpty) { SetVisible(_root.Q<VisualElement>("inv-detail"), false); _selectedContext = SelectedSlotContext.None; return; }
            
            _selectedSlot = -1; // Not in grid
            _selectedContext = context;
            
            // Remove grid selection visual
            var grid = _root.Q<VisualElement>("inv-grid");
            if (grid != null)
                for (int k = 0; k < 40; k++) grid.Q<VisualElement>($"cell-{k}")?.RemoveFromClassList("selected");

            ShowDetail(slot);
        }

        private void ShowDetail(InventorySlot slot)
        {
            var item = _db.GetItem(slot.ItemIndex);
            if (item == null) return;
            SetVisible(_root.Q<VisualElement>("inv-detail"), true);
            SetText("inv-detail-name", Attrition.Persistence.ItemRuntimeConfig.Name(item));
            SetText("inv-detail-desc", Attrition.Persistence.ItemRuntimeConfig.Description(item));

            var mods = _root.Q<VisualElement>("inv-detail-mods");
            if (mods != null)
            {
                mods.Clear();
                AppendMods(mods, item);
            }

            var equipBtn = _root.Q<Button>("inv-detail-equip");
            if (equipBtn != null)
            {
                equipBtn.text = _selectedContext == SelectedSlotContext.InventoryGrid ? "EQUIP" : "UNEQUIP";
                // Accessory kiểu AbilityGrant tự áp dụng khi CHỈ CẦN có trong túi, không có ô để mặc:
                // TryEquipAccessoryFromSlot chặn nó và trả false, nút bấm không phản hồi gì.
                bool blocked = _selectedContext == SelectedSlotContext.InventoryGrid
                               && item is AccessorySO a && a.kind != AccessoryKind.DamageEffect;
                equipBtn.SetEnabled(!blocked);
            }

            // Skill / accessory / key item không vứt được → ẨN nút DROP hẳn (trước chỉ disable nên vẫn
            // thấy nút xám, bấm không có gì xảy ra). Cùng một điều kiện host dùng để chặn RPC.
            SetVisible(_root.Q<Button>("inv-detail-drop"), PlayerInventory.CanDrop(item));
        }

        private void AppendMods(VisualElement parent, ItemSO item)
        {
            StatModifier[] arr = null;
            if (item is EquipmentSO eq) arr = eq.modifiers;
            else if (item is AccessorySO acc && acc.kind == AccessoryKind.DamageEffect) arr = acc.modifiers;
            else if (item is SkillSO sk)
            {
                var runtime = Attrition.Persistence.SkillRuntimeConfig.From(sk);
                parent.Add(MakeModLabel($"Mana {runtime.manaCost}  ·  Cast {runtime.castTime:0.0}s"));
                parent.Add(MakeModLabel($"Base DMG {runtime.baseDamage}  ({runtime.element})"));
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

        private void EquipOrUnequipSelected()
        {
            if (_inventory == null || _selectedContext == SelectedSlotContext.None) return;

            if (_selectedContext == SelectedSlotContext.InventoryGrid)
            {
                if (_selectedSlot < 0) return;
                switch (_activeTab)
                {
                    case ItemCategory.Equipment: _inventory.RpcRequestEquip(_selectedSlot); break;
                    case ItemCategory.Skill: _inventory.RpcRequestEquipSkill(_selectedSlot); break;
                    case ItemCategory.Accessory: _inventory.RpcRequestEquipAccessory(_selectedSlot); break;
                }
            }
            else
            {
                switch (_selectedContext)
                {
                    case SelectedSlotContext.EquippedHead: _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Head); break;
                    case SelectedSlotContext.EquippedChest: _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Chest); break;
                    case SelectedSlotContext.EquippedLegs: _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Legs); break;
                    case SelectedSlotContext.EquippedBoots: _inventory.RpcRequestUnequipArmor((int)EquipmentSlot.Boots); break;
                    case SelectedSlotContext.EquippedAccessory: _inventory.RpcRequestUnequipAccessory(); break;
                    case SelectedSlotContext.EquippedSkill: _inventory.RpcRequestUnequipSkill(); break;
                }
            }
            SetVisible(_root.Q<VisualElement>("inv-detail"), false);
            _selectedContext = SelectedSlotContext.None;
            _selectedSlot = -1;
        }

        private void DropSelected()
        {
            if (_inventory == null || _selectedContext == SelectedSlotContext.None) return;
            
            if (_selectedContext == SelectedSlotContext.InventoryGrid)
            {
                if (_selectedSlot < 0) return;
                _inventory.RpcRequestDrop((int)_activeTab, _selectedSlot);
            }
            else
            {
                switch (_selectedContext)
                {
                    case SelectedSlotContext.EquippedHead: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Head); break;
                    case SelectedSlotContext.EquippedChest: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Chest); break;
                    case SelectedSlotContext.EquippedLegs: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Legs); break;
                    case SelectedSlotContext.EquippedBoots: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Boots); break;
                    case SelectedSlotContext.EquippedAccessory: _inventory.RpcRequestDropEquippedAccessory(); break;
                    case SelectedSlotContext.EquippedSkill: _inventory.RpcRequestDropEquippedSkill(); break;
                }
            }
            
            SetVisible(_root.Q<VisualElement>("inv-detail"), false);
            _selectedContext = SelectedSlotContext.None;
            _selectedSlot = -1;
        }
    }
}
