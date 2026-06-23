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
    }
}
