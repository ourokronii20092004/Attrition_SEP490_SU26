namespace Attrition.Persistence
{
    /// <summary>
    /// Cờ tạm dừng game cho chế độ SOLO. Fusion physics chạy theo Runner.DeltaTime,
    /// KHÔNG quan tâm Time.timeScale — nên đặt timeScale=0 không dừng được quái/đạn.
    /// Các hệ thống mô phỏng (EnemyController, PlayerController, projectile...) tự đọc cờ này
    /// trong FixedUpdateNetwork và return sớm + đóng băng velocity khi IsPaused.
    ///
    /// COOP không bao giờ set cờ này qua SetSoloFreeze (online — dừng sẽ phá đồng bộ).
    /// Ngoại lệ duy nhất: CoopSession ghi thẳng IsPaused khi host chờ client quay lại.
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused;

        /// <summary>Ai đang yêu cầu dừng. Nhiều UI có thể mở lồng nhau nên phải cộng dồn lý do.</summary>
        [System.Flags]
        public enum Freeze
        {
            None = 0,
            /// <summary>Overlay của GameUIController: Pause/Inventory/FastTravel/Settings/GameOver.</summary>
            Overlay = 1,
            /// <summary>Hội thoại NPC hoặc popup nhận thưởng (DialogueUI).</summary>
            Dialogue = 2,
            /// <summary>Bản đồ tổng (M).</summary>
            WorldMap = 4,
        }

        private static Freeze _active;

        /// <summary>
        /// SOLO: bật/tắt MỘT lý do dừng; game đóng băng khi CÒN ÍT NHẤT một lý do. COOP: no-op.
        ///
        /// VÌ SAO CỘNG DỒN LÝ DO thay vì một bool: các UI chặn gameplay có thể lồng nhau (ESC giữa lúc
        /// đang thoại NPC, mở map rồi bấm Tab...). Với một bool thì người ĐÓNG SAU sẽ gỡ luôn freeze
        /// của người vẫn đang mở → quái đánh lại giữa lúc đang đọc thoại. Cộng dồn thì mỗi UI chỉ cần
        /// khai báo trạng thái CỦA MÌNH, không phải biết gì về các UI khác.
        ///
        /// Gộp cả Time.timeScale (Animator + Update thường) lẫn IsPaused (sim Fusion, vì Fusion bỏ qua
        /// timeScale) vào đây để nơi gọi không phải nhớ cả hai.
        /// </summary>
        public static void SetSoloFreeze(Freeze reason, bool on)
        {
            if (GameLaunch.Mode != LaunchMode.Solo) return;

            if (on) _active |= reason;
            else _active &= ~reason;

            bool freeze = _active != Freeze.None;
            IsPaused = freeze;
            UnityEngine.Time.timeScale = freeze ? 0f : 1f;
        }

        /// <summary>
        /// Xoá sạch mọi lý do (rời trận / đổi scene / về menu). Cờ là static nên sống xuyên scene —
        /// thoát lúc đang mở UI mà không reset thì phiên sau vào game đứng hình.
        /// </summary>
        public static void ResetFreeze()
        {
            _active = Freeze.None;
            IsPaused = false;
            UnityEngine.Time.timeScale = 1f;
        }
    }
}
