#if UNITY_EDITOR
using System.Collections.Generic;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Attrition.Controllers;
using Attrition.Data;
using Attrition.Gameplay.Environment;
using Attrition.Gameplay.NPC;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>Nâng cấp BossRematchRoom đã có: đúng thứ tự boss, 3 cửa lock-in, Autumn thoại, cửa nổi trên nền.</summary>
    public static class Map5RematchDoorsSetupEditor
    {
        private const string SceneName = "Castle - Map 5";
        private const string RootName = "BossRematchRoom";
        private const string DoorsName = "RematchRoomDoors";
        private const string DialoguePath = "Assets/_Project/Data/NPC/FairyDialogue/Dlg_M5_Room4_Autumn_Explain.asset";

        [MenuItem("Tools/Attrition/World/Setup Map 5 Room 4 Doors")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != SceneName)
            {
                EditorUtility.DisplayDialog("Room 4 Doors", $"Mở scene '{SceneName}' trước.", "OK");
                return;
            }

            var root = GameObject.Find(RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Room 4 Doors",
                    $"Scene chưa có '{RootName}'. Chạy Setup Boss Rematch Room trước.", "OK");
                return;
            }

            var druid = FindBoss("Rematch_Druid_Boss2");
            var elf = FindBoss("Rematch_Elf_Boss3");
            var demonKin = FindBoss("Rematch_DemonKin_Boss4");
            if (druid == null || elf == null || demonKin == null)
            {
                EditorUtility.DisplayDialog("Room 4 Doors", "Thiếu một trong ba rematch boss.", "OK");
                return;
            }

            // Yêu cầu: trên Boss 2, giữa Boss 3, dưới Boss 4. Scene cũ đang đảo Elf/DemonKin.
            Vector3 elfOld = elf.transform.position;
            Vector3 demonOld = demonKin.transform.position;
            if (elf.transform.position.y < demonKin.transform.position.y)
            {
                Undo.RecordObject(elf.transform, "Move Elf to middle room");
                Undo.RecordObject(demonKin.transform, "Move DemonKin to bottom room");
                elf.transform.position = demonOld;
                demonKin.transform.position = elfOld;
            }

            // Không tạo trùng. Xoá nhóm cửa cũ do chính tool này tạo để có thể chạy lại sau khi đổi layout.
            var oldDoors = root.transform.Find(DoorsName);
            if (oldDoors != null) Undo.DestroyObjectImmediate(oldDoors.gameObject);
            var doors = new GameObject(DoorsName);
            Undo.RegisterCreatedObjectUndo(doors, "Create rematch room doors");
            doors.transform.SetParent(root.transform, false);

            float elevatorX = FindElevator12X();
            SetupRoom(druid, doors.transform, elevatorX, "Boss2_Druid");
            SetupRoom(elf, doors.transform, elevatorX, "Boss3_Elf");
            SetupRoom(demonKin, doors.transform, elevatorX, "Boss4_DemonKin");

            FixFinalGateVisual();
            SetupAutumnDialogue();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = doors;
            Debug.Log("[Room4Doors] Xong: Elf/DemonKin đã đúng tầng; 3 cửa mở sẵn, khoá khi trận bắt đầu, " +
                      "mở lại khi boss chết/wipe; FinalBossGate giữ điều kiện đủ 3 boss; Autumn chỉ thoại. " +
                      "Hãy kiểm tra vị trí cửa trong Scene view rồi SAVE scene để Fusion bake NetworkObject.");
        }

        private static EnemyController FindBoss(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<EnemyController>() : null;
        }

        private static void SetupRoom(EnemyController boss, Transform parent, float elevatorX, string suffix)
        {
            var ai = FindBossAI(boss);
            if (ai == null)
            {
                Debug.LogError($"[Room4Doors] {boss.name}: không tìm thấy IBossEncounter.");
                return;
            }

            GetRoomHorizontal(boss.transform.position, out float minX, out float maxX);
            float leftDistance = Mathf.Abs(elevatorX - minX);
            float rightDistance = Mathf.Abs(elevatorX - maxX);
            float doorX = leftDistance <= rightDistance ? minX + 0.75f : maxX - 0.75f;
            Vector3 doorPos = new Vector3(doorX, boss.transform.position.y, 0f);

            var group = new GameObject($"Room_{suffix}");
            group.transform.SetParent(parent, false);

            var door = CreateDoor(group.transform, $"EntryDoor_{suffix}", doorPos);

            var triggerGo = new GameObject($"BossTrigger_{suffix}");
            triggerGo.transform.SetParent(group.transform, true);
            triggerGo.transform.position = Vector3.Lerp(doorPos, boss.transform.position, 0.35f);
            var triggerCol = triggerGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector2(4f, 5f);
            var trigger = triggerGo.AddComponent<BossEncounterTrigger>();
            trigger.boss = ai;

            var gateGo = new GameObject($"BossGate_{suffix}");
            gateGo.transform.SetParent(group.transform, true);
            gateGo.transform.position = boss.transform.position;
            gateGo.AddComponent<NetworkObject>();
            var gate = gateGo.AddComponent<RematchBossDoor>();
            SetRef(gate, "boss", boss);
            SetRef(gate, "bossAI", ai);
            SetRef(gate, "entryDoor", door);
        }

        private static MonoBehaviour FindBossAI(EnemyController boss)
        {
            foreach (var mb in boss.GetComponents<MonoBehaviour>())
                if (mb is Attrition.Core.IBossEncounter) return mb;
            return null;
        }

        private static Door CreateDoor(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.AddComponent<NetworkObject>();
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) go.layer = ground;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1.5f, 5f);

            var visual = CreateVisual(go.transform);
            var door = go.AddComponent<Door>();
            SetBool(door, "startOpen", true);
            SetRef(door, "blockingCollider", col);
            SetRef(door, "doorVisual", visual);
            return door;
        }

        private static GameObject CreateVisual(Transform parent)
        {
            var go = new GameObject("DoorVisual");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.5f, 5f);
            sr.color = new Color(0.35f, 0.2f, 0.45f, 1f);
            sr.sortingOrder = 20;
            return go;
        }

        private static void GetRoomHorizontal(Vector3 position, out float minX, out float maxX)
        {
            Collider2D best = null;
            float area = float.MaxValue;
            foreach (var zone in Object.FindObjectsByType<CameraBoundsZone>(FindObjectsInactive.Include,
                                                                            FindObjectsSortMode.None))
            {
                var col = zone.GetComponent<Collider2D>();
                if (col == null || !col.bounds.Contains(position)) continue;
                float candidate = col.bounds.size.x * col.bounds.size.y;
                if (candidate < area) { area = candidate; best = col; }
            }
            minX = best != null ? best.bounds.min.x : position.x - 12f;
            maxX = best != null ? best.bounds.max.x : position.x + 12f;
        }

        private static float FindElevator12X()
        {
            var go = GameObject.Find("Elevator_12");
            if (go == null) return 120f;
            var col = go.GetComponent<TilemapCollider2D>();
            return col != null && col.bounds.size.x > 0f ? col.bounds.center.x : 120f;
        }

        private static void FixFinalGateVisual()
        {
            var final = GameObject.Find("FinalBossGate");
            if (final == null) return;
            var sr = final.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) return;
            Undo.RecordObject(sr, "Fix FinalBossGate visual");
            if (sr.sprite == null)
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.sortingOrder = 20;
        }

        private static void SetupAutumnDialogue()
        {
            var go = GameObject.Find("Autumn_Fairy_Encourage") ?? GameObject.Find("Room4_Autumn_Fairy");
            if (go == null) { Debug.LogWarning("[Room4Doors] Không thấy Autumn Fairy trong Room 4."); return; }
            go.name = "Room4_Autumn_Fairy";
            var npc = go.GetComponent<NetworkNPC>();
            if (npc == null) return;

            var dialogue = LoadOrCreateDialogue();
            var so = new SerializedObject(npc);
            SetProp(so, "npcName", p => p.stringValue = "Autumn Fairy");
            SetProp(so, "idleDialogue", p => p.objectReferenceValue = dialogue);
            SetProp(so, "quest", p => p.objectReferenceValue = null);
            SetProp(so, "extraQuests", p => p.arraySize = 0);
            SetProp(so, "claimForNpc", p => p.objectReferenceValue = null);
            SetProp(so, "turnInObjectiveKey", p => p.stringValue = "");
            SetProp(so, "turnInDialogue", p => p.objectReferenceValue = null);
            SetProp(so, "turnInDoneDialogue", p => p.objectReferenceValue = null);
            SetProp(so, "autoStartQuest", p => p.boolValue = false);
            SetProp(so, "autoFinishWithoutReward", p => p.boolValue = false);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DialogueSO LoadOrCreateDialogue()
        {
            var d = AssetDatabase.LoadAssetAtPath<DialogueSO>(DialoguePath);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<DialogueSO>();
                AssetDatabase.CreateAsset(d, DialoguePath);
            }
            d.lines = new[]
            {
                new DialogueLine { speakerName = "Autumn Fairy", text = "Three fallen guardians wait here: one above, one in this hall, and one below." },
                new DialogueLine { speakerName = "Autumn Fairy", text = "Choose any room first. Its door will seal only while that battle is being fought." },
                new DialogueLine { speakerName = "Autumn Fairy", text = "Defeat all three and the sealed door beside me will open the road onward." },
            };
            EditorUtility.SetDirty(d);
            return d;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Room4Doors] Thiếu field '{field}' trên {target.GetType().Name}."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProp(SerializedObject so, string field, System.Action<SerializedProperty> apply)
        {
            var p = so.FindProperty(field);
            if (p != null) apply(p);
        }
    }
}
#endif
