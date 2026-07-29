using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// ⚠ LỖI THỜI (2026-07-28) — DÙNG `SceneLinkChainSetupEditor` THAY THẾ.
    /// Tool này chỉ làm 1 cặp Map1↔Map2 và HARDCODE toạ độ; bản mới làm cả chuỗi Map1..Map5 và đọc vị
    /// trí trực tiếp từ scene (`Player_SpawnPoint` + zone đi-tiếp) nên tự khớp khi designer dịch object.
    /// Giữ lại để tham chiếu; menu vẫn chạy được và idempotent, nhưng KHÔNG cần dùng nữa.
    ///
    /// Dựng CỬA NỐI HAI CHIỀU giữa Map 1 và Map 2 — bổ sung đường ĐI NGƯỢC (Map 2 → Map 1) mà trước
    /// đây chưa có (mỗi map chỉ có 1 zone đi tiếp, mở sau khi hạ boss).
    ///
    /// Chạy tool trong TỪNG scene (nó tự nhận scene đang mở):
    ///  - Map 1: thêm `SceneEntryPoint` id "from_map2" (chỗ player hiện ra khi từ Map 2 về) +
    ///           gán `entryPointId = "from_map1"` cho zone đi-tiếp có sẵn.
    ///  - Map 2: thêm `RoomTransitionZone` ĐI NGƯỢC về Map 1 (startActive=TRUE — không cần đánh boss) +
    ///           `SceneEntryPoint` id "from_map1" (chỗ hiện ra khi từ Map 1 sang).
    ///
    /// Menu: Tools/Attrition/Scene Link/[DEPRECATED] Setup Map1 ↔ Map2 (2 chiều)
    /// Chạy lại an toàn (idempotent). SAU KHI CHẠY: kiểm tra vị trí trong Scene view rồi SAVE scene
    /// để Fusion bake NetworkObject của zone mới.
    /// </summary>
    public static class Map1Map2LinkSetupEditor
    {
        private const string Map1Scene = "The Darkest Path - Map 1";
        private const string Map2Scene = "Forest - Map 2";

        // ID điểm vào — phải khớp giữa 2 scene.
        private const string EntryFromMap1 = "from_map1";   // ở Map 2: chỗ hiện ra khi từ Map 1 sang
        private const string EntryFromMap2 = "from_map2";   // ở Map 1: chỗ hiện ra khi từ Map 2 về

        // Toạ độ đã tra từ scene: zone đi-tiếp Map 1 ở world (368.44, 71.68);
        // Player_SpawnPoint của Map 2 ở world (-71.44, 5.27).
        private static readonly Vector3 Map1ExitZonePos = new Vector3(368.44f, 71.68f, 0f);
        private static readonly Vector3 Map2SpawnPos = new Vector3(-71.44f, 5.27f, 0f);

        [MenuItem("Tools/Attrition/Scene Link/[DEPRECATED] Setup Map1 <-> Map2 (2 chieu)")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            string name = scene.name;

            if (name == Map1Scene) SetupMap1();
            else if (name == Map2Scene) SetupMap2();
            else
            {
                EditorUtility.DisplayDialog("Scene Link",
                    $"Scene đang mở là '{name}'.\n\nMở '{Map1Scene}' hoặc '{Map2Scene}' rồi chạy lại " +
                    "(chạy tool ở CẢ HAI scene mới đủ 2 chiều).", "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        //  MAP 1

        private static void SetupMap1()
        {
            // 1) Điểm vào "from_map2": lùi hẳn về BÊN TRONG map, cách zone đi-tiếp 8 units để player
            //    về từ Map 2 KHÔNG đứng đè lên zone → tránh bị hút sang Map 2 ngay lập tức.
            var entryPos = Map1ExitZonePos + new Vector3(-8f, 0f, 0f);
            var entry = EnsureEntryPoint(EntryFromMap2, entryPos);

            // 2) Zone đi-tiếp sẵn có → khai báo điểm vào ở Map 2 để chiều đi cũng ra đúng cửa.
            int patched = 0;
            foreach (var zone in Object.FindObjectsByType<RoomTransitionZone>(FindObjectsSortMode.None))
            {
                if (zone == null) continue;
                var so = new SerializedObject(zone);
                var nextProp = so.FindProperty("nextSceneName");
                if (nextProp == null || nextProp.stringValue != Map2Scene) continue;

                var entryProp = so.FindProperty("entryPointId");
                if (entryProp != null)
                {
                    entryProp.stringValue = EntryFromMap1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    patched++;
                }
            }

            Selection.activeGameObject = entry;
            Debug.Log($"[Attrition] MAP 1 xong: điểm vào '{EntryFromMap2}' tại {entryPos} " +
                      $"(kiểm tra là mặt đất đứng được!) + gán entryPointId cho {patched} zone đi Map 2. " +
                      "SAVE scene.");
        }

        //  MAP 2

        private static void SetupMap2()
        {
            // 1) Điểm vào "from_map1" = ngay cạnh Player_SpawnPoint (chỗ vào map), lệch phải 2 units
            //    để không đè lên zone đi-ngược đặt bên trái.
            var entryPos = Map2SpawnPos + new Vector3(2f, 0f, 0f);
            EnsureEntryPoint(EntryFromMap1, entryPos);

            // 2) Zone ĐI NGƯỢC về Map 1 — đặt bên TRÁI spawn (rìa vào map).
            //    startActive = TRUE: quay về map cũ không cần điều kiện gì.
            var zonePos = Map2SpawnPos + new Vector3(-3f, 0f, 0f);
            var zone = EnsureBackZone(zonePos);

            Selection.activeGameObject = zone;
            Debug.Log($"[Attrition] MAP 2 xong: zone về Map 1 tại {zonePos} + điểm vào " +
                      $"'{EntryFromMap1}' tại {entryPos}. KIỂM TRA vị trí zone nằm ở rìa map (mặt đất " +
                      "đứng được, không lơ lửng) rồi SAVE scene để Fusion bake NetworkObject.");
        }

        //  HELPERS

        /// <summary>Tạo (hoặc tìm lại) 1 SceneEntryPoint theo id. Idempotent.</summary>
        private static GameObject EnsureEntryPoint(string id, Vector3 pos)
        {
            foreach (var ep in Object.FindObjectsByType<SceneEntryPoint>(FindObjectsSortMode.None))
            {
                if (ep != null && ep.EntryId == id)
                {
                    Debug.Log($"[Attrition] Điểm vào '{id}' đã tồn tại — giữ nguyên vị trí hiện tại.");
                    return ep.gameObject;
                }
            }

            var go = new GameObject($"SceneEntryPoint_{id}");
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Entry Point");
            go.transform.position = pos;

            var comp = go.AddComponent<SceneEntryPoint>();
            var so = new SerializedObject(comp);
            so.FindProperty("entryId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Tạo (hoặc tìm lại) zone đi NGƯỢC về Map 1. Idempotent.</summary>
        private static GameObject EnsureBackZone(Vector3 pos)
        {
            foreach (var z in Object.FindObjectsByType<RoomTransitionZone>(FindObjectsSortMode.None))
            {
                if (z == null) continue;
                var check = new SerializedObject(z);
                var np = check.FindProperty("nextSceneName");
                if (np != null && np.stringValue == Map1Scene)
                {
                    Debug.Log("[Attrition] Zone về Map 1 đã tồn tại — giữ nguyên.");
                    return z.gameObject;
                }
            }

            var go = new GameObject("SceneTransitionZone_BackToMap1");
            Undo.RegisterCreatedObjectUndo(go, "Create Back Transition Zone");
            go.transform.position = pos;

            go.AddComponent<NetworkObject>();

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2f, 4f);
            col.isTrigger = true;

            var zone = go.AddComponent<RoomTransitionZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("nextSceneName").stringValue = Map1Scene;
            so.FindProperty("entryPointId").stringValue = EntryFromMap2;
            so.FindProperty("startActive").boolValue = true;   // về map cũ: mở sẵn
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }
    }
}
