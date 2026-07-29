using System;

namespace Attrition.Data
{
    public static class DialogueEvents
    {
        /// <summary>
        /// Kích hoạt mở hộp thoại Custom (dùng cho Boss hoặc các sự kiện đặc biệt).
        /// Tham số 1: DialogueSO chứa nội dung thoại.
        /// Tham số 2: Action callback gọi khi thoại kết thúc.
        /// </summary>
        public static Action<DialogueSO, Action> OnOpenCustomDialogue;

        /// <summary>
        /// Yêu cầu ĐÓNG hộp thoại đang mở ngay (không chờ người chơi đọc hết).
        /// Dùng khi bỏ qua cutscene giữa lúc đang thoại. Callback onComplete của thoại
        /// vẫn chạy như bình thường (DialogueUI.CloseDialogue tự gọi).
        /// </summary>
        public static Action OnForceCloseDialogue;
    }
}
