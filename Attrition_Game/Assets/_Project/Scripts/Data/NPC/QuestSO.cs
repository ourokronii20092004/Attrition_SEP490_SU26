using UnityEngine;

namespace Attrition.Data
{
    /// <summary>Loại mục tiêu nhiệm vụ.</summary>
    public enum QuestObjectiveType
    {
        /// <summary>Giết quái — targetId = enemyId (VD: "slime").</summary>
        Kill,
        /// <summary>Mục tiêu tùy chỉnh (bật công tắc, giải đố, mở cửa…) — targetId = custom key.</summary>
        Custom
    }

    /// <summary>
    /// Một phần thưởng item cụ thể.
    /// </summary>
    [System.Serializable]
    public class QuestItemReward
    {
        [Tooltip("itemId trong ItemDatabaseSO (VD: 'iron_helm').")]
        public string itemId;
        [Tooltip("Số lượng.")]
        public int amount = 1;
    }

    /// <summary>
    /// ScriptableObject định nghĩa 1 nhiệm vụ.
    /// Tạo qua: Create → Attrition → NPC → Quest.
    /// Thêm quest mới = tạo asset mới → kéo vào Inspector NPC. Không cần sửa code.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/NPC/Quest", fileName = "NewQuest")]
    public class QuestSO : ScriptableObject
    {
        [Header("──── IDENTITY ────")]
        [Tooltip("ID duy nhất toàn game. VD: 'slay_slimes', 'open_gate_1'.")]
        public string questId;
        [Tooltip("Tên hiển thị trên UI. VD: 'Slime Extermination'.")]
        public string title;
        [TextArea(2, 4)]
        [Tooltip("Mô tả ngắn cho quest tracker HUD.")]
        public string description;

        [Header("──── OBJECTIVE ────")]
        [Tooltip("Kill = giết quái theo targetId.\nCustom = mục tiêu tùy chỉnh (puzzle, switch…).")]
        public QuestObjectiveType objectiveType = QuestObjectiveType.Kill;
        [Tooltip("Kill: enemyId (VD: 'slime').\nCustom: key tùy ý (VD: 'puzzle_gate_1').")]
        public string targetId;
        [Tooltip("Số lượng cần hoàn thành. VD: 5 = giết 5 con.")]
        public int requiredAmount = 1;

        [Header("──── REWARDS ────")]
        [Tooltip("EXP nhận khi nộp quest (cộng cho CẢ HAI player).")]
        public int expReward;
        [Tooltip("Danh sách item thưởng — thêm thẳng vào Inventory cả hai player.")]
        public QuestItemReward[] itemRewards = new QuestItemReward[0];

        [Header("──── DIALOGUES (theo trạng thái quest) ────")]
        [Tooltip("Lần đầu nói chuyện — mời nhận quest.")]
        public DialogueSO dialogueNotStarted;
        [Tooltip("Đã nhận quest nhưng chưa xong — nhắc nhở.")]
        public DialogueSO dialogueInProgress;
        [Tooltip("Hoàn thành mục tiêu, quay lại NPC — nhận thưởng.")]
        public DialogueSO dialogueCompleted;
        [Tooltip("Sau khi đã nhận thưởng xong — lời cảm ơn.")]
        public DialogueSO dialogueFinished;
    }
}
