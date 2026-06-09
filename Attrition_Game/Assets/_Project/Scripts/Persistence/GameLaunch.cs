namespace Attrition.Persistence
{
    /// <summary>
    /// Holder tĩnh truyền ý định chơi qua ranh giới load scene (MainMenu → scene gameplay).
    /// KHÔNG phải dữ liệu lưu — chỉ sống trong RAM giữa 2 scene.
    ///
    /// Solo  = chạy cục bộ GameMode.Single (không cần login/mạng).
    /// Coop  = chạy mạng GameMode.Host/Client (cần login, đi qua lobby).
    /// </summary>
    public enum LaunchMode { Solo, Coop }

    public static class GameLaunch
    {
        /// <summary>Chế độ được chọn ở MainMenu. Mặc định Solo để test nhanh.</summary>
        public static LaunchMode Mode = LaunchMode.Solo;

        /// <summary>Save slot người chơi chọn (0..2). Dùng khi load tiến trình.</summary>
        public static int SelectedSlot = 0;

        /// <summary>Tên scene gameplay sẽ load khi bắt đầu (test: Enemy_Axe_Demon).</summary>
        public static string GameplayScene = "Enemy_Axe_Demon";

        // ─── Bối cảnh ONLINE (chỉ dùng khi Mode = Coop / có đăng nhập) ───
        /// <summary>UserId (OwnerId) từ đăng nhập. Rỗng = chưa login → chỉ lưu local.</summary>
        public static string OwnerId = "";
        /// <summary>CharacterId trên server (nếu đã có). Rỗng = server tự resolve theo (owner, name).</summary>
        public static string CharacterId = "";
        /// <summary>Tên nhân vật đang chơi (dùng cho snapshot online + hiển thị).</summary>
        public static string CharacterName = "";
        /// <summary>Mã phòng coop hiện tại (đính kèm snapshot online).</summary>
        public static string RoomCode = "";

        /// <summary>True nếu đang chơi online (coop, đã đăng nhập) → lưu server. Ngược lại lưu local.</summary>
        public static bool IsOnline => Mode == LaunchMode.Coop && !string.IsNullOrEmpty(OwnerId);
    }
}
