using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Data;
using Attrition.Gameplay.NPC;

namespace Attrition.UI
{
    /// <summary>
    /// Tab NHIỆM VỤ trong bảng Tab — nằm cùng hàng với EQUIPMENT / ACCESSORY / SKILL (yêu cầu user).
    ///
    /// Liệt kê mọi nhiệm vụ ĐÃ NHẬN mà CHƯA nhận thưởng: state 1 (đang làm) và state 2 (xong mục tiêu,
    /// chờ nộp). Nhiệm vụ chưa nhận (state 0) và đã nhận thưởng (state 3) không hiện — đúng nghĩa "log
    /// nhiệm vụ đã nhận chưa hoàn thành".
    ///
    /// Nguồn dữ liệu là các `NetworkNPC` trong scene (state/progress đều [Networked]) nên client đọc được
    /// y như host. Tái dùng class USS của quest tracker (`tracker-entry*`) để không phải thêm style mới.
    /// </summary>
    public partial class GameUIController
    {
        /// <summary>Dựng lại danh sách nhiệm vụ. Gọi khi mở tab Quest và khi mở bảng Tab.</summary>
        private void RefreshQuestLog()
        {
            var list = _root?.Q<VisualElement>("inv-quest-list");
            if (list == null) return;

            list.Clear();
            int shown = 0;

            foreach (var npc in FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None))
            {
                if (npc == null) continue;

                // Chỉ lấy NPC GIỮ quest. NPC "nhận nộp hộ" (Autumn/Summer) có `Quest` null hoặc trỏ nhiệm
                // vụ riêng của nó, nên không bị liệt kê trùng cùng một nhiệm vụ hai lần.
                var q = npc.Quest;
                if (q == null) continue;

                byte state = npc.QuestState;
                if (state != 1 && state != 2) continue;   // 0 = chưa nhận, 3 = đã nhận thưởng

                list.Add(BuildQuestEntry(npc, q, state == 2));
                shown++;
            }

            if (shown == 0)
            {
                var empty = new Label("No active quests.");
                empty.AddToClassList("tracker-entry-progress");
                empty.style.paddingLeft = 6;
                empty.style.paddingTop = 10;
                list.Add(empty);
            }
        }

        /// <summary>Một dòng nhiệm vụ: tiêu đề + mô tả + tiến độ + thanh tiến độ.</summary>
        private VisualElement BuildQuestEntry(NetworkNPC npc, QuestSO q, bool isComplete)
        {
            var entry = new VisualElement();
            entry.AddToClassList("tracker-entry");
            if (isComplete) entry.AddToClassList("tracker-entry-complete");

            var title = new Label(isComplete ? $"✓ {q.title}" : q.title);
            title.AddToClassList("tracker-entry-title");
            entry.Add(title);

            if (!string.IsNullOrEmpty(q.description))
            {
                var desc = new Label(q.description);
                desc.AddToClassList("tracker-entry-progress");
                desc.style.whiteSpace = WhiteSpace.Normal;
                entry.Add(desc);
            }

            // Xong mục tiêu → nhắc đi nộp (player hay quên phải quay lại NPC nào).
            var progress = new Label(isComplete
                ? $"Complete — return to {npc.NpcName} to claim your reward"
                : $"{npc.QuestProgress}/{q.requiredAmount} "
                  + (q.objectiveType == QuestObjectiveType.Kill ? "defeated" : "completed"));
            progress.AddToClassList("tracker-entry-progress");
            progress.style.whiteSpace = WhiteSpace.Normal;
            entry.Add(progress);

            var barBg = new VisualElement();
            barBg.AddToClassList("tracker-progress-bar-bg");
            var barFill = new VisualElement();
            barFill.AddToClassList("tracker-progress-bar-fill");
            float pct = q.requiredAmount > 0
                ? Mathf.Clamp01((float)npc.QuestProgress / q.requiredAmount)
                : (isComplete ? 1f : 0f);
            barFill.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));
            barBg.Add(barFill);
            entry.Add(barBg);

            return entry;
        }
    }
}
