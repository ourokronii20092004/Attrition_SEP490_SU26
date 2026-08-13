using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Danh sách PICKUP ĐẶT SẴN TRONG SCENE ĐÃ NHẶT, sống xuyên scene (static) — cùng mô hình với
    /// <see cref="BreakableState"/> và <see cref="BossDefeatState"/>.
    ///
    /// Vì sao cần: `PickupItem.Consumed` là `[Networked]` nên chỉ sống trong PHIÊN. Vật thể lại được
    /// ĐẶT SẴN trong scene (9 bình máu ẩn khắp 5 map) → Fusion load lại scene là spawn lại nguyên vẹn.
    /// Rời map rồi quay lại, hoặc thoát vào lại, là nhặt được LẦN NỮA → farm max HP charge tới trần
    /// (hardMaxHealthCharges = 9). Phần thưởng "tìm được bình giấu" mất hết ý nghĩa.
    ///
    /// - SOLO: nạp từ save slot (LoadFrom) + ghi ngược vào slot (WriteTo) → bền qua các lần chơi.
    /// - COOP: chỉ giữ trong phiên của host — cùng quyết định với BreakableState/BossDefeatState.
    ///
    /// Khoá = "scene@x,y" (vị trí làm tròn), CÙNG quy ước với BreakableState và vì cùng lý do: pickup
    /// đặt sẵn không di chuyển nên vị trí ổn định, còn tên GameObject thì tool sinh theo index nên
    /// thêm/bớt một cái là lệch hết khoá cũ.
    ///
    /// CHỈ áp cho pickup ĐẶT SẴN. Đồ quái rơi ra (`DroppedItem`) spawn lúc chạy, KHÔNG đi qua đây —
    /// nếu ghi nhớ theo vị trí thì món rơi trúng chỗ cũ sẽ biến mất oan.
    /// </summary>
    public static class PickupState
    {
        private static readonly HashSet<string> _collected = new HashSet<string>();

        private static string Key(string scene, Vector3 pos)
            => $"{scene}@{Mathf.RoundToInt(pos.x)},{Mathf.RoundToInt(pos.y)}";

        public static bool IsCollected(string scene, Vector3 pos)
            => !string.IsNullOrEmpty(scene) && _collected.Contains(Key(scene, pos));

        /// <summary>Đánh dấu đã nhặt. Trả true nếu MỚI (để caller biết có cần lưu).</summary>
        public static bool MarkCollected(string scene, Vector3 pos)
        {
            if (string.IsNullOrEmpty(scene)) return false;
            return _collected.Add(Key(scene, pos));
        }

        public static void LoadFrom(Attrition.Persistence.SaveSlotData data)
        {
            _collected.Clear();
            if (data?.collectedPickups == null) return;
            foreach (var k in data.collectedPickups)
                if (!string.IsNullOrEmpty(k)) _collected.Add(k);
        }

        // Slot đã nạp (-1 = chưa nạp). Nạp LAZY đúng 1 lần cho mỗi slot.
        private static int _loadedSlot = -1;

        /// <summary>
        /// Đảm bảo đã nạp danh sách từ save slot hiện tại (solo). Gọi từ `PickupItem.Spawned` vì thứ tự
        /// chạy không đảm bảo. Nạp lại khi đổi slot (chọn save khác / new game).
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
            data.collectedPickups = new List<string>(_collected);
        }

        /// <summary>
        /// Xoá sạch (xoá save / bắt đầu game mới). Reset cả cờ đã-nạp — nếu không, xoá slot rồi tạo game
        /// mới CÙNG slot trong cùng phiên sẽ giữ danh sách cũ (bình đã nhặt coi như mất dù là game mới).
        /// </summary>
        public static void Clear()
        {
            _collected.Clear();
            _loadedSlot = -1;
        }
    }
}
