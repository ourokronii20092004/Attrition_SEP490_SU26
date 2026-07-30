using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Gameplay.Player.Inventory;
using Attrition.Data;

namespace Attrition.UI
{
    public partial class GameUIController
    {
        private VisualElement _dragGhost;
        private SelectedSlotContext _dragContext = SelectedSlotContext.None;
        private int _dragSlot = -1;
        private bool _isDragging = false;
        private Vector2 _dragStartPos;

        private void SetupDragAndDrop()
        {
            if (_dragGhost == null)
            {
                _dragGhost = new VisualElement { name = "drag-ghost" };
                _dragGhost.style.position = new StyleEnum<Position>(Position.Absolute);
                _dragGhost.style.width = 64;
                _dragGhost.style.height = 64;
                _dragGhost.style.visibility = Visibility.Hidden;
                _root.Add(_dragGhost);
            }
        }

        private void RegisterDragCallbacks(VisualElement element, SelectedSlotContext context, int slotIndex = -1)
        {
            element.RegisterCallback<PointerDownEvent>(evt => OnPointerDown(evt, context, slotIndex, element), TrickleDown.TrickleDown);
            element.RegisterCallback<PointerMoveEvent>(evt => OnPointerMove(evt, element), TrickleDown.TrickleDown);
            element.RegisterCallback<PointerUpEvent>(evt => OnPointerUp(evt, element), TrickleDown.TrickleDown);
            
            // ContextClickEvent là sự kiện chuẩn cho Chuột Phải trong UI Toolkit (giúp bypass việc Editor nuốt click)
            element.RegisterCallback<ContextClickEvent>(evt => {
                if (_inventory == null || _db == null) return;
                if (context == SelectedSlotContext.InventoryGrid)
                    OnCellClicked(slotIndex);
                else if (context != SelectedSlotContext.None)
                    OnEquipSlotClicked(context);
                evt.StopPropagation();
                evt.PreventDefault();
            }, TrickleDown.TrickleDown);
        }

        private void OnPointerDown(PointerDownEvent evt, SelectedSlotContext context, int slotIndex, VisualElement element)
        {
            if (_inventory == null || _db == null) return;

            // Xử lý Click Chuột Phải -> Mở bảng tuỳ chọn
            if (evt.button == 1)
            {
                if (context == SelectedSlotContext.InventoryGrid)
                    OnCellClicked(slotIndex);
                else if (context != SelectedSlotContext.None)
                    OnEquipSlotClicked(context);
                
                evt.StopPropagation(); // Chặn các sự kiện chuột phải khác
                return;
            }

            // Chỉ xử lý kéo thả khi Click Chuột Trái
            if (evt.button != 0) return;

            InventorySlot slotInfo = InventorySlot.Empty;
            if (context == SelectedSlotContext.InventoryGrid && slotIndex >= 0)
                slotInfo = GetSlot(slotIndex);
            else
            {
                switch (context)
                {
                    case SelectedSlotContext.EquippedHead: slotInfo = _inventory.EquippedHead; break;
                    case SelectedSlotContext.EquippedChest: slotInfo = _inventory.EquippedChest; break;
                    case SelectedSlotContext.EquippedLegs: slotInfo = _inventory.EquippedLegs; break;
                    case SelectedSlotContext.EquippedBoots: slotInfo = _inventory.EquippedBoots; break;
                    case SelectedSlotContext.EquippedAccessory: slotInfo = _inventory.EquippedAccessory; break;
                    case SelectedSlotContext.EquippedSkill: slotInfo = _inventory.EquippedSkill; break;
                }
            }

            _dragContext = context;
            _dragSlot = slotIndex;
            _isDragging = false;
            _dragStartPos = evt.position;

            if (!slotInfo.IsEmpty)
            {
                var item = _db.GetItem(slotInfo.ItemIndex);
                if (item != null && item.icon != null)
                {
                    _dragGhost.style.backgroundImage = new StyleBackground(item.icon);
                }
            }

            element.CapturePointer(evt.pointerId);
            // LƯU Ý: Không gọi evt.StopPropagation() ở đây để ClickEvent vẫn có thể chạy!
        }

        private void OnPointerMove(PointerMoveEvent evt, VisualElement element)
        {
            if (!element.HasPointerCapture(evt.pointerId)) return;

            if (!_isDragging && Vector2.Distance(_dragStartPos, evt.position) > 5f)
            {
                InventorySlot slotInfo = InventorySlot.Empty;
                if (_dragContext == SelectedSlotContext.InventoryGrid && _dragSlot >= 0)
                    slotInfo = GetSlot(_dragSlot);
                else
                {
                    switch (_dragContext)
                    {
                        case SelectedSlotContext.EquippedHead: slotInfo = _inventory.EquippedHead; break;
                        case SelectedSlotContext.EquippedChest: slotInfo = _inventory.EquippedChest; break;
                        case SelectedSlotContext.EquippedLegs: slotInfo = _inventory.EquippedLegs; break;
                        case SelectedSlotContext.EquippedBoots: slotInfo = _inventory.EquippedBoots; break;
                        case SelectedSlotContext.EquippedAccessory: slotInfo = _inventory.EquippedAccessory; break;
                        case SelectedSlotContext.EquippedSkill: slotInfo = _inventory.EquippedSkill; break;
                    }
                }

                if (!slotInfo.IsEmpty)
                {
                    _isDragging = true;
                    _dragGhost.style.visibility = Visibility.Visible;
                    _dragGhost.BringToFront();
                }
            }

            if (_isDragging)
            {
                _dragGhost.style.left = evt.position.x - 32f;
                _dragGhost.style.top = evt.position.y - 32f;
            }
        }

        private void OnPointerUp(PointerUpEvent evt, VisualElement element)
        {
            if (!element.HasPointerCapture(evt.pointerId)) return;
            
            element.ReleasePointer(evt.pointerId);

            if (_isDragging)
            {
                _dragGhost.style.visibility = Visibility.Hidden;
                ProcessDrop(evt.position);
            }
            else
            {
                // Nếu click thả ra tại chỗ (chuột trái) -> tính là click mở panel!
                if (_dragContext == SelectedSlotContext.InventoryGrid)
                    OnCellClicked(_dragSlot);
                else if (_dragContext != SelectedSlotContext.None)
                    OnEquipSlotClicked(_dragContext);
            }

            _isDragging = false;
            _dragContext = SelectedSlotContext.None;
            _dragSlot = -1;
        }

        private void ProcessDrop(Vector2 screenPos)
        {
            if (_inventory == null) return;

            // 1. Kéo vào Grid (Tháo trang bị ra ô cụ thể, hoặc Swap đồ)
            var grid = _root.Q<VisualElement>("inv-grid");
            if (grid != null)
            {
                for (int i = 0; i < 40; i++)
                {
                    var cell = grid.Q<VisualElement>($"cell-{i}");
                    if (cell != null && cell.worldBound.Contains(screenPos))
                    {
                        if (_dragContext == SelectedSlotContext.InventoryGrid)
                        {
                            if (_dragSlot == i)
                            {
                                // Kéo và thả trong cùng 1 ô -> Tính là Click!
                                OnCellClicked(_dragSlot);
                                return;
                            }
                            else
                            {
                                _inventory.RpcRequestSwap((int)_activeTab, _dragSlot, i);
                                return;
                            }
                        }
                        else
                        {
                            switch (_dragContext)
                            {
                                case SelectedSlotContext.EquippedHead: _inventory.RpcRequestUnequipArmorToSlot((int)EquipmentSlot.Head, i); break;
                                case SelectedSlotContext.EquippedChest: _inventory.RpcRequestUnequipArmorToSlot((int)EquipmentSlot.Chest, i); break;
                                case SelectedSlotContext.EquippedLegs: _inventory.RpcRequestUnequipArmorToSlot((int)EquipmentSlot.Legs, i); break;
                                case SelectedSlotContext.EquippedBoots: _inventory.RpcRequestUnequipArmorToSlot((int)EquipmentSlot.Boots, i); break;
                                // Accessory: chỉ đổi được tại checkpoint (toast giải thích lý do).
                                case SelectedSlotContext.EquippedAccessory:
                                    if (BlockAccessorySwapOutsideCheckpoint()) return;
                                    _inventory.RpcRequestUnequipAccessoryToSlot(i);
                                    break;
                                case SelectedSlotContext.EquippedSkill: _inventory.RpcRequestUnequipSkillToSlot(i); break;
                            }
                        }
                        return;
                    }
                }
            }

            // 2. Kéo vào ô trang bị (Mặc đồ nhanh)
            string[] equipBtnNames = { "equip-head", "equip-chest", "equip-legs", "equip-boots", "equip-accessory", "equip-skill" };
            foreach (var btnName in equipBtnNames)
            {
                var btn = _root.Q<Button>(btnName);
                if (btn != null && btn.worldBound.Contains(screenPos))
                {
                    // Nếu kéo từ trang bị thả vào chính trang bị đó -> Tính là Click!
                    if (_dragContext != SelectedSlotContext.InventoryGrid && _dragContext != SelectedSlotContext.None)
                    {
                        bool isSameSlot = false;
                        if (btnName == "equip-head" && _dragContext == SelectedSlotContext.EquippedHead) isSameSlot = true;
                        if (btnName == "equip-chest" && _dragContext == SelectedSlotContext.EquippedChest) isSameSlot = true;
                        if (btnName == "equip-legs" && _dragContext == SelectedSlotContext.EquippedLegs) isSameSlot = true;
                        if (btnName == "equip-boots" && _dragContext == SelectedSlotContext.EquippedBoots) isSameSlot = true;
                        if (btnName == "equip-accessory" && _dragContext == SelectedSlotContext.EquippedAccessory) isSameSlot = true;
                        if (btnName == "equip-skill" && _dragContext == SelectedSlotContext.EquippedSkill) isSameSlot = true;

                        if (isSameSlot)
                        {
                            OnEquipSlotClicked(_dragContext);
                            return;
                        }
                    }

                    if (_dragContext == SelectedSlotContext.InventoryGrid)
                    {
                        switch (btnName)
                        {
                            // Accessory: chỉ đổi được tại checkpoint (toast giải thích lý do).
                            case "equip-accessory":
                                if (BlockAccessorySwapOutsideCheckpoint()) return;
                                _inventory.RpcRequestEquipAccessory(_dragSlot);
                                break;
                            case "equip-skill": _inventory.RpcRequestEquipSkill(_dragSlot); break;
                            default: _inventory.RpcRequestEquip(_dragSlot); break;
                        }
                    }
                    return;
                }
            }

            // 3. Vứt đồ (Kéo ra ngoài UI)
            var invRight = _root.Q<VisualElement>("inv-right");
            var charLeft = _root.Q<VisualElement>("char-left");
            bool isOutside = true;

            if (invRight != null && invRight.worldBound.Contains(screenPos)) isOutside = false;
            if (charLeft != null && charLeft.worldBound.Contains(screenPos)) isOutside = false;

            if (isOutside)
            {
                if (_dragContext == SelectedSlotContext.InventoryGrid)
                {
                    _inventory.RpcRequestDrop((int)_activeTab, _dragSlot);
                }
                else
                {
                    switch (_dragContext)
                    {
                        case SelectedSlotContext.EquippedHead: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Head); break;
                        case SelectedSlotContext.EquippedChest: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Chest); break;
                        case SelectedSlotContext.EquippedLegs: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Legs); break;
                        case SelectedSlotContext.EquippedBoots: _inventory.RpcRequestDropEquippedArmor((int)EquipmentSlot.Boots); break;
                        case SelectedSlotContext.EquippedAccessory: _inventory.RpcRequestDropEquippedAccessory(); break;
                        case SelectedSlotContext.EquippedSkill: _inventory.RpcRequestDropEquippedSkill(); break;
                    }
                }
            }
        }
    }
}
