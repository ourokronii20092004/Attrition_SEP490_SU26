using System.Collections.Generic;

namespace Attrition.Persistence
{
    /// <summary>
    /// Trạng thái CUTSCENE toàn cục — chia sẻ giữa Gameplay ↔ UI mà không tạo vòng tham chiếu
    /// assembly (cùng kiểu với <see cref="DialogueState"/>).
    ///
    /// CutscenePlayer ghi khi bắt đầu/kết thúc; GameUIController đọc để KHÔNG cho mở Pause/Inventory
    /// giữa cảnh; PlayerController khoá di chuyển qua DialogueState (đã có sẵn).
    ///
    /// KHÔNG dùng GamePause ở đây: coop không được phép dừng mô phỏng Fusion (phá đồng bộ). Cutscene
    /// chỉ khoá input + lia camera nên chạy giống nhau cho cả solo và coop.
    ///
    /// Ngoài ra giữ tập cutsceneId ĐÃ XEM (cho playOnce), nạp/ghi cùng file save như fog bản đồ.
    /// </summary>
    public static class CutsceneState
    {
        /// <summary>True khi một cutscene đang chiếu trên máy này.</summary>
        public static bool IsPlaying { get; set; }

        private static readonly HashSet<string> _seen = new HashSet<string>();

        public static bool HasSeen(string cutsceneId) =>
            !string.IsNullOrEmpty(cutsceneId) && _seen.Contains(cutsceneId);

        /// <summary>Đánh dấu đã xem. Trả về true nếu MỚI (để biết có cần save).</summary>
        public static bool MarkSeen(string cutsceneId)
        {
            if (string.IsNullOrEmpty(cutsceneId)) return false;
            return _seen.Add(cutsceneId);
        }

        public static void LoadFrom(SaveSlotData data)
        {
            _seen.Clear();
            _loaded = true;
            if (data?.seenCutscenes == null) return;
            foreach (var id in data.seenCutscenes)
                if (!string.IsNullOrEmpty(id)) _seen.Add(id);
        }

        private static bool _loaded;

        /// <summary>
        /// Nạp danh sách đã xem từ slot save — CHỈ MỘT LẦN mỗi lượt chơi (solo).
        /// Bắt buộc phải một lần: nhiều CutscenePlayer trong scene, và mỗi lần đổi map lại spawn mới.
        /// Nếu nạp lại sau khi đã MarkSeen (mà chưa kịp save), cờ vừa đánh dấu sẽ bị xoá → cutscene
        /// vừa xem chiếu lại.
        /// </summary>
        public static void EnsureLoadedFromSave()
        {
            if (_loaded) return;
            _loaded = true;
            if (GameLaunch.IsOnline) return; // coop: xem mục ponytail ở CutscenePlayer
            LoadFrom(SaveManager.LoadSlot(GameLaunch.SelectedSlot));
        }

        public static void WriteTo(SaveSlotData data)
        {
            if (data == null) return;
            data.seenCutscenes = new List<string>(_seen);
        }

        /// <summary>Xoá sạch (game mới / về menu). Không đụng save.</summary>
        public static void Clear()
        {
            _seen.Clear();
            _loaded = false; // lượt chơi sau phải nạp lại từ slot của lượt đó
            IsPlaying = false;
        }
    }
}
