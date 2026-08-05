using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Danh sách BOSS ĐÃ HẠ, sống xuyên scene (static) — cùng mô hình với <see cref="WorldMapState"/>.
    ///
    /// Vì sao cần: `BossGateController.BossDefeated` chỉ là [Networked] nên KHÔNG bền — đi từ Map 2 về
    /// Map 1 (load lại scene) hay out game vào lại thì boss đã chết lại spawn nguyên máu.
    ///
    /// - SOLO: nạp từ save slot (LoadFrom) + ghi ngược vào slot (WriteTo) → bền qua các lần chơi.
    /// - COOP: nạp/ghi qua world-state của phòng trên server (bulk save). Trước 2026-08 coop chỉ giữ
    ///   trong RAM host nên reopen phòng là boss đã hạ sống lại — nay đã bền.
    ///
    /// Khoá (bossId) = `EnemyStats.EnemyId` (vd "severed_fang"). Boss là duy nhất toàn game nên đủ phân biệt.
    /// </summary>
    public static class BossDefeatState
    {
        private static readonly HashSet<string> _defeated = new HashSet<string>();

        public static bool IsDefeated(string bossId)
            => !string.IsNullOrEmpty(bossId) && _defeated.Contains(bossId);

        /// <summary>Đánh dấu boss đã hạ. Trả true nếu MỚI (để caller biết có cần lưu).</summary>
        public static bool MarkDefeated(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return false;
            return _defeated.Add(bossId);
        }

        public static IReadOnlyCollection<string> AllDefeated => _defeated;

        public static void LoadFrom(Attrition.Persistence.SaveSlotData data)
        {
            _defeated.Clear();
            if (data?.defeatedBosses == null) return;
            foreach (var id in data.defeatedBosses)
                if (!string.IsNullOrEmpty(id)) _defeated.Add(id);
        }

        /// <summary>
        /// COOP: nạp từ world-state của phòng (server) thay vì save slot. Trước đây coop chỉ giữ
        /// trong RAM host nên reopen phòng là boss sống lại — giờ bulk save đẩy lên DB nên nạp lại được.
        ///
        /// <paramref name="sessionId"/> quyết định cách hoà dữ liệu:
        ///  - Phòng KHÁC (hoặc lần đầu) → THAY THẾ: không để boss của phòng trước lẫn sang phòng này.
        ///  - CÙNG phòng (fetch lại khi đổi map / người thứ 2 vào muộn) → HỢP NHẤT (union).
        ///
        /// Vì sao phải union trong cùng phòng: RAM đang giữ boss vừa hạ mà server có thể CHƯA biết
        /// (bulk save chạy sau, hoặc lần đó lỗi mạng). Bản cũ luôn Clear() nên mỗi lần đổi map là xoá
        /// sạch boss vừa đánh → quay lại map cũ thấy boss sống nguyên máu. Đúng lỗi user báo.
        /// </summary>
        public static void LoadFromIds(IEnumerable<string> bossIds, string sessionId)
        {
            if (_loadedSessionId != sessionId)
            {
                _defeated.Clear();
                _loadedSessionId = sessionId;
            }

            if (bossIds == null) return;
            foreach (var id in bossIds)
                if (!string.IsNullOrEmpty(id)) _defeated.Add(id);
        }

        // Phòng coop đã nạp (null = chưa nạp phòng nào). Phân biệt "fetch lại cùng phòng" với "sang
        // phòng khác" — xem LoadFromIds.
        private static string _loadedSessionId;

        // Slot đã nạp (-1 = chưa nạp). Dùng để nạp LAZY đúng 1 lần cho mỗi slot.
        private static int _loadedSlot = -1;

        /// <summary>
        /// Đảm bảo đã nạp danh sách từ save slot hiện tại (solo). Gọi từ `BossGateController.Spawned`
        /// vì thứ tự chạy không đảm bảo — Spawned có thể chạy TRƯỚC `FogTracker.Start` (nơi nạp
        /// WorldMapState). Nạp lại khi đổi slot (chọn save khác / new game). Coop: no-op, chỉ giữ
        /// trong phiên host.
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
            data.defeatedBosses = new List<string>(_defeated);
        }

        /// <summary>
        /// Xoá sạch (xoá save / bắt đầu game mới). Không đụng file save.
        /// Reset cả cờ đã-nạp để lần sau nạp lại từ đầu — nếu không, xoá slot rồi tạo game mới CÙNG slot
        /// trong cùng phiên sẽ giữ danh sách boss cũ (boss coi như đã hạ dù là game mới).
        /// </summary>
        public static void Clear()
        {
            _defeated.Clear();
            _loadedSlot = -1;
            _loadedSessionId = null;
        }
    }
}
