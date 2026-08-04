using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Attrition.UI
{
    /// <summary>
    /// Điều khiển bảng REST (checkpoint) BẰNG BÀN PHÍM — không cần chuột.
    ///   W/S (hoặc ↑/↓) : di chuyển lựa chọn
    ///   A/D (hoặc ←/→) : đổi giá trị (chuyển bình, đổi map ở Fast Travel)
    ///   ENTER          : xác nhận
    ///   ESC            : bảng con → về menu Rest chính; ở menu chính → đóng, về game
    ///
    /// VÌ SAO TỰ QUẢN CON TRỎ thay vì dùng focus có sẵn của UI Toolkit: focus mặc định chỉ đi bằng
    /// Tab/mũi tên và không nhận WASD, lại dễ nhảy sang element ngoài menu đang mở (cả 4 menu đều nằm
    /// chung trong `ft-screen`, menu ẩn bằng class `hidden` vẫn còn trong cây). Tự giữ danh sách dòng
    /// thì luôn biết chính xác đang ở menu nào.
    ///
    /// Con trỏ bàn phím dùng class `kb-selected`, KHÁC với `selected` của chuột ở `ft-row` — hai thứ khác
    /// nhau: `kb-selected` = đang trỏ tới, `selected` = beacon đã chọn để teleport.
    /// </summary>
    public partial class GameUIController
    {
        /// <summary>Một dòng điều hướng: cái để tô sáng + hành động Enter/A/D.</summary>
        private sealed class NavRow
        {
            public VisualElement Highlight;
            public System.Action Activate;
            public System.Action Left;
            public System.Action Right;
        }

        private enum BonfireMenu { Main, LevelUp, Flasks, Travel }
        private BonfireMenu _bonfireMenu = BonfireMenu.Main;

        private readonly List<NavRow> _navRows = new();
        private int _navIndex;
        private const string NavClass = "kb-selected";

        private Label _bonfireHint;

        /// <summary>Bảng Rest đang mở bảng con? (ESC lúc đó = về menu chính, không đóng cả bảng)</summary>
        private bool IsBonfireSubmenuOpen => _bonfireMenu != BonfireMenu.Main;

        //  ─── XÂY DANH SÁCH DÒNG CHO TỪNG MENU ───

        private void BuildMainNav()
        {
            ClearNav();
            AddNav("ft-btn-rest", RestHere);
            AddNav("ft-btn-levelup", OpenLevelUpMenu);
            AddNav("ft-btn-flasks", OpenFlasksMenu);
            AddNav("ft-btn-travel", OpenTravelMenu);
            AddNav("ft-btn-leave", () => ShowOverlay(Overlay.None));
            ApplyNavHighlight();
        }

        private void BuildLevelUpNav()
        {
            ClearNav();
            // 6 chỉ số: Enter HOẶC D đều cộng 1 điểm (D tự nhiên hơn khi coi mỗi dòng là một stepper).
            AddNav("alloc-MaxHP", () => ProvAllocate(0), null, () => ProvAllocate(0));
            AddNav("alloc-MaxMana", () => ProvAllocate(1), null, () => ProvAllocate(1));
            AddNav("alloc-AD", () => ProvAllocate(2), null, () => ProvAllocate(2));
            AddNav("alloc-AP", () => ProvAllocate(3), null, () => ProvAllocate(3));
            AddNav("alloc-DEF", () => ProvAllocate(4), null, () => ProvAllocate(4));
            AddNav("alloc-RES", () => ProvAllocate(5), null, () => ProvAllocate(5));
            AddNav("ft-levelup-apply", ApplyLevelUp);
            AddNav("ft-levelup-back", ShowBonfireMain);
            ApplyNavHighlight();
        }

        private void BuildFlasksNav()
        {
            ClearNav();
            // Bình là ZERO-SUM (dồn HP thì mất mana) nên mỗi loại là 1 dòng, A/D chuyển qua lại.
            // Tô sáng ô SỐ (flask-*-val) vì hai nút </> của dòng đó không đại diện cho cả dòng.
            AddNav("flask-hp-val", null, () => ChangeFlasks(-1, 1), () => ChangeFlasks(1, -1));
            AddNav("flask-mana-val", null, () => ChangeFlasks(1, -1), () => ChangeFlasks(-1, 1));
            AddNav("ft-flasks-apply", ApplyFlasks);
            AddNav("ft-flasks-back", ShowBonfireMain);
            ApplyNavHighlight();
        }

        /// <summary>
        /// Fast Travel: các dòng beacon (tạo runtime) + TELEPORT + BACK. A/D đổi map ở MỌI dòng — menu này
        /// không có hành động ngang nào khác, mà danh sách beacon lại phụ thuộc map nên gắn vào đâu cũng đúng.
        /// Gọi lại mỗi lần RefreshFastTravelList vì danh sách dòng bị dựng lại.
        /// </summary>
        private void BuildTravelNav()
        {
            ClearNav();

            foreach (var (marker, row) in _ftRows)
            {
                if (row == null) continue;
                var m = marker; var r = row;
                r.focusable = false;   // xem ghi chú ở AddNav — tránh focus của panel đếm lệch con trỏ
                _navRows.Add(new NavRow
                {
                    Highlight = r,
                    // ENTER lần 1 trên beacon = CHỌN. ENTER lần 2 (vẫn đứng ở beacon đó) = ĐI LUÔN.
                    // Nhờ vậy không phải W/S xuống nút TELEPORT nữa — khớp yêu cầu hạn chế thao tác.
                    // Nút TELEPORT vẫn giữ, ai muốn bấm chuột thì vẫn dùng được.
                    //
                    // Gọi thẳng SelectFtRow chứ không giả lập click (xem ghi chú ở RefreshFastTravelList).
                    Activate = () =>
                    {
                        bool alreadyPicked = _ftSelected.HasValue
                                             && _ftSelected.Value.checkpointId == m.checkpointId;
                        if (alreadyPicked) TeleportToSelected();
                        else SelectFtRow(m, r);
                    },
                    Left = () => ChangeFastTravelMap(-1),
                    Right = () => ChangeFastTravelMap(1),
                });
            }

            AddNav("ft-go", TeleportToSelected, () => ChangeFastTravelMap(-1), () => ChangeFastTravelMap(1));
            AddNav("ft-travel-back", ShowBonfireMain, () => ChangeFastTravelMap(-1), () => ChangeFastTravelMap(1));
            ApplyNavHighlight();
        }

        //  ─── HẠ TẦNG ───

        private void ClearNav()
        {
            foreach (var r in _navRows) r.Highlight?.RemoveFromClassList(NavClass);
            _navRows.Clear();
            _navIndex = 0;
        }

        private void AddNav(string elementName, System.Action activate,
                            System.Action left = null, System.Action right = null)
        {
            var el = _root?.Q<VisualElement>(elementName);
            if (el == null) return;

            // TẮT focus của UI Toolkit trên element này.
            //
            // VÌ SAO: Button trong UI Toolkit mặc định focusable, và panel TỰ điều hướng focus bằng
            // W/S/mũi tên rồi kích hoạt nút đang focus khi nhấn ENTER. Nó chạy SONG SONG với con trỏ
            // `_navIndex` của mình → hai hệ thống đếm lệch nhau: mình sáng ở "LEVEL UP" nhưng focus của
            // panel vẫn ở "REST", ENTER kích hoạt cái đang focus → bấm LEVEL UP lại ra REST.
            // Đúng lỗi user báo "bị lệch một dòng".
            //
            // Bỏ focus đi thì chỉ còn MỘT nguồn sự thật là `_navIndex`. Chuột vẫn bấm được bình thường
            // (click không cần focus).
            el.focusable = false;

            _navRows.Add(new NavRow { Highlight = el, Activate = activate, Left = left, Right = right });
        }

        private void ApplyNavHighlight()
        {
            for (int i = 0; i < _navRows.Count; i++)
            {
                var el = _navRows[i].Highlight;
                if (el == null) continue;
                if (i == _navIndex) el.AddToClassList(NavClass);
                else el.RemoveFromClassList(NavClass);
            }
        }

        /// <summary>Dòng dùng được? Nút bị disable (vd TELEPORT khi chưa chọn beacon) thì bỏ qua.</summary>
        private bool IsNavUsable(int i)
        {
            if (i < 0 || i >= _navRows.Count) return false;
            var el = _navRows[i].Highlight;
            return el != null && el.enabledInHierarchy;
        }

        private void MoveNav(int step)
        {
            if (_navRows.Count == 0) return;

            // Bọc vòng, bỏ qua dòng disable. Giới hạn số lần thử = số dòng để không lặp vô hạn khi
            // TOÀN BỘ dòng đều disable (vd Fast Travel chưa khám phá beacon nào).
            int idx = _navIndex;
            for (int tries = 0; tries < _navRows.Count; tries++)
            {
                idx = (idx + step + _navRows.Count) % _navRows.Count;
                if (!IsNavUsable(idx)) continue;
                _navIndex = idx;
                ApplyNavHighlight();
                return;
            }
        }

        /// <summary>
        /// Bàn phím cho bảng Rest. Gọi từ GameUIController.Update khi overlay == FastTravel.
        /// Trả true nếu ĐÃ xử lý ESC (để Update không mở Pause cùng frame).
        /// </summary>
        private bool HandleBonfireKeys()
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) MoveNav(-1);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) MoveNav(1);

            bool hasRow = _navIndex >= 0 && _navIndex < _navRows.Count;

            if (hasRow && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)))
                _navRows[_navIndex].Left?.Invoke();

            if (hasRow && (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)))
                _navRows[_navIndex].Right?.Invoke();

            if (hasRow && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                && IsNavUsable(_navIndex))
                _navRows[_navIndex].Activate?.Invoke();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsBonfireSubmenuOpen) ShowBonfireMain();   // bảng con → menu chính
                else ShowOverlay(Overlay.None);                // menu chính → về game
                return true;
            }

            return false;
        }

        /// <summary>Dòng nhắc phím dưới bảng Rest — tạo bằng code nên không phải sửa GameUI.uxml.</summary>
        private void EnsureBonfireHint()
        {
            if (_bonfireHint != null || _ftScreen == null) return;

            _bonfireHint = new Label("W/S SELECT   ·   A/D ADJUST   ·   ENTER CONFIRM   ·   ESC BACK")
            {
                name = "ft-key-hint",
                pickingMode = PickingMode.Ignore,
            };
            var s = _bonfireHint.style;
            s.position = Position.Absolute;
            s.bottom = 24; s.left = 0; s.right = 0;
            s.fontSize = 18;                     // cỡ chữ inline: USS không đè được, xem ghi chú ở Toast
            s.color = new Color(0.70f, 0.66f, 0.55f);
            s.unityTextAlign = TextAnchor.MiddleCenter;
            _ftScreen.Add(_bonfireHint);
        }
    }
}
