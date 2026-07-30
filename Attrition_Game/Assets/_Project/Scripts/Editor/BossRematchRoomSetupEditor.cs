#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Attrition.Controllers;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Dựng ROOM BOSS ĐÁNH LẠI trong scene đang mở:
    ///   • Map 5 → 3 room nhỏ chứa boss 2 (Druid) / 3 (Elf) / 4 (DemonKin) + cổng vào phòng boss cuối.
    ///   • Map 4 → 1 room chứa boss 1 (SeveredFang).
    ///
    /// KHÔNG CONFLICT VỚI BOSS Ở SCENE KHÁC — 3 điều đảm bảo:
    ///  1. Fusion spawn theo SCENE: mỗi scene có NetworkRunner/scene riêng, boss ở Map 2 và bản đánh lại ở
    ///     Map 5 là hai NetworkObject KHÁC NHAU, không bao giờ tồn tại cùng lúc trong 1 scene.
    ///  2. `isRematchBoss = true` → KHÔNG gọi `NotifyEnemyKilled`, nên hạ bản đánh lại KHÔNG hoàn thành
    ///     nhầm quest boss của map gốc (chúng dùng chung enemyId druid/elf/demon_kin).
    ///  3. Cũng vì cờ đó → KHÔNG rơi/thưởng vật phẩm, nên accessory tiến trình không bị trao 2 lần.
    ///
    /// `waitForTrigger = true` trên AI boss: mỗi boss đứng im tới khi player vào room kích hoạt → vào room
    /// nào đánh trước cũng được, không bị cả 3 xông ra cùng lúc.
    ///
    /// Menu: Tools/Attrition/World/Setup Boss Rematch Room (scene dang mo)
    /// Idempotent: đã có "BossRematchRoom" trong scene thì bỏ qua.
    /// </summary>
    public static class BossRematchRoomSetupEditor
    {
        // GUID prefab boss (đã kiểm trong Prefabs/Enemy).
        private const string SeveredFangGuid = "cc54e814d890df54ab76e79ad23d2a6c";
        private const string DruidGuid = "ca8c89668e85ae845b8fe71aaeefe563";
        private const string ElfGuid = "c4e2d1b6f8035b2c0d9e7f605b4c3d21";
        private const string DemonKinGuid = "b3d1c0a5e7f24a1b9c8d6e5f4a3b2c10";

        /// <summary>Khoảng cách giữa 2 room nhỏ (units) khi tool đặt sẵn.</summary>
        private const float RoomSpacing = 40f;

        [MenuItem("Tools/Attrition/World/Setup Boss Rematch Room (scene dang mo)")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                EditorUtility.DisplayDialog("Boss Rematch Room", "Mở scene Map 4 hoặc Map 5 trước đã.", "OK");
                return;
            }

            bool isMap5 = scene.name.Contains("Map 5");
            bool isMap4 = scene.name.Contains("Map 4");
            if (!isMap5 && !isMap4)
            {
                EditorUtility.DisplayDialog("Boss Rematch Room",
                    $"Scene '{scene.name}' không phải Map 4/Map 5. Tool chỉ dựng cho 2 map đó.", "OK");
                return;
            }

            if (GameObject.Find("BossRematchRoom") != null)
            {
                EditorUtility.DisplayDialog("Boss Rematch Room",
                    $"Scene '{scene.name}' đã có 'BossRematchRoom' — bỏ qua. Xoá nó nếu muốn dựng lại.", "OK");
                return;
            }

            var root = new GameObject("BossRematchRoom");
            Undo.RegisterCreatedObjectUndo(root, "Setup Boss Rematch Room");

            var sv = SceneView.lastActiveSceneView;
            Vector3 origin = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            var bosses = new List<EnemyController>();
            var report = new System.Text.StringBuilder();

            if (isMap5)
            {
                bosses.Add(PlaceBoss(DruidGuid, "Rematch_Druid_Boss2", root, origin + new Vector3(-RoomSpacing, 0f, 0f), report));
                bosses.Add(PlaceBoss(ElfGuid, "Rematch_Elf_Boss3", root, origin, report));
                bosses.Add(PlaceBoss(DemonKinGuid, "Rematch_DemonKin_Boss4", root, origin + new Vector3(RoomSpacing, 0f, 0f), report));
            }
            else
            {
                bosses.Add(PlaceBoss(SeveredFangGuid, "Rematch_SeveredFang_Boss1", root, origin, report));
            }

            // ── Cổng vào phòng boss cuối (chỉ Map 5 cần: mở khi hạ đủ 3 boss) ──
            if (isMap5)
            {
                var gateGo = new GameObject("FinalBossGate");
                gateGo.transform.SetParent(root.transform);
                gateGo.transform.position = origin + new Vector3(0f, -6f, 0f);
                gateGo.AddComponent<NetworkObject>();

                // Cửa: collider RẮN chặn đường + hình tạm.
                var door = gateGo.AddComponent<Door>();
                var col = gateGo.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1.5f, 5f);
                col.isTrigger = false;
                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer >= 0) gateGo.layer = groundLayer;

                var visual = new GameObject("DoorVisual");
                visual.transform.SetParent(gateGo.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.35f, 0.2f, 0.45f);
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(1.5f, 5f);
                sr.sortingOrder = 6;

                SetPrivate(door, "blockingCollider", col);
                SetPrivate(door, "doorVisual", visual);

                var gate = gateGo.AddComponent<BossRematchGate>();
                SetBossArray(gate, "requiredBosses", bosses);
                SetPrivate(gate, "gateDoor", door);

                report.AppendLine("  FinalBossGate: mở khi hạ đủ 3 boss (thứ tự tuỳ ý).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;

            Debug.Log($"[BossRematchRoom] {scene.name}: đã dựng trong 'BossRematchRoom'.\n" + report +
                      "\nĐẢM BẢO KHÔNG CONFLICT VỚI BOSS Ở MAP GỐC:\n" +
                      "• isRematchBoss = true → KHÔNG rơi vật phẩm, KHÔNG báo quest (dùng chung enemyId với\n" +
                      "  boss gốc nên nếu báo sẽ hoàn thành nhầm quest map kia + trao accessory lần 2).\n" +
                      "• waitForTrigger = true → boss đứng im tới khi player vào room, đánh room nào trước cũng được.\n\n" +
                      "VIỆC CÒN LẠI:\n" +
                      "1. Kéo từng boss + cổng tới đúng room trong scene (tool xếp tạm cách nhau " + RoomSpacing + " units).\n" +
                      "2. Mỗi room nên có CameraBoundsZone riêng (skill boss + leash elite đọc vùng này).\n" +
                      "3. Đặt BossEncounterTrigger cho từng boss nếu muốn có thoại/khoá cửa riêng.\n" +
                      "4. SAVE scene để Fusion bake NetworkObject.");
        }

        /// <summary>Đặt 1 boss bản ĐÁNH LẠI: bật isRematchBoss + waitForTrigger, tắt loot.</summary>
        private static EnemyController PlaceBoss(string prefabGuid, string name, GameObject parent,
                                                 Vector3 pos, System.Text.StringBuilder report)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[BossRematchRoom] Không load được prefab boss (guid {prefabGuid}).");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(go, "Place Rematch Boss");

            var ctrl = go.GetComponent<EnemyController>();
            if (ctrl == null)
            {
                Debug.LogWarning($"[BossRematchRoom] {name}: prefab chưa có EnemyController — " +
                                 "chạy 'Setup Boss Moveset' / 'Setup Boss Prefabs' trước.");
                report.AppendLine($"  {name}: THIẾU EnemyController");
                return null;
            }

            // Cờ chính: không loot, không đụng quest của boss gốc.
            SetBool(ctrl, "isRematchBoss", true);

            // Xoá luôn danh sách loot trên bản đánh lại cho chắc (GrantLoot đã bị bỏ qua, nhưng để trống
            // thì Inspector cũng thể hiện rõ ý đồ "boss này không thưởng gì").
            ClearStringArray(ctrl, "lootItemIds");

            // Boss chờ trigger → vào room nào đánh trước cũng được.
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb is not Attrition.Core.IBossEncounter) continue;
                SetBool(mb, "waitForTrigger", true);
            }

            report.AppendLine($"  {name}: isRematchBoss = true, loot đã xoá, waitForTrigger = true");
            return ctrl;
        }

        private static void SetBossArray(Object target, string field, List<EnemyController> list)
        {
            var so = new SerializedObject(target);
            var arr = so.FindProperty(field);
            if (arr == null) { Debug.LogWarning($"[BossRematchRoom] Thiếu field '{field}'."); return; }

            var valid = new List<EnemyController>();
            foreach (var b in list) if (b != null) valid.Add(b);

            arr.arraySize = valid.Count;
            for (int i = 0; i < valid.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = valid[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivate(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[BossRematchRoom] Thiếu field '{field}' trên {target.GetType().Name}."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;   // AI không có field này → bỏ qua, không phải lỗi
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClearStringArray(Object target, string field)
        {
            var so = new SerializedObject(target);
            var arr = so.FindProperty(field);
            if (arr == null) return;
            arr.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
