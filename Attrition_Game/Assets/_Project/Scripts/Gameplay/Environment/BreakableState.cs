using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Danh sách VẬT PHÁ ĐƯỢC ĐÃ VỠ, sống xuyên scene (static) — cùng mô hình với
    /// <see cref="BossDefeatState"/>.
    ///
    /// Vì sao cần: `BreakableObject` chỉ `Runner.Despawn` nên nó mất trong PHIÊN đó, nhưng vật thể được
    /// ĐẶT SẴN trong scene → Fusion load lại scene là spawn lại nguyên vẹn. Đi sang map khác rồi quay
    /// lại, hoặc thoát game vào lại, thì tường đã phá lại chắn đường như cũ — đường tắt vừa mở mất ý nghĩa.
    ///
    /// - SOLO: nạp từ save slot (LoadFrom) + ghi ngược vào slot (WriteTo) → bền qua các lần chơi.
    /// - COOP: chỉ giữ trong phiên của host (không đụng API/DB) — cùng quyết định với BossDefeatState.
    ///
    /// Khoá = "scene@x,y" (vị trí làm tròn). KHÔNG dùng tên GameObject: tool chuyển tile sinh tên theo
    /// index (`Breakable_0`, `Breakable_1`...), vẽ thêm tile rồi chạy lại là index đổi → khoá cũ trỏ sai
    /// vật thể. Vị trí thì ổn định vì vật thể đặt sẵn, không di chuyển.
    /// </summary>
    public static class BreakableState
    {
        private static readonly HashSet<string> _broken = new HashSet<string>();

        private static string Key(string scene, Vector3 pos)
            => $"{scene}@{Mathf.RoundToInt(pos.x)},{Mathf.RoundToInt(pos.y)}";

        public static bool IsBroken(string scene, Vector3 pos)
            => !string.IsNullOrEmpty(scene) && _broken.Contains(Key(scene, pos));

        /// <summary>Đánh dấu đã vỡ. Trả true nếu MỚI (để caller biết có cần lưu).</summary>
        public static bool MarkBroken(string scene, Vector3 pos)
        {
            if (string.IsNullOrEmpty(scene)) return false;
            return _broken.Add(Key(scene, pos));
        }

        public static void LoadFrom(Attrition.Persistence.SaveSlotData data)
        {
            _broken.Clear();
            if (data?.brokenObjects == null) return;
            foreach (var k in data.brokenObjects)
                if (!string.IsNullOrEmpty(k)) _broken.Add(k);
        }

        // Slot đã nạp (-1 = chưa nạp). Nạp LAZY đúng 1 lần cho mỗi slot.
        private static int _loadedSlot = -1;

        /// <summary>
        /// Đảm bảo đã nạp danh sách từ save slot hiện tại (solo). Gọi từ `BreakableObject.Spawned` vì thứ
        /// tự chạy không đảm bảo. Nạp lại khi đổi slot (chọn save khác / new game).
        /// Coop: no-op, chỉ giữ trong phiên host.
        /// </summary>
        public static void EnsureLoadedForSolo()
        {
            if (Attrition.Persistence.GameLaunch.IsOnline) return;

            int slot = Attrition.Persistence.GameLaunch.SelectedSlot;
            if (_loadedSlot == slot) return;
            _loadedSlot = slot;

            LoadFrom(Attrition.Persistence.SaveManager.LoadSlot(slot));
        }

        public static void WriteTo(Attrition.Persistence.SaveSlotData data)
        {
            if (data == null) return;
            data.brokenObjects = new List<string>(_broken);
        }

        /// <summary>
        /// Xoá sạch (xoá save / bắt đầu game mới). Reset cả cờ đã-nạp — nếu không, xoá slot rồi tạo game
        /// mới CÙNG slot trong cùng phiên sẽ giữ danh sách cũ (tường coi như đã phá dù là game mới).
        /// </summary>
        public static void Clear()
        {
            _broken.Clear();
            _loadedSlot = -1;
        }
    }
}
