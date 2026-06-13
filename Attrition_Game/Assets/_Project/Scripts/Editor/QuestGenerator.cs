using UnityEngine;
using UnityEditor;
using Attrition.Data;

namespace Attrition.Editor
{
    public class QuestGenerator
    {
        [MenuItem("Attrition/Tools/Generate Demo Quest")]
        public static void GenerateSlimeQuest()
        {
            // Đảm bảo thư mục tồn tại
            string folderPath = "Assets/_Project/Data/NPC";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/NPC"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "NPC");

            // 1. Tạo DialogueSO: Offer
            var offer = ScriptableObject.CreateInstance<DialogueSO>();
            offer.lines = new DialogueLine[]
            {
                new DialogueLine { speakerName = "Old Sage", text = "Greetings, brave warrior." },
                new DialogueLine { speakerName = "Old Sage", text = "The slimes in the eastern cave have been causing havoc. Could you slay 3 of them for me?" }
            };
            AssetDatabase.CreateAsset(offer, $"{folderPath}/Dialogue_SlimeQuest_Offer.asset");

            // 2. Tạo DialogueSO: In Progress
            var inProgress = ScriptableObject.CreateInstance<DialogueSO>();
            inProgress.lines = new DialogueLine[]
            {
                new DialogueLine { speakerName = "Old Sage", text = "Still working on those slimes? Keep at it!" }
            };
            AssetDatabase.CreateAsset(inProgress, $"{folderPath}/Dialogue_SlimeQuest_InProgress.asset");

            // 3. Tạo DialogueSO: Complete
            var complete = ScriptableObject.CreateInstance<DialogueSO>();
            complete.lines = new DialogueLine[]
            {
                new DialogueLine { speakerName = "Old Sage", text = "Excellent work! You've proven yourself worthy." },
                new DialogueLine { speakerName = "Old Sage", text = "Here is your reward. You've earned it!" }
            };
            AssetDatabase.CreateAsset(complete, $"{folderPath}/Dialogue_SlimeQuest_Complete.asset");

            // 4. Tạo DialogueSO: Finished
            var finished = ScriptableObject.CreateInstance<DialogueSO>();
            finished.lines = new DialogueLine[]
            {
                new DialogueLine { speakerName = "Old Sage", text = "Thank you again for your help. Safe travels!" }
            };
            AssetDatabase.CreateAsset(finished, $"{folderPath}/Dialogue_SlimeQuest_Finished.asset");

            // 5. Tạo QuestSO
            var quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.questId = "slay_slimes";
            quest.title = "Slime Extermination";
            quest.description = "Slay 3 Slimes in the eastern cave.";
            quest.objectiveType = QuestObjectiveType.Kill;
            quest.targetId = "slime";
            quest.requiredAmount = 3;
            quest.expReward = 150;
            quest.itemRewards = new QuestItemReward[]
            {
                new QuestItemReward { itemId = "leather_helm", amount = 1 }
            };

            // Link dialogues
            quest.dialogueNotStarted = offer;
            quest.dialogueInProgress = inProgress;
            quest.dialogueCompleted = complete;
            quest.dialogueFinished = finished;

            AssetDatabase.CreateAsset(quest, $"{folderPath}/Quest_SlaySlimes.asset");

            // Lưu thay đổi
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Attrition] Đã tạo thành công Demo Quest tại: {folderPath}");
        }
    }
}
