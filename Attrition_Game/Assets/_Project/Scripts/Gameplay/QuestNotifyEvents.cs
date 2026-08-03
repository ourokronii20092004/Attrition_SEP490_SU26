using System;

namespace Attrition.Controllers
{
    /// <summary>
    /// Cầu nối "quest vừa xong mục tiêu" → UI (Gameplay không ref UI, giống SaveNotifyEvents).
    /// NetworkNPC phát khi QuestState chuyển sang Completed; GameUIController hiện toast nhắc player
    /// quay lại ĐÚNG NPC để nộp — người test hay bỏ qua bước nộp nên cần nhắc ngay lúc vừa xong.
    /// Chuỗi tiếng Anh vì hiển thị thẳng lên HUD.
    /// </summary>
    public static class QuestNotifyEvents
    {
        /// <summary>(questTitle, npcName) — đã đủ mục tiêu, chờ nộp.</summary>
        public static event Action<string, string> OnObjectiveComplete;

        public static void RaiseObjectiveComplete(string questTitle, string npcName)
            => OnObjectiveComplete?.Invoke(questTitle, npcName);
    }
}
