using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Attrition.Data;

namespace Attrition.Editor
{
    /// <summary>
    /// Đặt >=3 NPC (dùng lại prefab Fairy có sẵn) vào scene đang mở + sinh QuestSO/DialogueSO mẫu cho
    /// mỗi NPC (1 kill-quest, 1 idle, 1 kill-quest khác). Theo pattern editor setup tool: designer chạy
    /// menu → đặt sẵn NPC → SAVE scene để Fusion bake NetworkObject. Quest/dialogue asset sinh vào
    /// Data/NPC/Generated. Chạy lại an toàn: bỏ qua nếu scene đã có NPCGroup.
    ///
    /// Menu: Tools/Attrition/NPC/Populate Current Scene (3 NPCs)
    /// </summary>
    public static class SceneNpcSetupEditor
    {
        private const string FairySpringGuid = "223b76a1e4ab32d4d991aa3b376c675f";
        private const string FairySummerGuid = "ee324b2e47908084c9e694d8fe317740";
        private const string FairyWinterGuid = "cdefc836467ba1644934e92fad59d561";
        private const string GenFolder = "Assets/_Project/Data/NPC/Generated";

        // enemyId hợp lệ (khớp EnemyStats.EnemyId) để kill-quest đếm được.
        private static readonly string[] KillTargets = { "slime", "bat", "skeleton_sword", "undead", "mushroom" };

        [MenuItem("Tools/Attrition/NPC/Populate Current Scene (3 NPCs)")]
        public static void Populate()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                EditorUtility.DisplayDialog("NPC Setup", "Mở một scene map trước đã.", "OK");
                return;
            }

            // Idempotent: nếu đã có group thì thôi.
            if (GameObject.Find("NPCGroup") != null)
            {
                EditorUtility.DisplayDialog("NPC Setup",
                    $"Scene '{scene.name}' đã có NPCGroup — bỏ qua. Xóa nó nếu muốn tạo lại.", "OK");
                return;
            }

            EnsureFolder();

            var springPrefab = LoadPrefab(FairySpringGuid);
            var summerPrefab = LoadPrefab(FairySummerGuid);
            var winterPrefab = LoadPrefab(FairyWinterGuid);
            if (springPrefab == null || summerPrefab == null || winterPrefab == null)
            {
                Debug.LogError("[Attrition] Không load được prefab Fairy — kiểm tra Prefabs/NPC.");
                return;
            }

            var group = new GameObject("NPCGroup");
            Undo.RegisterCreatedObjectUndo(group, "Populate NPCs");
            var sv = SceneView.lastActiveSceneView;
            Vector3 origin = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            // Prefix scene để questId/asset không trùng giữa các map.
            string key = Sanitize(scene.name);
            string speaker = SpeakerFor(scene.name);
            string target = KillTargets[Mathf.Abs(key.GetHashCode()) % KillTargets.Length];

            // NPC 1: giao kill-quest.
            var q1 = MakeKillQuest(key, "01", speaker, target, 5, 150, "leather_helm");
            SpawnNpc(springPrefab, group.transform, origin + new Vector3(-3f, 0f, 0f),
                $"{speaker} Elder", q1, null);

            // NPC 2: chỉ trò chuyện (idle).
            var idle = MakeIdleDialogue(key, speaker);
            SpawnNpc(summerPrefab, group.transform, origin + new Vector3(0f, 0f, 0f),
                $"{speaker} Wanderer", null, idle);

            // NPC 3: giao kill-quest khác (target kế tiếp).
            string target2 = KillTargets[(Mathf.Abs(key.GetHashCode()) + 1) % KillTargets.Length];
            var q2 = MakeKillQuest(key, "02", speaker, target2, 8, 220, "iron_helm");
            SpawnNpc(winterPrefab, group.transform, origin + new Vector3(3f, 0f, 0f),
                $"{speaker} Sentinel", q2, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = group;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Attrition] Đã đặt 3 NPC vào '{scene.name}'. Chỉnh vị trí cho khớp mặt đất " +
                      "rồi SAVE scene để Fusion bake NetworkObject.");
        }

        private static GameObject LoadPrefab(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void SpawnNpc(GameObject prefab, Transform parent, Vector3 pos,
            string npcName, QuestSO quest, DialogueSO idle)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;

            // NetworkNPC private fields set qua SerializedObject (pattern editor tool).
            var npc = go.GetComponent<Attrition.Gameplay.NPC.NetworkNPC>();
            if (npc == null)
            {
                Debug.LogWarning($"[Attrition] Prefab '{prefab.name}' thiếu NetworkNPC — bỏ qua set field.");
                return;
            }
            var so = new SerializedObject(npc);
            so.FindProperty("npcName").stringValue = npcName;
            so.FindProperty("quest").objectReferenceValue = quest;
            so.FindProperty("idleDialogue").objectReferenceValue = idle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        //  ASSET GEN

        private static QuestSO MakeKillQuest(string key, string suffix, string speaker,
            string enemyId, int amount, int exp, string rewardItem)
        {
            var offer = MakeDialogue($"Dialogue_{key}_{suffix}_Offer", new[]
            {
                new DialogueLine { speakerName = speaker, text = $"Traveler, danger stalks these lands." },
                new DialogueLine { speakerName = speaker, text = $"Slay {amount} {Pretty(enemyId)} for us and be rewarded." },
            });
            var prog = MakeDialogue($"Dialogue_{key}_{suffix}_InProgress", new[]
            {
                new DialogueLine { speakerName = speaker, text = $"The {Pretty(enemyId)} still roam. Keep at it." },
            });
            var done = MakeDialogue($"Dialogue_{key}_{suffix}_Complete", new[]
            {
                new DialogueLine { speakerName = speaker, text = "You've done it! Here is your reward." },
            });
            var fin = MakeDialogue($"Dialogue_{key}_{suffix}_Finished", new[]
            {
                new DialogueLine { speakerName = speaker, text = "Thank you again. Safe travels." },
            });

            var quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.questId = $"{key}_{suffix}_slay_{enemyId}";
            quest.title = $"Cull the {Pretty(enemyId)}";
            quest.description = $"Slay {amount} {Pretty(enemyId)}.";
            quest.objectiveType = QuestObjectiveType.Kill;
            quest.targetId = enemyId;
            quest.requiredAmount = amount;
            quest.expReward = exp;
            quest.itemRewards = new[] { new QuestItemReward { itemId = rewardItem, amount = 1 } };
            quest.dialogueNotStarted = offer;
            quest.dialogueInProgress = prog;
            quest.dialogueCompleted = done;
            quest.dialogueFinished = fin;
            AssetDatabase.CreateAsset(quest, $"{GenFolder}/Quest_{key}_{suffix}.asset");
            return quest;
        }

        private static DialogueSO MakeIdleDialogue(string key, string speaker)
        {
            return MakeDialogue($"Dialogue_{key}_Idle", new[]
            {
                new DialogueLine { speakerName = speaker, text = "Rest a moment, traveler. The road ahead is long." },
                new DialogueLine { speakerName = speaker, text = "Beware what waits in the deeper reaches." },
            });
        }

        private static DialogueSO MakeDialogue(string assetName, DialogueLine[] lines)
        {
            var d = ScriptableObject.CreateInstance<DialogueSO>();
            d.lines = lines;
            AssetDatabase.CreateAsset(d, $"{GenFolder}/{assetName}.asset");
            return d;
        }

        //  HELPERS

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/NPC"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "NPC");
            if (!AssetDatabase.IsValidFolder(GenFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Data/NPC", "Generated");
        }

        private static string SpeakerFor(string sceneName)
        {
            string s = sceneName.ToLowerInvariant();
            if (s.Contains("forest") && !s.Contains("dark")) return "Druid";
            if (s.Contains("valley") || s.Contains("elf")) return "Elf";
            if (s.Contains("dark")) return "Demonkin";
            if (s.Contains("castle")) return "Warden";
            return "Villager";
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        private static string Pretty(string enemyId)
            => string.IsNullOrEmpty(enemyId) ? "" : enemyId.Replace('_', ' ');
    }
}
