using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Player;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.Gameplay.NPC
{
    /// <summary>
    /// NPC controller gắn vào NPC prefab (scene-placed NetworkObject).
    /// - Quản lý trạng thái quest [Networked] (CHUNG cho cả hai player).
    /// - Cung cấp RPC để UI gọi (accept, claim reward).
    /// - Phát thưởng cho TẤT CẢ player khi hoàn thành quest.
    ///
    /// Prefab cần: NetworkObject + Collider2D (Is Trigger) cho vùng tương tác.
    /// Prompt "[F] Talk" do DialogueUI (UI Toolkit) render — PlayerController phát hiện
    /// NPC gần qua trigger và expose IsNearNPC/CurrentNPC, không cần world-canvas trên NPC.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NetworkNPC : NetworkBehaviour
    {
        // ═══════════════════════════════════════════
        //  DATA (gán trong Inspector)
        // ═══════════════════════════════════════════

        [Header("──── NPC IDENTITY ────")]
        [Tooltip("Tên NPC hiển thị trong hội thoại.")]
        [SerializeField] private string npcName = "NPC";

        [Header("──── QUEST (tùy chọn) ────")]
        [Tooltip("Nhiệm vụ NPC giao. Bỏ trống = NPC chỉ nói chuyện thường.")]
        [SerializeField] private QuestSO quest;

        [Header("──── IDLE DIALOGUE (không có quest) ────")]
        [Tooltip("Hội thoại mặc định khi NPC không giao quest hoặc quest đã xong.")]
        [SerializeField] private DialogueSO idleDialogue;

        // ═══════════════════════════════════════════
        //  NETWORKED QUEST STATE (shared cả hai player)
        // ═══════════════════════════════════════════

        /// <summary>0=NotStarted, 1=Active, 2=Completed, 3=Rewarded</summary>
        [Networked] public byte QuestState { get; set; }

        /// <summary>Tiến độ hiện tại (VD: số quái đã giết).</summary>
        [Networked] public int QuestProgress { get; set; }

        // ═══════════════════════════════════════════
        //  PUBLIC ACCESSORS (DialogueUI đọc)
        // ═══════════════════════════════════════════

        public string NpcName => npcName;
        public QuestSO Quest => quest;
        public DialogueSO IdleDialogue => idleDialogue;

        /// <summary>Có quest và quest đang active?</summary>
        public bool HasActiveQuest => quest != null && QuestState == 1;

        /// <summary>Số lượng mục tiêu cần hoàn thành.</summary>
        public int RequiredAmount => quest != null ? quest.requiredAmount : 0;

        /// <summary>Lấy DialogueSO phù hợp với trạng thái quest hiện tại.</summary>
        public DialogueSO GetCurrentDialogue()
        {
            if (quest == null) return idleDialogue;

            switch (QuestState)
            {
                case 0: return quest.dialogueNotStarted ?? idleDialogue;
                case 1: return quest.dialogueInProgress ?? idleDialogue;
                case 2: return quest.dialogueCompleted ?? idleDialogue;
                case 3: return quest.dialogueFinished ?? idleDialogue;
                default: return idleDialogue;
            }
        }

        // ═══════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════

        public override void Spawned()
        {
            // Nhãn tên nổi trên đầu NPC (world-space, mọi máy thấy giống nhau). Vàng kim cho NPC.
            string display = string.IsNullOrEmpty(npcName) ? "NPC" : npcName;
            Attrition.Gameplay.WorldNameLabel.Attach(
                transform, display, new Vector3(0f, 0.95f, 0f), new Color(0.91f, 0.78f, 0.41f), 3f);

            // Host khôi phục tiến trình quest đã lưu (solo local). Mỗi NPC tự đọc questId của mình
            // → không lệ thuộc thứ tự spawn. Online: server là nguồn, không nạp từ slot.
            RestoreSavedProgress();
        }

        /// <summary>Host nạp lại state/progress của quest NPC này từ save slot (solo). No-op nếu chưa có.</summary>
        private void RestoreSavedProgress()
        {
            if (!HasStateAuthority || quest == null || string.IsNullOrEmpty(quest.questId)) return;
            if (Attrition.Persistence.GameLaunch.IsOnline) return;

            var data = Attrition.Persistence.SaveManager.LoadSlot(Attrition.Persistence.GameLaunch.SelectedSlot);
            if (data?.quests == null) return;

            foreach (var e in data.quests)
            {
                if (e == null || e.questId != quest.questId) continue;
                QuestState = e.state;
                QuestProgress = e.progress;
                break;
            }
        }

        // ═══════════════════════════════════════════
        //  RPC — Client → Host
        // ═══════════════════════════════════════════

        /// <summary>Player nhấn Accept trong UI → nhận quest.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcAcceptQuest()
        {
            if (quest == null || QuestState != 0) return;
            QuestState = 1; // Active
            QuestProgress = 0;
        }

        /// <summary>Player nhấn Decline → không nhận quest, đóng dialogue.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcDeclineQuest()
        {
            // Không làm gì — quest vẫn NotStarted, player có thể quay lại sau.
        }

        /// <summary>Player quay lại NPC khi quest đã hoàn thành → nhận thưởng.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcClaimReward()
        {
            if (quest == null || QuestState != 2) return;
            QuestState = 3; // Rewarded
            DistributeRewards();
        }

        // ═══════════════════════════════════════════
        //  QUEST PROGRESS — Host xử lý
        // ═══════════════════════════════════════════

        /// <summary>
        /// Static: gọi khi quái chết (EnemyController.DieFinal).
        /// Host duyệt tất cả NPC, cộng tiến độ nếu enemyId khớp.
        /// </summary>
        public static void NotifyEnemyKilled(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (!npc.HasStateAuthority) continue;
                if (npc.quest == null || npc.QuestState != 1) continue;
                if (npc.quest.objectiveType != QuestObjectiveType.Kill) continue;
                if (npc.quest.targetId != enemyId) continue;

                npc.QuestProgress++;
                if (npc.QuestProgress >= npc.quest.requiredAmount)
                    npc.QuestState = 2; // Completed
            }
        }

        /// <summary>
        /// Static: gọi bởi PuzzleController/Switch khi mục tiêu tùy chỉnh hoàn thành.
        /// VD: NetworkNPC.NotifyCustomObjective("puzzle_gate_1");
        /// </summary>
        public static void NotifyCustomObjective(string objectiveKey)
        {
            if (string.IsNullOrEmpty(objectiveKey)) return;
            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (!npc.HasStateAuthority) continue;
                if (npc.quest == null || npc.QuestState != 1) continue;
                if (npc.quest.objectiveType != QuestObjectiveType.Custom) continue;
                if (npc.quest.targetId != objectiveKey) continue;

                npc.QuestProgress++;
                if (npc.QuestProgress >= npc.quest.requiredAmount)
                    npc.QuestState = 2; // Completed
            }
        }

        // ═══════════════════════════════════════════
        //  SAVE / LOAD — Host gom & khôi phục tiến trình quest
        // ═══════════════════════════════════════════

        /// <summary>
        /// Host gom trạng thái quest của MỌI NPC trong scene để lưu vào save slot.
        /// Chỉ lưu NPC có quest và đã có tiến triển (state>0) để file gọn.
        /// </summary>
        public static Attrition.Persistence.QuestProgressEntry[] CaptureAll()
        {
            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Attrition.Persistence.QuestProgressEntry>();
            foreach (var npc in npcs)
            {
                if (npc.quest == null || string.IsNullOrEmpty(npc.quest.questId)) continue;
                if (npc.QuestState == 0) continue; // chưa nhận → khỏi lưu
                list.Add(new Attrition.Persistence.QuestProgressEntry
                {
                    questId = npc.quest.questId,
                    state = npc.QuestState,
                    progress = npc.QuestProgress
                });
            }
            return list.ToArray();
        }

        // ═══════════════════════════════════════════
        //  REWARD DISTRIBUTION — Host
        // ═══════════════════════════════════════════

        private void DistributeRewards()
        {
            if (!HasStateAuthority || quest == null) return;
            var db = ItemDatabaseSO.Instance;

            // EXP → cộng cho TẤT CẢ player (giống cơ chế coop khi quái chết)
            if (quest.expReward > 0)
            {
                var progressions = FindObjectsByType<PlayerProgression>(FindObjectsSortMode.None);
                foreach (var p in progressions)
                    if (p != null) p.GainExp(quest.expReward);
            }

            // Items → thêm thẳng vào Inventory CẢ HAI player
            if (quest.itemRewards != null && db != null)
            {
                var inventories = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
                foreach (var reward in quest.itemRewards)
                {
                    if (string.IsNullOrEmpty(reward.itemId) || reward.amount <= 0) continue;
                    int idx = db.GetIndex(reward.itemId);
                    if (idx < 0) continue;
                    foreach (var inv in inventories)
                        if (inv != null) inv.TryAddItem(idx, reward.amount);
                }
            }

            // Fire event → DialogueUI hiện popup "Congratulations!" trên tất cả client
            RpcNotifyRewards();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcNotifyRewards()
        {
            if (quest == null) return;

            // Fire local events để DialogueUI hiện popup
            if (quest.expReward > 0)
                RewardEvents.NotifyExp(quest.expReward);

            var db = ItemDatabaseSO.Instance;
            if (quest.itemRewards != null && db != null)
            {
                foreach (var reward in quest.itemRewards)
                {
                    if (string.IsNullOrEmpty(reward.itemId) || reward.amount <= 0) continue;
                    RewardEvents.NotifyItem(reward.itemId, reward.amount);
                }
            }

            RewardEvents.NotifyBatchComplete();
        }
    }
}
