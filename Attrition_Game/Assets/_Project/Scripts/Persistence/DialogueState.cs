namespace Attrition.Persistence
{
    /// <summary>
    /// Trạng thái hội thoại toàn cục — chia sẻ giữa Gameplay ↔ UI
    /// mà không tạo vòng tham chiếu assembly.
    /// PlayerController đọc để khóa input; DialogueUI ghi khi mở/đóng.
    /// </summary>
    public static class DialogueState
    {
        /// <summary>True khi UI hội thoại đang hiện — player bị khóa di chuyển.</summary>
        public static bool IsActive { get; set; }
    }
}
