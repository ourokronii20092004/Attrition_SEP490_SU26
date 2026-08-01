#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Attrition.Data;
using Attrition.Gameplay.NPC;

namespace Attrition.Editor
{
    /// <summary>
    /// Đặt 4 NPC FAIRY THEO MÙA vào scene đang mở + gán quest/thoại đúng vai trò.
    ///
    /// VAI TRÒ — CHUỖI NỘP 3 CHẶNG (user chốt 2026-07-30):
    ///   • Spring — kể chuyện + cho biết map có bao nhiêu bình HP + GIAO quest diệt ELITE.
    ///              Map 3: thêm quest ĐƯA TIN vào chuỗi (làm sau elite).
    ///   • Summer — NHẬN NỘP quest elite (của Spring) + GIAO quest diệt BOSS.
    ///   • Autumn — NHẬN NỘP quest boss (của Summer). Map 3: cũng là NPC nhận tin.
    ///   • Winter — hướng dẫn giải puzzle / chỉ đường (chỉ thoại, không quest).
    ///
    /// HAI CƠ CHẾ làm được điều đó:
    ///   `claimForNpc` — trao thưởng HỘ NPC khác mà không nhân đôi state quest.
    ///   `extraQuests` — chuỗi nhiều nhiệm vụ trên cùng 1 NPC (NetworkNPC chỉ có 1 ô `quest`).
    /// Summer VỪA nhận nộp VỪA giao quest nhờ `HasClaimPending`: còn việc nộp hộ dở thì ưu tiên xử lý,
    /// xong rồi mới chào mời nhiệm vụ boss của chính nó.
    ///
    /// Menu: Tools/Attrition/NPC/Setup Season Fairies (scene dang mo)
    /// Idempotent: đã có "SeasonFairies" trong scene thì bỏ qua (xoá nó nếu muốn dựng lại).
    /// </summary>
    public static class SeasonFairySetupEditor
    {
        private const string SpringGuid = "223b76a1e4ab32d4d991aa3b376c675f";
        private const string SummerGuid = "ee324b2e47908084c9e694d8fe317740";
        private const string AutumnGuid = "57e43c88fb242bc459ba1b3f79f2ccf5";
        private const string WinterGuid = "cdefc836467ba1644934e92fad59d561";

        private const string QuestDir = "Assets/_Project/Data/NPC/AccessoryQuests";
        private const string GenDir = "Assets/_Project/Data/NPC/FairyDialogue";

        /// <summary>Số bình HP GIẤU trong mỗi map (map 1-4 mỗi map 1; map 5 không có).</summary>
        private static int HiddenFlasksIn(int map) => map >= 1 && map <= 4 ? 1 : 0;

        private static string NextRegion(int map) => map switch
        {
            1 => "the corrupted forest",
            2 => "the fallen elf valley",
            3 => "the dark forest",
            4 => "the demon castle",
            _ => "the end of this journey",
        };

        /// <summary>Tên scene → số map. Khớp danh sách trong HazardTilemapSetupEditor.</summary>
        private static int MapOf(string sceneName)
        {
            if (sceneName.Contains("Map 1")) return 1;
            if (sceneName.Contains("Map 2")) return 2;
            if (sceneName.Contains("Map 3") || sceneName.Contains("Map3")) return 3;
            if (sceneName.Contains("Map 4")) return 4;
            if (sceneName.Contains("Map 5")) return 5;
            return 0;
        }

        [MenuItem("Tools/Attrition/NPC/Setup Season Fairies (scene dang mo)")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                EditorUtility.DisplayDialog("Season Fairies", "Mở một scene map trước đã.", "OK");
                return;
            }

            int map = MapOf(scene.name);
            if (map == 5)
            {
                Map5EndingSetupEditor.Setup();
                return;
            }
            if (map == 0)
            {
                EditorUtility.DisplayDialog("Season Fairies",
                    $"Không nhận ra số map từ tên scene '{scene.name}'. Cần chứa 'Map 1'..'Map 5'.", "OK");
                return;
            }

            if (GameObject.Find("SeasonFairies") != null)
            {
                EditorUtility.DisplayDialog("Season Fairies",
                    $"Scene '{scene.name}' đã có 'SeasonFairies' — bỏ qua. Xoá nó nếu muốn dựng lại.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(GenDir))
                AssetDatabase.CreateFolder("Assets/_Project/Data/NPC", "FairyDialogue");

            var group = new GameObject("SeasonFairies");
            Undo.RegisterCreatedObjectUndo(group, "Setup Season Fairies");

            var sv = SceneView.lastActiveSceneView;
            Vector3 origin = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            var warn = new List<string>();

            // ── SPRING: kể chuyện + báo số bình HP + GIAO quest diệt elite ──
            // Đặt TRƯỚC Summer/Autumn vì hai NPC kia cần tham chiếu tới nó (chuỗi nộp 3 chặng).
            var spring = Place(SpringGuid, "Spring_Fairy", group, origin + new Vector3(-8f, 0f, 0f));
            var springNpc = spring != null ? spring.GetComponent<NetworkNPC>() : null;
            if (springNpc != null)
            {
                SetString(springNpc, "npcName", "Spring Fairy");

                int hidden = HiddenFlasksIn(map);
                string flaskLine = hidden > 0
                    ? $"There {(hidden == 1 ? "is" : "are")} {hidden} hidden health flask{(hidden == 1 ? "" : "s")} in this region, beyond the usual paths."
                    : "No hidden health flasks remain here; only rewards taken from powerful foes.";

                SetObject(springNpc, "idleDialogue",
                    MakeDialogue($"Dlg_M{map}_Spring_Idle", "Spring Fairy",
                        $"This land is losing itself to corruption. {flaskLine}"));

                // Quest ELITE do Spring giao. Map 3 thêm quest ĐƯA TIN vào chuỗi (làm sau elite).
                var eliteQ = FindQuest(map, "elite", warn);
                SetObject(springNpc, "quest", eliteQ);

                var deliverQ = map == 3 ? FindQuest(3, "deliver", warn) : null;
                SetQuestArray(springNpc, "extraQuests",
                    deliverQ != null ? new[] { deliverQ } : new QuestSO[0]);
            }

            // ── SUMMER: NHẬN NỘP quest elite (của Spring) + GIAO quest diệt boss ──
            // Vừa nhận vừa giao được nhờ `HasClaimPending`: còn việc nộp hộ thì ưu tiên xử lý, xong rồi
            // mới chào mời quest riêng (boss).
            var summer = Place(SummerGuid, "Summer_Fairy", group, origin + new Vector3(-3f, 0f, 0f));
            var summerNpc = summer != null ? summer.GetComponent<NetworkNPC>() : null;
            if (summerNpc != null)
            {
                SetString(summerNpc, "npcName", "Summer Fairy");
                SetObject(summerNpc, "claimForNpc", springNpc);   // nhận nộp hộ Spring (elite)

                var bossQ = FindQuest(map, "boss", warn);          // map 5 CỐ Ý không có quest boss
                SetObject(summerNpc, "quest", bossQ);

                SetObject(summerNpc, "idleDialogue",
                    MakeDialogue($"Dlg_M{map}_Summer_Idle", "Summer Fairy",
                        "Power in this region will not come willingly. Go and claim it."));
            }

            // ── AUTUMN: NHẬN NỘP quest boss (của Summer) ──
            var autumn = Place(AutumnGuid, "Autumn_Fairy", group, origin + new Vector3(3f, 0f, 0f));
            var autumnNpc = autumn != null ? autumn.GetComponent<NetworkNPC>() : null;
            if (autumnNpc != null)
            {
                SetString(autumnNpc, "npcName", "Autumn Fairy");
                SetObject(autumnNpc, "claimForNpc", summerNpc);   // trao thưởng hộ Summer (boss)
                SetObject(autumnNpc, "idleDialogue",
                    MakeDialogue($"Dlg_M{map}_Autumn_Idle", "Autumn Fairy",
                        $"Defeat the guardian ahead and return. Beyond it lies {NextRegion(map)}."));

                // Map 3: Autumn (cuối map) là NPC NHẬN TIN của nhiệm vụ đưa tin do Spring (đầu map) giao.
                if (map == 3)
                {
                    SetString(autumnNpc, "turnInObjectiveKey", "deliver_m3_report");
                    SetObject(autumnNpc, "turnInDialogue",
                        MakeDialogue("Dlg_M3_Autumn_TurnIn", "Autumn Fairy",
                            "So she witnessed it herself... Her report has reached me."));
                    SetObject(autumnNpc, "turnInDoneDialogue",
                        MakeDialogue("Dlg_M3_Autumn_TurnInDone", "Autumn Fairy",
                            "The message has arrived. What follows is our burden."));
                }
            }

            // ── WINTER: hướng dẫn puzzle / chỉ đường ──
            var winter = Place(WinterGuid, "Winter_Fairy", group, origin + new Vector3(8f, 0f, 0f));
            var winterNpc = winter != null ? winter.GetComponent<NetworkNPC>() : null;
            if (winterNpc != null)
            {
                SetString(winterNpc, "npcName", "Winter Fairy");
                SetObject(winterNpc, "idleDialogue",
                    MakeDialogue($"Dlg_M{map}_Winter_Idle", "Winter Fairy",
                        "The hidden health flask lies away from the main road. Listen for a hollow wall and search beyond it."));
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = group;

            Debug.Log(
                $"[SeasonFairies] Map {map}: đã đặt 4 fairy trong 'SeasonFairies'.\n" +
                "CHUỖI NỘP 3 CHẶNG:\n" +
                "  Spring = kể chuyện + báo số bình HP giấu + GIAO quest diệt ELITE"
                    + (map == 3 ? " + giao quest ĐƯA TIN (sau elite)" : "") + "\n" +
                "  Summer = NHẬN NỘP elite (claimForNpc → Spring) + GIAO quest diệt BOSS\n" +
                "  Autumn = NHẬN NỘP boss (claimForNpc → Summer)"
                    + (map == 3 ? " + nhận tin đưa thư" : "") + "\n" +
                "  Winter = hướng dẫn puzzle/chỉ đường (chỉ thoại)\n\n" +
                "VIỆC CÒN LẠI: kéo từng fairy tới đúng chỗ trong map (Spring ĐẦU map, Autumn CUỐI map), " +
                "rồi SAVE scene để Fusion bake NetworkObject.");

            if (warn.Count > 0)
                Debug.LogWarning("[SeasonFairies] Thiếu quest asset: " + string.Join(", ", warn) +
                                 "\n→ Chạy 'Tools/Attrition/NPC/Generate Accessory Quests' TRƯỚC rồi chạy lại tool này.");
        }

        /// <summary>Tìm QuestSO theo quy ước tên của AccessoryQuestSetupEditor: Quest_M{map}_{tag}_{itemId}.</summary>
        private static QuestSO FindQuest(int map, string tag, List<string> warn)
        {
            string prefix = $"Quest_M{map}_{tag}_";
            foreach (var guid in AssetDatabase.FindAssets("t:QuestSO", new[] { QuestDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(prefix)) continue;
                return AssetDatabase.LoadAssetAtPath<QuestSO>(path);
            }

            // Map 5 CỐ Ý không có quest boss (boss cuối = kết game) → không cảnh báo.
            if (!(map == 5 && tag == "boss")) warn.Add(prefix + "*");
            return null;
        }

        private static GameObject Place(string prefabGuid, string name, GameObject parent, Vector3 pos)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[SeasonFairies] Không load được prefab {name} (guid {prefabGuid}).");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(go, "Place Fairy");
            return go;
        }

        /// <summary>Tạo DialogueSO 1 dòng (ghi đè nếu đã có, giữ GUID để không đứt tham chiếu).</summary>
        private static DialogueSO MakeDialogue(string assetName, string speaker, string text)
        {
            var d = ScriptableObject.CreateInstance<DialogueSO>();
            d.lines = new[] { new DialogueLine { speakerName = speaker, text = text } };

            string path = $"{GenDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(d, existing);
                Object.DestroyImmediate(d);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(d, path);
            return d;
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[SeasonFairies] Thiếu field '{field}'."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[SeasonFairies] Thiếu field '{field}'."); return; }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetQuestArray(Object target, string field, QuestSO[] values)
        {
            var so = new SerializedObject(target);
            var arr = so.FindProperty(field);
            if (arr == null) { Debug.LogWarning($"[SeasonFairies] Thiếu field '{field}'."); return; }
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
