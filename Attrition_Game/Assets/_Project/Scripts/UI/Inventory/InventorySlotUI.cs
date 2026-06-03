using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI.Inventory
{
    /// <summary>
    /// Component gắn trên mỗi ô UI trong Grid.
    /// Hiển thị icon + số lượng. Right-click = equip. Drag &amp; Drop = swap (BR-17 block trong boss zone).
    /// </summary>
    public class InventorySlotUI : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Image highlightBorder;

        private InventoryUI _ui;
        private PlayerInventory _inventory;
        private ItemCategory _category;
        private int _slotIndex;
        private InventorySlot _currentSlot;

        // Drag state
        private static InventorySlotUI _dragSource;
        private Transform _originalParent;
        private CanvasGroup _canvasGroup;

        public void Setup(InventoryUI ui, PlayerInventory inventory, ItemCategory cat, int index)
        {
            _ui = ui;
            _inventory = inventory;
            _category = cat;
            _slotIndex = index;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Refresh(InventorySlot slot, ItemDatabaseSO db)
        {
            _currentSlot = slot;

            if (slot.IsEmpty || db == null)
            {
                if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
                if (amountText != null) amountText.text = "";
                return;
            }

            var item = db.GetItem(slot.ItemIndex);
            if (item == null)
            {
                if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
                if (amountText != null) amountText.text = "";
                return;
            }

            if (iconImage != null) { iconImage.sprite = item.icon; iconImage.enabled = item.icon != null; }
            if (amountText != null) amountText.text = slot.Amount > 1 ? slot.Amount.ToString() : "";
        }

        // ─── RIGHT-CLICK = EQUIP ───

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_inventory == null || _currentSlot.IsEmpty) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Client gửi RPC lên Host
                switch (_category)
                {
                    case ItemCategory.Equipment:
                    {
                        var db = ItemDatabaseSO.Instance;
                        if (db != null)
                        {
                            var item = db.GetItem(_currentSlot.ItemIndex);
                            if (item is SkillSO)
                                _inventory.RpcRequestEquipSkill(_slotIndex);
                            else
                                _inventory.RpcRequestEquip(_slotIndex);
                        }
                        break;
                    }
                    case ItemCategory.Accessory:
                        _inventory.RpcRequestEquipAccessory(_slotIndex);
                        break;
                    case ItemCategory.Skill:
                        _inventory.RpcRequestEquipSkill(_slotIndex);
                        break;
                }
            }
        }

        // ─── HOVER = SHOW DETAIL ───

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

        // ─── DRAG & DROP = SWAP ───

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_currentSlot.IsEmpty) return;
            _dragSource = this;
            _originalParent = transform.parent;
            if (_canvasGroup != null) { _canvasGroup.alpha = 0.6f; _canvasGroup.blocksRaycasts = false; }
            transform.SetParent(transform.root); // move to top
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_canvasGroup != null) { _canvasGroup.alpha = 1f; _canvasGroup.blocksRaycasts = true; }
            transform.SetParent(_originalParent);
            transform.SetSiblingIndex(_slotIndex);

            // Tìm slot drop target
            if (eventData.pointerCurrentRaycast.gameObject != null)
            {
                var target = eventData.pointerCurrentRaycast.gameObject.GetComponent<InventorySlotUI>();
                if (target != null && target != this && target._category == _category)
                {
                    _inventory?.RpcRequestSwap((int)_category, _slotIndex, target._slotIndex);
                }
            }

            _dragSource = null;
        }
    }
}
