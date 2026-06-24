using System;

namespace Attrition.Controllers
{
    /// <summary>
    /// Cầu nối kết quả lưu online → UI (Gameplay không ref UI). GameSaveService phát sự kiện sau
    /// mỗi lần lưu lên server; GameUIController (assembly UI) lắng nghe để hiện toast cho người chơi.
    /// Dùng tiếng Anh cho message vì là chuỗi hiển thị thẳng lên HUD.
    /// </summary>
    public static class SaveNotifyEvents
    {
        /// <summary>(message) — lưu lên server thành công.</summary>
        public static event Action<string> OnSaveOk;
        /// <summary>(message) — lưu thất bại (cần báo người chơi: mất kết nối / phiên hết hạn).</summary>
        public static event Action<string> OnSaveFailed;
        /// <summary>(message) — phiên đăng nhập hết hạn: UI báo rồi tự đưa người chơi về main menu để login lại.</summary>
        public static event Action<string> OnSessionExpired;

        public static void RaiseOk(string message) => OnSaveOk?.Invoke(message);
        public static void RaiseFailed(string message) => OnSaveFailed?.Invoke(message);
        public static void RaiseSessionExpired(string message) => OnSessionExpired?.Invoke(message);
    }
}
