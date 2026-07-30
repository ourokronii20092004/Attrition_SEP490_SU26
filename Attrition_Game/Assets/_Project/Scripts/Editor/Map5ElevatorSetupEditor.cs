#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Dựng ĐẦY ĐỦ 12 thang máy đã có sẵn trong `Castle - Map 5` (`Elevator_01..12`): biến mỗi Tilemap
    /// thành bệ đứng được + tạo cần gạt riêng cho từng thang.
    ///
    /// VÌ SAO CẦN: 12 object `Elevator_01..12` trong scene hiện CHỈ có Transform + Tilemap +
    /// TilemapRenderer — không Rigidbody2D, không Collider2D, không NetworkObject, không script
    /// `Elevator`, và KHÔNG có cần gạt nào. Nghĩa là chúng chỉ là hình vẽ: player rơi xuyên qua.
    ///
    /// Tool gắn cho mỗi thang:
    ///   • `TilemapCollider2D` (KHÔNG trigger) + layer `Ground` → player ĐỨNG được, CheckGround nhận là đất.
    ///   • `Rigidbody2D` Kinematic → bệ đẩy player theo khi chạy (Elevator.MovePosition).
    ///   • `NetworkObject` + `Elevator` với danh sách điểm dừng.
    ///   • 1 `Lever` (cần gạt) đặt cạnh điểm dừng ĐẦU, layer `Enemy` để đòn đánh của player quét trúng.
    ///
    /// ĐIỂM DỪNG: mặc định 2 điểm (chỗ đặt → lên cao `DefaultRise`). Riêng **thang 12 có 3 điểm dừng**
    /// theo yêu cầu — xem `ThreeStopElevator`. Sau khi chạy tool, chỉnh `stopOffsets` trong Inspector cho
    /// khớp địa hình thật của từng thang (tool không đoán được độ cao mỗi trục thang).
    ///
    /// Menu: Tools/Attrition/World/Setup Map 5 Elevators (12 thang + can gat)
    /// Idempotent: chạy lại không thêm trùng component, không tạo trùng cần gạt.
    /// </summary>
    public static class Map5ElevatorSetupEditor
    {
        private const string ScenePath = "Assets/_Project/Scenes/Castle - Map 5.unity";

        /// <summary>Tên thang có 3 điểm dừng (yêu cầu user).</summary>
        private const string ThreeStopElevator = "Elevator_12";

        /// <summary>Độ cao đi lên mặc định (units) cho thang 2 điểm dừng.</summary>
        private const float DefaultRise = 8f;

        /// <summary>Thang 3 tầng: tầng giữa + tầng trên.</summary>
        private const float MidRise = 6f;
        private const float TopRise = 12f;

        /// <summary>Cần gạt đặt lệch sang bên bệ bao nhiêu unit.</summary>
        private const float LeverOffsetX = 3.2f;

        [MenuItem("Tools/Attrition/World/Setup Map 5 Elevators (12 thang + can gat)")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!System.IO.File.Exists(ScenePath))
                {
                    Debug.LogError($"[Map5Elevator] Không thấy scene: {ScenePath}");
                    return;
                }
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            var done = new List<string>();
            var report = new System.Text.StringBuilder();

            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (tm == null) continue;
                string name = tm.gameObject.name;
                if (!name.StartsWith("Elevator_")) continue;

                var go = tm.gameObject;

                // ── Collider: theo hình tile, RẮN (không trigger) để đứng lên được ──
                var col = go.GetComponent<TilemapCollider2D>();
                if (col == null) col = Undo.AddComponent<TilemapCollider2D>(go);
                if (col.isTrigger)
                {
                    Undo.RecordObject(col, "Elevator collider solid");
                    col.isTrigger = false;
                }

                // ── Layer Ground: để PlayerController.CheckGround nhận ra là mặt đất ──
                if (groundLayer >= 0 && go.layer != groundLayer)
                {
                    Undo.RecordObject(go, "Elevator layer Ground");
                    go.layer = groundLayer;
                }

                // ── Rigidbody2D Kinematic: Elevator dùng MovePosition để đẩy player theo bệ ──
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb == null) rb = Undo.AddComponent<Rigidbody2D>(go);
                if (rb.bodyType != RigidbodyType2D.Kinematic)
                {
                    Undo.RecordObject(rb, "Elevator kinematic");
                    rb.bodyType = RigidbodyType2D.Kinematic;
                }

                // ── Fusion: NetworkBehaviour bắt buộc có NetworkObject ──
                if (go.GetComponent<NetworkObject>() == null) Undo.AddComponent<NetworkObject>(go);

                // ── Script Elevator + điểm dừng ──
                var elevator = go.GetComponent<Elevator>();
                if (elevator == null) elevator = Undo.AddComponent<Elevator>(go);

                bool threeStop = name == ThreeStopElevator;
                var stops = threeStop
                    ? new[] { Vector2.zero, new Vector2(0f, MidRise), new Vector2(0f, TopRise) }
                    : new[] { Vector2.zero, new Vector2(0f, DefaultRise) };
                SetStopOffsets(elevator, stops);

                // ── Cần gạt riêng cho thang này ──
                string leverName = $"Lever_{name.Substring("Elevator_".Length)}";
                bool leverCreated = EnsureLever(leverName, go, elevator, enemyLayer);

                done.Add(name);
                report.AppendLine($"  {name,-12} stops={stops.Length}"
                                  + (threeStop ? " (3 TANG)" : "")
                                  + (leverCreated ? $"  + {leverName} (moi)" : $"  + {leverName} (da co)"));
            }

            if (done.Count == 0)
            {
                Debug.LogWarning("[Map5Elevator] Không thấy Tilemap nào tên 'Elevator_*' trong scene.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Map5Elevator] Xong {done.Count} thang máy:\n" + report +
                      "\nVIỆC CÒN LẠI (tool không đoán được địa hình):\n" +
                      "• Chỉnh 'stopOffsets' từng thang cho khớp trục thang thật — Gizmo vẽ đường đi + các\n" +
                      "  điểm dừng ngay trong Scene view khi chọn thang.\n" +
                      $"• Thang {ThreeStopElevator} đã có 3 điểm dừng ({MidRise} và {TopRise}) — sửa lại nếu 3 tầng ở độ cao khác.\n" +
                      "• Kéo cần gạt Lever_* tới chỗ player với tới được, gán sprite cho nó.\n" +
                      "• SAVE scene lần nữa nếu có sửa tay (Fusion cần bake NetworkObject).");
        }

        /// <summary>
        /// Tạo cần gạt cho 1 thang nếu chưa có. Trả về true nếu vừa tạo mới.
        ///
        /// Cần gạt đặt cạnh vị trí GỐC của thang (điểm dừng đầu) — đó là chỗ player đứng lần đầu để gọi
        /// thang. Collider KHÔNG trigger vì `PlayerCombat` quét đòn đánh với `useTriggers = false`; layer
        /// `Enemy` để nằm trong `targetLayers` của đòn đánh.
        /// </summary>
        private static bool EnsureLever(string leverName, GameObject elevatorGo, Elevator elevator, int enemyLayer)
        {
            var existing = GameObject.Find(leverName);
            if (existing != null)
            {
                // Đã có → chỉ đảm bảo còn nối đúng thang (scene có thể đã bị sửa tay).
                var lv = existing.GetComponent<Lever>();
                if (lv != null) SetPrivate(lv, "elevator", elevator);
                return false;
            }

            var go = new GameObject(leverName);
            Undo.RegisterCreatedObjectUndo(go, "Create Elevator Lever");

            // Đặt cạnh bệ, hơi cao lên cho khỏi lún vào sàn.
            go.transform.position = elevatorGo.transform.position + new Vector3(LeverOffsetX, 0.8f, 0f);

            go.AddComponent<NetworkObject>();

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.5f);
            col.isTrigger = false;

            if (enemyLayer >= 0) go.layer = enemyLayer;

            // Hình tạm (khối vàng) — designer thay sprite thật sau.
            var visual = new GameObject("LeverVisual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.7f, 0.6f, 0.2f);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.5f, 1.5f);
            sr.sortingOrder = 5;

            var lever = go.AddComponent<Lever>();
            SetPrivate(lever, "elevator", elevator);
            SetPrivate(lever, "shakeTarget", visual.transform);

            return true;
        }

        /// <summary>Ghi mảng `stopOffsets` (private [SerializeField] Vector2[]) qua SerializedObject.</summary>
        private static void SetStopOffsets(Elevator elevator, Vector2[] stops)
        {
            var so = new SerializedObject(elevator);
            var arr = so.FindProperty("stopOffsets");
            if (arr == null)
            {
                Debug.LogWarning("[Map5Elevator] Elevator không có field 'stopOffsets'.");
                return;
            }

            arr.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
                arr.GetArrayElementAtIndex(i).vector2Value = stops[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivate(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[Map5Elevator] Field '{field}' không thấy trên {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
