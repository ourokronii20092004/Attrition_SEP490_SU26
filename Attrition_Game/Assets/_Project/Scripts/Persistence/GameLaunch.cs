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

        public const string DefaultGameplayScene = "The Darkest Path - Map 1";

        /// <summary>Tên scene gameplay hiện tại hoặc sẽ load.</summary>
        public static string GameplayScene = DefaultGameplayScene;

        /// <summary>UserId (OwnerId) từ đăng nhập. Rỗng = chưa login → chỉ lưu local.</summary>
        public static string OwnerId = "";
        /// <summary>Avatar URL từ login (web gửi trong UserDto.avatarUrl). Rỗng = chưa có.</summary>
        public static string AvatarUrl = "";
        /// <summary>CharacterId trên server (nếu đã có). Rỗng = server tự resolve theo (owner, name).</summary>
        public static string CharacterId = "";
        /// <summary>Tên nhân vật đang chơi (dùng cho snapshot online + hiển thị). Coop: tên CHARACTER server.</summary>
        public static string CharacterName = "";
        /// <summary>Level nhân vật đang chọn (hiển thị thẻ lobby coop). 0 = chưa biết → mặc định 1.</summary>
        public static int CharacterLevel = 1;
        /// <summary>Mã phòng coop hiện tại (đính kèm snapshot online).</summary>
        public static string RoomCode = "";
        /// <summary>
        /// SessionId (room bền) trên server — host tạo/reopen room qua API trả về. Dùng làm khóa
        /// lưu/đọc tiến trình per-room (character_session, world_state). Rỗng = chưa gắn room server.
        /// </summary>
        public static string SessionId = "";
        /// <summary>Tên phòng do host đặt (hiển thị trong lobby). Client đọc từ LobbyPlayer của host.</summary>
        public static string RoomName = "";

        /// <summary>
        /// Quest world-state JSON host fetch từ server khi vào room (host-authoritative).
        /// NPC online đọc holder này để khôi phục (đối xứng với solo đọc save slot), tránh đua timing
        /// giữa Spawned() của NPC và lúc fetch xong. Rỗng = chưa có tiến trình quest đã lưu.
        /// </summary>
        public static string CoopQuestsJson = "";

        /// <summary>True nếu đang chơi online (coop, đã đăng nhập) → lưu server. Ngược lại lưu local.</summary>
        public static bool IsOnline => Mode == LaunchMode.Coop && !string.IsNullOrEmpty(OwnerId);

        /// <summary>
        /// Cache inventory theo SESSION (host fetch 1 lần khi vào game): characterId → inventoryJson của
        /// ĐÚNG cặp (character, session hiện tại). Đồ gắn theo hành trình/room, KHÔNG phải toàn cục theo
        /// character. Rỗng/không có key = character này chưa có tiến trình trong session → seed tân thủ.
        /// Quest world-state của session cache riêng ở CoopQuestsJson. Reset khi rời/đổi session.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> SessionInventoryByChar
            = new System.Collections.Generic.Dictionary<string, string>();

        /// <summary>
        /// Vị trí rest đã lưu của mỗi nhân vật trong SESSION (host fetch khi vào game): characterId →
        /// (posX, posY, scene). NetworkSpawner đọc để spawn coop ĐÚNG checkpoint đã lưu thay vì điểm
        /// gốc. Không có key / scene khác = chưa rest trong scene này → dùng spawnPoint mặc định.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<string, (float x, float y, string scene)> SessionRestPosByChar
            = new System.Collections.Generic.Dictionary<string, (float, float, string)>();

        /// <summary>
        /// Stat đã lưu của mỗi nhân vật trong SESSION (host fetch khi vào game): characterId → DTO
        /// session đầy đủ (level, exp, điểm cộng, HP/Mana, số bình máu/mana...). PlayerStats đọc để
        /// hydrate stat cop khi spawn (đối xứng với solo đọc save slot). Không có key = char mới → seed
        /// mặc định. Reset khi rời/đổi session.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<string, APIManager.CharacterSessionDto> SessionStatsByChar
            = new System.Collections.Generic.Dictionary<string, APIManager.CharacterSessionDto>();

        /// <summary>True khi MỘT PlayerInventory đã bắt đầu fetch session (chặn fetch trùng).</summary>
        public static bool SessionInventoryFetchStarted = false;

        /// <summary>True khi fetch session đã XONG (các player khác chờ cờ này rồi mới đọc cache).</summary>
        public static bool SessionInventoryLoaded = false;

        /// <summary>
        /// Playtime phòng đã lưu trên server (detail.playTimeSeconds, lấy khi fetch session). Coop dùng
        /// làm baseline để playtime CỘNG DỒN khi vào lại phòng thay vì reset về 0 (đối xứng với solo
        /// đọc save slot trong PlayerStats.ApplyLoadedProgress). 0 = phòng mới/chưa có tiến trình.
        /// </summary>
        public static int SessionPlaytimeSeconds = 0;

        /// <summary>
        /// True khi fetch session THẤT BẠI (mất mạng/lỗi server) → chặn lưu online: lưu lúc này sẽ ghi
        /// đè tiến trình thật bằng trạng thái rỗng/tân thủ. Xem GameSaveService + PlayerInventory.
        /// </summary>
        public static bool SessionLoadFailed = false;

        /// <summary>Xoá cache inventory-theo-session (gọi khi rời room / đổi session / reset).</summary>
        public static void ClearSessionInventoryCache()
        {
            SessionInventoryByChar.Clear();
            SessionRestPosByChar.Clear();
            SessionStatsByChar.Clear();
            SessionInventoryFetchStarted = false;
            SessionInventoryLoaded = false;
            SessionLoadFailed = false;
            SessionPlaytimeSeconds = 0;
            CoopQuestsJson = "";
        }
    }
}
