#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Attrition.Controllers;
using Attrition.Data;
using Attrition.Gameplay.Environment;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Hoàn thiện phòng boss Map 2–5 theo Map 1 và đổi puzzle Map 4 từ lever sang pressure plate.
    /// Chạy một lần rồi review/save scene. Idempotent: chạy lại không tạo trùng trigger/gate/plate.
    /// </summary>
    public static class RemainingMapGameplaySetupEditor
    {
        private const string SceneDir = "Assets/_Project/Scenes/";

        private readonly struct MapSetup
        {
            public readonly string Scene;
            public readonly string BossAI;
            public readonly string NextScene;
            public readonly string BossName;

            public MapSetup(string scene, string bossAI, string nextScene, string bossName)
            {
                Scene = scene;
                BossAI = bossAI;
                NextScene = nextScene;
                BossName = bossName;
            }

            public string IntroPath => $"Assets/_Project/Data/Dialogue/Bosses/Dialogue_{BossName}_Intro.asset";
            public string DeathPath => $"Assets/_Project/Data/Dialogue/Bosses/Dialogue_{BossName}_Death.asset";
        }

        private static readonly MapSetup[] Maps =
        {
            new MapSetup("Forest - Map 2", "DruidBossAI", "Elf Valley -Map 3", "Druid"),
            new MapSetup("Elf Valley -Map 3", "ElfBossAI", "Dark Forest - Map 4", "Elf"),
            new MapSetup("Dark Forest - Map 4", "DemonKinBossAI", "Castle - Map 5", "DemonKin"),
            // ponytail: chưa có scene ending/credits riêng, boss cuối trả về Main Menu. Khi có ending scene,
            // đổi đúng một NextScene ở đây và thêm scene đó vào Build Settings.
            new MapSetup("Castle - Map 5", "ArchDemonBossAI", "Main_Menu_UI", "ArchDemon"),
        };

        [MenuItem("Tools/Attrition/World/Setup Boss Rooms Map 2-5 + Map 4 Plates")]
        public static void SetupAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var original = SceneManager.GetActiveScene().path;
            int completed = 0;

            try
            {
                EnsureBuildScenes();
                Attrition.Editor.Tools.BossDialogueGeneratorEditor.GenerateDialogue();
                foreach (var map in Maps)
                    if (SetupMap(map)) completed++;
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RemainingMaps] Xong {completed}/{Maps.Length} map. Map 5 về Main Menu sau boss cuối. " +
                      "Hãy playtest solo + host/client trước khi commit scene.");
        }

        private static bool SetupMap(MapSetup map)
        {
            string path = SceneDir + map.Scene + ".unity";
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (sceneAsset == null)
            {
                Debug.LogError($"[RemainingMaps] Không tìm thấy scene '{path}'.");
                return false;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var bossAI = FindBossAI(map.BossAI);
            if (bossAI == null)
            {
                Debug.LogError($"[RemainingMaps] {map.Scene}: không tìm thấy {map.BossAI}. Chạy Setup Boss Moveset trước.");
                return false;
            }

            var boss = bossAI.GetComponent<EnemyController>();
            if (boss == null)
            {
                Debug.LogError($"[RemainingMaps] {map.Scene}: {map.BossAI} thiếu EnemyController. Chạy Setup Boss Moveset trước.");
                return false;
            }

            var intro = AssetDatabase.LoadAssetAtPath<DialogueSO>(map.IntroPath);
            var death = AssetDatabase.LoadAssetAtPath<DialogueSO>(map.DeathPath);
            if (!HasLines(intro) || !HasLines(death))
            {
                Debug.LogError($"[RemainingMaps] {map.Scene}: thiếu hoặc rỗng boss dialogue.");
                return false;
            }

            var gate = FindBestGate(bossAI.transform.position) ?? CreateGate(bossAI.transform.position, map.NextScene);
            SetRef(gate, "boss", boss);
            SetRef(gate, "bossAI", bossAI);
            SetRef(gate, "deathDialogue", death);
            SetRef(bossAI, "introDialogue", intro);

            var entryDoor = GetRef<Door>(gate, "entryDoor");
            if (entryDoor == null)
            {
                Debug.LogError($"[RemainingMaps] {map.Scene}: BossGateController thiếu EntryDoor.");
                return false;
            }

            EnsureTrigger(bossAI, entryDoor.transform.position);
            SetBool(bossAI, "waitForTrigger", true);

            var zone = GetRef<RoomTransitionZone>(gate, "exitZone");
            if (zone != null) SetString(zone, "nextSceneName", map.NextScene);

            if (map.Scene == "Dark Forest - Map 4") MigrateMap4Puzzle();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RemainingMaps] {map.Scene}: boss={map.BossAI}, trigger/gate OK, next='{map.NextScene}'.");
            return true;
        }

        private static MonoBehaviour FindBossAI(string typeName)
        {
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && mb.GetType().Name == typeName && mb is Attrition.Core.IBossEncounter) return mb;
            return null;
        }

        private static BossGateController FindBestGate(Vector3 bossPosition)
        {
            BossGateController best = null;
            float bestDistance = float.MaxValue;
            foreach (var gate in UnityEngine.Object.FindObjectsByType<BossGateController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                float d = (gate.transform.position - bossPosition).sqrMagnitude;
                if (d < bestDistance) { best = gate; bestDistance = d; }
            }
            return best;
        }

        private static void EnsureTrigger(MonoBehaviour bossAI, Vector3 entryPosition)
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<BossEncounterTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing.boss == bossAI) return;
            }

            var go = new GameObject("BossEncounterTrigger");
            go.transform.position = Vector3.Lerp(entryPosition, bossAI.transform.position, 0.3f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(4f, 6f);
            var trigger = go.AddComponent<BossEncounterTrigger>();
            trigger.boss = bossAI;
        }

        private static BossGateController CreateGate(Vector3 bossPosition, string nextScene)
        {
            GetRoomHorizontal(bossPosition, out float minX, out float maxX);
            float y = bossPosition.y;

            var root = new GameObject("BossExitGate");
            var entry = CreateDoor(root.transform, "EntryDoor", new Vector3(minX + 1f, y, 0f), true);
            var exit = CreateDoor(root.transform, "ExitDoor", new Vector3(maxX - 1f, y, 0f), false);

            var zoneGo = new GameObject("SceneTransitionZone");
            zoneGo.transform.SetParent(root.transform, true);
            zoneGo.transform.position = new Vector3(maxX - 0.25f, y, 0f);
            zoneGo.AddComponent<NetworkObject>();
            var zoneCol = zoneGo.AddComponent<BoxCollider2D>();
            zoneCol.isTrigger = true;
            zoneCol.size = new Vector2(2f, 4f);
            var zone = zoneGo.AddComponent<RoomTransitionZone>();
            SetString(zone, "nextSceneName", nextScene);
            SetBool(zone, "startActive", false);

            var gateGo = new GameObject("BossGateController");
            gateGo.transform.SetParent(root.transform, true);
            gateGo.transform.position = bossPosition;
            gateGo.AddComponent<NetworkObject>();
            var gate = gateGo.AddComponent<BossGateController>();
            SetRef(gate, "entryDoor", entry);
            SetRef(gate, "exitDoor", exit);
            SetRef(gate, "exitZone", zone);
            return gate;
        }

        private static Door CreateDoor(Transform parent, string name, Vector3 position, bool startOpen)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.AddComponent<NetworkObject>();
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 4f);
            var door = go.AddComponent<Door>();
            SetRef(door, "blockingCollider", col);
            SetBool(door, "startOpen", startOpen);
            return door;
        }

        private static void GetRoomHorizontal(Vector3 position, out float minX, out float maxX)
        {
            Collider2D best = null;
            float area = float.MaxValue;
            foreach (var zone in UnityEngine.Object.FindObjectsByType<CameraBoundsZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var col = zone.GetComponent<Collider2D>();
                if (col == null || !col.bounds.Contains(position)) continue;
                float candidate = col.bounds.size.x * col.bounds.size.y;
                if (candidate < area) { area = candidate; best = col; }
            }
            minX = best != null ? best.bounds.min.x : position.x - 12f;
            maxX = best != null ? best.bounds.max.x : position.x + 12f;
        }

        private static void MigrateMap4Puzzle()
        {
            var puzzle = UnityEngine.Object.FindFirstObjectByType<CoopSequentialLeverPuzzle>(FindObjectsInactive.Include);
            if (puzzle == null) { Debug.LogWarning("[RemainingMaps] Map 4 không có CoopSequentialLeverPuzzle."); return; }

            var plates = new List<PuzzlePlate>();
            var doors = new List<Door>();
            for (int i = 0; i < 2; i++)
            {
                // --- Plate ---
                string oldName = $"Lever_{i}";
                string newName = $"Plate_{i}";
                var go = FindChildByName(puzzle.transform.root, newName) ?? FindChildByName(puzzle.transform.root, oldName);
                if (go == null) { Debug.LogError($"[RemainingMaps] Map 4 thiếu {oldName}."); continue; }

                go.name = newName;
                go.layer = 0;
                var lever = go.GetComponent<Lever>();
                if (lever != null) UnityEngine.Object.DestroyImmediate(lever);
                var col = go.GetComponent<BoxCollider2D>() ?? go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1.2f, 0.4f);
                var plate = go.GetComponent<PuzzlePlate>() ?? go.AddComponent<PuzzlePlate>();
                plates.Add(plate);

                // --- Door (khớp index với plate) ---
                string doorName = $"Door_{i}";
                var doorGo = FindChildByName(puzzle.transform.root, doorName);
                if (doorGo == null)
                {
                    // Tạo door mới cạnh plate (offset sang phải 3 unit)
                    doorGo = new GameObject(doorName);
                    doorGo.transform.SetParent(puzzle.transform, false);
                    doorGo.transform.localPosition = go.transform.localPosition + new Vector3(3f, 1.5f, 0f);
                    doorGo.AddComponent<NetworkObject>();
                    var doorCol = doorGo.AddComponent<BoxCollider2D>();
                    doorCol.size = new Vector2(1f, 3f);
                    doorCol.isTrigger = false;
                    var door = doorGo.AddComponent<Door>();
                    SetRef(door, "blockingCollider", doorCol);
                    SetBool(door, "startOpen", false);
                    doors.Add(door);
                    Debug.Log($"[RemainingMaps] Map 4: tạo {doorName} mới (cần chỉnh vị trí trong scene).");
                }
                else
                {
                    var door = doorGo.GetComponent<Door>();
                    if (door == null)
                    {
                        doorGo.AddComponent<NetworkObject>();
                        var doorCol = doorGo.GetComponent<BoxCollider2D>() ?? doorGo.AddComponent<BoxCollider2D>();
                        doorCol.size = new Vector2(1f, 3f);
                        doorCol.isTrigger = false;
                        door = doorGo.AddComponent<Door>();
                        SetRef(door, "blockingCollider", doorCol);
                        SetBool(door, "startOpen", false);
                    }
                    doors.Add(door);
                }
            }

            SetArray(puzzle, "plates", plates.ToArray());
            SetArray(puzzle, "doors", doors.ToArray());
            Debug.Log($"[RemainingMaps] Map 4: plates={plates.Count}, doors={doors.Count} đã gán vào CoopSequentialLeverPuzzle.");
        }

        private static GameObject FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static void EnsureBuildScenes()
        {
            var paths = new HashSet<string>();
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in scenes) paths.Add(scene.path);

            foreach (var map in Maps)
            {
                string path = SceneDir + map.Scene + ".unity";
                if (paths.Add(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool HasLines(DialogueSO dialogue) =>
            dialogue != null && dialogue.lines != null && dialogue.lines.Length > 0;

        private static T GetRef<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object
        {
            var prop = new SerializedObject(target).FindProperty(field);
            return prop != null ? prop.objectReferenceValue as T : null;
        }

        private static void SetRef(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[RemainingMaps] Thiếu field '{field}' trên {target.GetType().Name}."); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(UnityEngine.Object target, string field, T[] values) where T : UnityEngine.Object
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[RemainingMaps] Thiếu array '{field}'."); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
