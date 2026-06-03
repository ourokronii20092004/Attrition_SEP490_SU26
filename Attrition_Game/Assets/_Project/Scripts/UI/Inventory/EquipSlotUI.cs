using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI.Inventory
{
    /// <summary>
    /// Component cho 6 ô trang bị đang mặc (4 armor + 1 skill + 1 accessory).
    /// Right-click để gỡ trang bị (unequip → về inventory).
    /// </summary>
    public class EquipSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image highlightBorder;

        private InventoryUI _ui;
        private PlayerInventory _inventory;
        private InventorySlot _currentSlot;

        // Loại slot
        private enum SlotKind { Armor, Skill, Accessory }
        private SlotKind _kind;
        private EquipmentSlot _armorSlot;

        public void Setup(InventoryUI ui, PlayerInventory inventory, EquipmentSlot armorSlot)
        {
            _ui = ui;
            _inventory = inventory;
            _kind = SlotKind.Armor;
            _armorSlot = armorSlot;
        }

        public void SetupSkill(InventoryUI ui, PlayerInventory inventory)
        {
            _ui = ui;
            _inventory = inventory;
            _kind = SlotKind.Skill;
        }

        public void SetupAccessory(InventoryUI ui, PlayerInventory inventory)
        {
            _ui = ui;
            _inventory = inventory;
            _kind = SlotKind.Accessory;
        }

        public void Refresh(InventorySlot slot, ItemDatabaseSO db)
        {
            _currentSlot = slot;

            if (slot.IsEmpty || db == null)
            {
                if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
                return;
            }

            var item = db.GetItem(slot.ItemIndex);
            if (item != null && iconImage != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = item.icon != null;
            }
        }

        // Right-click = unequip
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_inventory == null || _currentSlot.IsEmpty) return;
            if (eventData.button != PointerEventData.InputButton.Right) return;

            switch (_kind)
            {
                case SlotKind.Armor:
                    _inventory.RpcRequestUnequipArmor((int)_armorSlot);
                    break;
                case SlotKind.Skill:
                    _inventory.RpcRequestUnequipSkill();
                    break;
                case SlotKind.Accessory:
                    _inventory.RpcRequestUnequipAccessory();
                    break;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (highlightBorder != null) highlightBorder.enabled = true;
            if (_ui != null && !_currentSlot.IsEmpty) _ui.ShowDetail(_currentSlot);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlightBorder != null) highlightBorder.enabled = false;
            if (_ui != null) _ui.HideDetail();
        }
    }
}
