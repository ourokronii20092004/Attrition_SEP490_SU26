namespace Attrition.Persistence
{
    /// <summary>
    /// Trạng thái "có overlay UI đang chặn gameplay" — chia sẻ Gameplay ↔ UI mà không tạo vòng tham chiếu
    /// assembly (cùng mô hình <see cref="DialogueState"/>).
    ///
    /// VÌ SAO CẦN: solo thì overlay đóng băng game qua <see cref="GamePause.SetSoloFreeze"/>, nhưng COOP
    /// thì hàm đó no-op (dừng sim sẽ phá đồng bộ) → mở bảng Rest/Inventory ở coop player VẪN đi lại được.
    /// Trước đây không ai nhận ra vì mấy bảng đó chỉ bấm bằng chuột. Từ khi điều khiển bằng WASD/Enter thì
    /// lộ ngay: nhấn S để xuống dòng là nhân vật NGỒI, A/D là chạy.
    ///
    /// GameUIController ghi khi đổi overlay; PlayerController đọc để khoá di chuyển của player local.
    /// </summary>
    public static class UiOverlayState
    {
        /// <summary>True khi có overlay chặn gameplay (Rest/Inventory/Pause/Settings/GameOver/Loading).</summary>
        public static bool IsBlocking { get; set; }
    }
}
