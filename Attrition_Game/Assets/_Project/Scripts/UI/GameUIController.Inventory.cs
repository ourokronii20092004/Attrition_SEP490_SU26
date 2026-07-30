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

        /// <summary>
        /// Đang xem tab NHIỆM VỤ (log quest) thay vì lưới đồ?
        ///
        /// VÌ SAO KHÔNG THÊM VÀO `ItemCategory`: enum đó là DATA của item (Equipment/Accessory/Skill/
        /// Material) — mọi ItemSO đều khai theo nó. Thêm "Quest" vào sẽ tạo một category item không tồn tại,
        /// và `IsInTab` phải xử lý nhánh vô nghĩa. Tab nhiệm vụ chỉ là chuyện của UI nên giữ cờ riêng.
        /// </summary>
        private bool _questTabActive;

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
            BindQuestTab("inv-tab-quest");

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

            SetupInventoryDetailDismiss();

            // Đăng ký kéo thả cho các ô trang bị
            if (_root.Q<Button>("equip-head") is Button headBtn) RegisterDragCallbacks(headBtn, SelectedSlotContext.EquippedHead);
            if (_root.Q<Button>("equip-chest") is Button chestBtn) RegisterDragCallbacks(chestBtn, SelectedSlotContext.EquippedChest);
            if (_root.Q<Button>("equip-legs") is Button legsBtn) RegisterDragCallbacks(legsBtn, SelectedSlotContext.EquippedLegs);
            if (_root.Q<Button>("equip-boots") is Button bootsBtn) RegisterDragCallbacks(bootsBtn, SelectedSlotContext.EquippedBoots);
            if (_root.Q<Button>("equip-accessory") is Button accBtn) RegisterDragCallbacks(accBtn, SelectedSlotContext.EquippedAccessory);
            if (_root.Q<Button>("equip-skill") is Button skillBtn) RegisterDragCallbacks(skillBtn, SelectedSlotContext.EquippedSkill);
        }



        /// <summary>Bảng detail (EQUIP/DROP) đang mở?</summary>
        private bool IsDetailOpen
        {
            get
            {
                var d = _root?.Q<VisualElement>("inv-detail");
                return d != null && !d.ClassListContains("hidden");
            }
        }

        /// <summary>Đóng bảng detail + xoá lựa chọn (dùng cho click ra ngoài, ESC, đổi tab, sau equip/drop).</summary>
        private void CloseInventoryDetail()
        {
            SetVisible(_root?.Q<VisualElement>("inv-detail"), false);
            _selectedSlot = -1;
            _selectedContext = SelectedSlotContext.None;
            var grid = _root?.Q<VisualElement>("inv-grid");
            if (grid != null)
                for (int k = 0; k < 40; k++) grid.Q<VisualElement>($"cell-{k}")?.RemoveFromClassList("selected");
        }

        /// <summary>
        /// Bấm ra ngoài bảng detail → đóng bảng. Bắt ở lớp NGOÀI CÙNG (inv-screen) trong pha bubble-up:
        /// ô đồ / ô trang bị / chính bảng detail đã StopPropagation hoặc được loại bằng kiểm tra
        /// "click có nằm trong bảng detail không", nên chỉ những click vào chỗ trống mới đóng.
        /// </summary>
        private void SetupInventoryDetailDismiss()
        {
            var screen = _root?.Q<VisualElement>("inv-screen");
            if (screen == null || _detailDismissBound) return;
            _detailDismissBound = true;

            screen.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!IsDetailOpen) return;

                // Click vào chính bảng detail (kể cả 2 nút) thì không đóng.
                var detail = _root.Q<VisualElement>("inv-detail");
                if (detail != null && detail.worldBound.Contains((Vector2)evt.position)) return;

                // Click vào 1 ô đồ / ô trang bị: các ô tự mở lại detail cho item mới (hoặc đóng nếu ô
                // trống) trong PointerUp — đóng ở đây sẽ nhấp nháy. Bỏ qua để chúng tự xử lý.
                if (IsOverAnySlot((Vector2)evt.position)) return;

                CloseInventoryDetail();
            });
        }

        private bool _detailDismissBound;

        /// <summary>Toạ độ có nằm trên 1 ô đồ trong grid hoặc 1 ô trang bị không?</summary>
        private bool IsOverAnySlot(Vector2 pos)
        {
            var grid = _root.Q<VisualElement>("inv-grid");
            if (grid != null)
                for (int i = 0; i < 40; i++)
                {
                    var cell = grid.Q<VisualElement>($"cell-{i}");
                    if (cell != null && cell.worldBound.Contains(pos)) return true;
                }

            foreach (var n in EquipSlotNames)
                if (_root.Q<Button>(n) is Button b && b.worldBound.Contains(pos)) return true;

            return false;
        }

        private static readonly string[] EquipSlotNames =
            { "equip-head", "equip-chest", "equip-legs", "equip-boots", "equip-accessory", "equip-skill" };

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

        private void BindQuestTab(string name)
        {
            var b = _root.Q<Button>(name);
            if (b != null) b.clicked += SwitchToQuestTab;
        }

        private void SwitchTab(ItemCategory cat)
        {
            _activeTab = cat;
            _questTabActive = false;
            _selectedSlot = -1;
            ApplyTabVisuals();
            SetVisible(_root.Q<VisualElement>("inv-detail"), false);
            RefreshInventory();
        }

        /// <summary>Chuyển sang tab NHIỆM VỤ: ẩn lưới đồ, hiện log quest.</summary>
        private void SwitchToQuestTab()
        {
            _questTabActive = true;
            _selectedSlot = -1;
            ApplyTabVisuals();
            SetVisible(_root.Q<VisualElement>("inv-detail"), false);
            RefreshQuestLog();
        }

        /// <summary>Bật/tắt nút tab đang chọn + đổi giữa lưới đồ và log nhiệm vụ.</summary>
        private void ApplyTabVisuals()
        {
            SetTabActive("inv-tab-equipment", !_questTabActive && _activeTab == ItemCategory.Equipment);
            SetTabActive("inv-tab-accessory", !_questTabActive && _activeTab == ItemCategory.Accessory);
            SetTabActive("inv-tab-skill", !_questTabActive && _activeTab == ItemCategory.Skill);
            SetTabActive("inv-tab-quest", _questTabActive);

            // Lưới đồ và log nhiệm vụ dùng CHUNG vùng bên phải → luôn chỉ 1 cái hiện.
            SetVisible(_root.Q<VisualElement>("inv-grid-scroll"), !_questTabActive);
            SetVisible(_root.Q<VisualElement>("inv-quest-scroll"), _questTabActive);

            // Thanh "SLOTS x/40" chỉ có nghĩa với lưới đồ.
            SetVisible(_root.Q<VisualElement>("inv-weight-row"), !_questTabActive);
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
