#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Attrition.Data;
using Attrition.Gameplay.Environment;
using Attrition.Gameplay.NPC;

namespace Attrition.Editor
{
    public static class Map5EndingSetupEditor
    {
        private const string DataDir = "Assets/_Project/Data/NPC/FairyDialogue";
        private const string QuestPath = "Assets/_Project/Data/NPC/AccessoryQuests/Quest_M5_FinalBoss.asset";
        private const string SpringGuid = "223b76a1e4ab32d4d991aa3b376c675f";
        private const string SummerGuid = "ee324b2e47908084c9e694d8fe317740";
        private const string AutumnGuid = "57e43c88fb242bc459ba1b3f79f2ccf5";
        private const string WinterGuid = "cdefc836467ba1644934e92fad59d561";
        private const string HealthFlaskGuid = "b8c8ac1fa32085b4da6d9024134fdd90";

        [MenuItem("Tools/Attrition/World/Setup Map 5 Final Quest + Ending")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Castle - Map 5")
            {
                EditorUtility.DisplayDialog("Map 5 Ending", "Hãy mở scene 'Castle - Map 5' trước.", "OK");
                return;
            }
            if (GameObject.Find("Map5Finale") != null)
            {
                EditorUtility.DisplayDialog("Map 5 Ending", "Scene đã có Map5Finale; không tạo trùng.", "OK");
                return;
            }

            var quest = MakeFinalQuest();
            var springIdle = MakeDialogue("Dlg_M5_Spring_FinalQuest", new DialogueLine
            {
                speakerName = "Spring Fairy",
                text = "The last tyrant waits ahead. End this journey together."
            });
            var summerIdle = MakeDialogue("Dlg_M5_Summer_Encourage", new DialogueLine
            {
                speakerName = "Summer Fairy",
                text = "You have crossed every trial. Trust each other, and do not stop now."
            });
            var autumnIdle = MakeDialogue("Dlg_M5_Autumn_Encourage", new DialogueLine
            {
                speakerName = "Autumn Fairy",
                text = "The final guardian is near. Whatever follows, your courage has brought hope back."
            });
            var winterIdle = MakeDialogue("Dlg_M5_Winter_HPGuide", new DialogueLine
            {
                speakerName = "Winter Fairy",
                text = "A health flask is hidden away from the main path. Search the quiet passage before facing the tyrant."
            });
            var thanks = MakeDialogue("Dlg_M5_Ending_Thanks",
                new DialogueLine { speakerName = "Spring Fairy", text = "The corruption is fading. You gave this land another spring." },
                new DialogueLine { speakerName = "Summer Fairy", text = "Your strength carried hope through the darkest road." },
                new DialogueLine { speakerName = "Autumn Fairy", text = "Every sacrifice brought us to this peaceful end." },
                new DialogueLine { speakerName = "Winter Fairy", text = "Thank you. Your journey will be remembered." });

            var root = new GameObject("Map5Finale");
            Undo.RegisterCreatedObjectUndo(root, "Setup Map 5 Finale");

            var bossTrigger = Object.FindFirstObjectByType<BossEncounterTrigger>();
            Vector3 beforeBoss = bossTrigger != null ? bossTrigger.transform.position + Vector3.left * 8f : new Vector3(220f, 18f, 0f);
            var guides = new GameObject("BeforeBossFairies");
            guides.transform.SetParent(root.transform);
            PlaceGuide(SpringGuid, "Spring_Fairy_FinalQuest", guides.transform, beforeBoss + Vector3.left * 4.5f, springIdle, quest, true);
            PlaceGuide(SummerGuid, "Summer_Fairy_Encourage", guides.transform, beforeBoss + Vector3.left * 1.5f, summerIdle, null, false);
            PlaceGuide(AutumnGuid, "Autumn_Fairy_Encourage", guides.transform, beforeBoss + Vector3.right * 1.5f, autumnIdle, null, false);
            PlaceGuide(WinterGuid, "Winter_Fairy_HPGuide", guides.transform, beforeBoss + Vector3.right * 4.5f, winterIdle, null, false);
            PlacePrefab(HealthFlaskGuid, "Map5_HiddenHealthFlask", root.transform,
                        beforeBoss + new Vector3(-6f, -2f, 0f));

            var exitDoor = GameObject.Find("ExitDoor");
            Vector3 afterDoor = exitDoor != null ? exitDoor.transform.position + Vector3.right * 5f : new Vector3(263f, 17f, 0f);
            var ending = new GameObject("EndingFairies");
            ending.transform.SetParent(root.transform);
            PlaceGuide(SpringGuid, "Ending_Spring_Fairy", ending.transform, afterDoor + Vector3.left * 3f, thanks, null, false);
            PlaceGuide(SummerGuid, "Ending_Summer_Fairy", ending.transform, afterDoor + Vector3.left, thanks, null, false);
            PlaceGuide(AutumnGuid, "Ending_Autumn_Fairy", ending.transform, afterDoor + Vector3.right, thanks, null, false);
            PlaceGuide(WinterGuid, "Ending_Winter_Fairy", ending.transform, afterDoor + Vector3.right * 3f, thanks, null, false);

            var zone = FindFinalZone();
            if (zone != null)
            {
                var so = new SerializedObject(zone);
                so.FindProperty("endingDialogue").objectReferenceValue = thanks;
                so.FindProperty("showEndingTitle").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                zone.transform.position = afterDoor + Vector3.right * 6f;
            }
            else Debug.LogWarning("[Map5Ending] Không tìm thấy RoomTransitionZone tới Main_Menu_UI.");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[Map5Ending] Đã dựng quest cuối, fairies trước/sau boss và ending zone. Kiểm tra Y/ground rồi Save scene để Fusion bake NetworkObject.");
        }

        private static QuestSO MakeFinalQuest()
        {
            var q = AssetDatabase.LoadAssetAtPath<QuestSO>(QuestPath);
            if (q == null)
            {
                q = ScriptableObject.CreateInstance<QuestSO>();
                AssetDatabase.CreateAsset(q, QuestPath);
            }
            q.questId = "m5_defeat_archdemon";
            q.title = "The Last Tyrant";
            q.description = "Defeat the Arch Demon and bring the journey to its end.";
            q.objectiveType = QuestObjectiveType.Kill;
            q.targetId = "archdemon";
            q.requiredTargetIds = new string[0];
            q.requiredAmount = 1;
            q.expReward = 0;
            q.itemRewards = new QuestItemReward[0];
            q.dialogueNotStarted = null;
            q.dialogueInProgress = null;
            q.dialogueCompleted = null;
            q.dialogueFinished = null;
            EditorUtility.SetDirty(q);
            return q;
        }

        private static DialogueSO MakeDialogue(string name, params DialogueLine[] lines)
        {
            string path = $"{DataDir}/{name}.asset";
            var d = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<DialogueSO>();
                AssetDatabase.CreateAsset(d, path);
            }
            d.lines = lines;
            EditorUtility.SetDirty(d);
            return d;
        }

        private static void PlacePrefab(string guid, string name, Transform parent, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) { Debug.LogError($"[Map5Ending] Thiếu prefab {name}."); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, $"Place {name}");
        }

        private static void PlaceGuide(string guid, string name, Transform parent, Vector3 position,
                                       DialogueSO dialogue, QuestSO quest, bool autoQuest)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) { Debug.LogError($"[Map5Ending] Thiếu prefab {name}."); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, "Place Map 5 Fairy");

            var npc = go.GetComponent<NetworkNPC>();
            if (npc == null) return;
            var so = new SerializedObject(npc);
            so.FindProperty("idleDialogue").objectReferenceValue = dialogue;
            so.FindProperty("quest").objectReferenceValue = quest;
            so.FindProperty("extraQuests").arraySize = 0;
            so.FindProperty("claimForNpc").objectReferenceValue = null;
            so.FindProperty("turnInObjectiveKey").stringValue = "";
            so.FindProperty("turnInDialogue").objectReferenceValue = null;
            so.FindProperty("turnInDoneDialogue").objectReferenceValue = null;
            so.FindProperty("autoStartQuest").boolValue = autoQuest;
            so.FindProperty("autoFinishWithoutReward").boolValue = autoQuest;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RoomTransitionZone FindFinalZone()
        {
            foreach (var zone in Object.FindObjectsByType<RoomTransitionZone>(FindObjectsSortMode.None))
            {
                var p = new SerializedObject(zone).FindProperty("nextSceneName");
                if (p != null && p.stringValue == "Main_Menu_UI") return zone;
            }
            return null;
        }
    }
}
#endif
