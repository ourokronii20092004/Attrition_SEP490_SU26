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
    /// thành bệ đứng được và tự chạy khi đủ player đứng lên (solo 1, coop cả 2).
    ///
    /// VÌ SAO CẦN: 12 object `Elevator_01..12` trong scene ban đầu CHỈ có Transform + Tilemap +
    /// TilemapRenderer — không Rigidbody2D, Collider2D, NetworkObject hay script `Elevator`.
    ///
    /// Tool gắn cho mỗi thang:
    ///   • `TilemapCollider2D` (KHÔNG trigger) + layer `Ground` → player ĐỨNG được, CheckGround nhận là đất.
    ///   • `Rigidbody2D` Kinematic → bệ đẩy player theo khi chạy (Elevator.MovePosition).
    ///   • `NetworkObject` + `Elevator` với danh sách điểm dừng và tự nhận biết player đứng trên bệ.
    ///   • Xoá `Lever_*` cũ nếu từng chạy bản tool dùng cần gạt.
    ///
    /// ĐIỂM DỪNG: mặc định 2 điểm (chỗ đặt → lên cao `DefaultRise`). Riêng **thang 12 có 3 điểm dừng**
    /// theo yêu cầu — xem `ThreeStopElevator`. Sau khi chạy tool, chỉnh `stopOffsets` trong Inspector cho
    /// khớp địa hình thật của từng thang (tool không đoán được độ cao mỗi trục thang).
    ///
    /// Menu: Tools/Attrition/World/Setup Map 5 Elevators (12 thang tu dong)
    /// Idempotent: chạy lại không thêm trùng component; lever cũ được xoá.
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

        [MenuItem("Tools/Attrition/World/Setup Map 5 Elevators (12 thang tu dong)")]
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

                // Không cần lever: Elevator tự đếm player đứng trên bệ (solo=1, coop=2) rồi chạy.
                string leverName = $"Lever_{name.Substring("Elevator_".Length)}";
                var obsoleteLever = GameObject.Find(leverName);
                if (obsoleteLever != null) Undo.DestroyObjectImmediate(obsoleteLever);

                done.Add(name);
                report.AppendLine($"  {name,-12} stops={stops.Length}"
                                  + (threeStop ? " (3 TANG)" : "")
                                  + "  auto-run");
            }

            if (done.Count == 0)
            {
                Debug.LogWarning("[Map5Elevator] Không thấy Tilemap nào tên 'Elevator_*' trong scene.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Map5Elevator] Xong {done.Count} thang máy tự chạy:\n" + report +
                      "\nVIỆC CÒN LẠI (tool không đoán được địa hình):\n" +
                      "• Chỉnh 'stopOffsets' từng thang cho khớp trục thang thật — Gizmo vẽ đường đi + các\n" +
                      "  điểm dừng ngay trong Scene view khi chọn thang.\n" +
                      $"• Thang {ThreeStopElevator} đã có 3 điểm dừng ({MidRise} và {TopRise}) — sửa lại nếu 3 tầng ở độ cao khác.\n" +
                      "• Solo: 1 player đứng lên; coop: cả 2 player còn sống đứng lên thì thang tự chạy.\n" +
                      "• SAVE scene lần nữa nếu có sửa tay (Fusion cần bake NetworkObject).");
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

    }
}
#endif
