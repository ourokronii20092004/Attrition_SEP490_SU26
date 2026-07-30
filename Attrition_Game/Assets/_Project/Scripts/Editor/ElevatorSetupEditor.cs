using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo nhanh cụm Thang máy + Cần gạt kiểu Hollow Knight (Map 5): 1 Elevator (bệ kinematic chạy
    /// qua các điểm dừng) + 1 Lever (đánh vào để đi tới điểm kế tiếp). Lever đã nối sẵn vào Elevator.
    /// Menu: Tools/Attrition/Create Elevator + Lever (Map 5)
    /// Sau khi chạy: đặt vị trí bệ + cần, chỉnh `stopOffsets` (danh sách điểm dừng), gán sprite,
    /// đảm bảo bệ nằm trên groundLayer + Lever ở layer nằm trong targetLayers của PlayerCombat,
    /// rồi SAVE scene để Fusion bake NetworkObject.
    ///
    /// Đã có 12 thang trong Map 5? Dùng `Tools/Attrition/World/Setup Map 5 Elevators` để dựng hàng loạt.
    /// </summary>
    public static class ElevatorSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Elevator + Lever (Map 5)")]
        public static void CreateElevator()
        {
            var root = new GameObject("ElevatorRig");
            Undo.RegisterCreatedObjectUndo(root, "Create Elevator + Lever");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) root.transform.position = sv.pivot;

            // ── Elevator (bệ) ──
            var elevGo = new GameObject("Elevator");
            elevGo.transform.SetParent(root.transform);
            elevGo.transform.localPosition = Vector3.zero;
            elevGo.AddComponent<NetworkObject>();
            var rb = elevGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var elevCol = elevGo.AddComponent<BoxCollider2D>();
            elevCol.size = new Vector2(3f, 0.5f);
            elevCol.isTrigger = false;
            // Bệ phải nằm trên groundLayer để CheckGround của player nhận là mặt đất đứng được.
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) elevGo.layer = groundLayer;
            CreateVisual(elevGo, "ElevatorVisual", new Vector2(3f, 0.5f), new Color(0.3f, 0.5f, 0.6f));
            var elevator = elevGo.AddComponent<Elevator>();

            // Elevator giờ dùng DANH SÁCH điểm dừng (`stopOffsets`) thay cho pointAOffset/pointBOffset —
            // để hỗ trợ thang nhiều tầng (Map 5 thang 12 có 3 điểm dừng). Mặc định 2 điểm: chỗ đặt → +8 cao.
            SetStopOffsets(elevator, new[] { Vector2.zero, new Vector2(0f, 8f) });

            // ── Lever (cần gạt) ──
            var leverGo = new GameObject("Lever");
            leverGo.transform.SetParent(root.transform);
            leverGo.transform.localPosition = new Vector3(3f, 0f, 0f);
            leverGo.AddComponent<NetworkObject>();
            var leverCol = leverGo.AddComponent<BoxCollider2D>();
            leverCol.size = new Vector2(0.8f, 1.5f);
            leverCol.isTrigger = false; // rắn để OverlapCircle của đòn đánh (useTriggers=false) quét trúng
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) leverGo.layer = enemyLayer;
            var leverVisual = CreateVisual(leverGo, "LeverVisual", new Vector2(0.5f, 1.5f), new Color(0.7f, 0.6f, 0.2f));
            var lever = leverGo.AddComponent<Lever>();
            SetPrivate(lever, "elevator", elevator);
            SetPrivate(lever, "shakeTarget", leverVisual.transform);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[Attrition] Đã tạo Elevator + Lever. Chỉnh 'stopOffsets' (danh sách điểm dừng — thêm " +
                      "phần tử thứ 3 nếu muốn thang 3 tầng), đặt cần gạt trong tầm với, gán sprite, đảm bảo bệ " +
                      "ở layer 'Ground' và cần ở layer trong targetLayers của PlayerCombat, rồi SAVE scene để " +
                      "Fusion bake NetworkObject.");
        }

        private static GameObject CreateVisual(GameObject parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.sortingOrder = 5;
            return go;
        }

        /// <summary>
        /// Ghi mảng `stopOffsets` (private [SerializeField] Vector2[]) qua SerializedObject.
        /// `SetPrivate` không dùng được: nó xử lý giá trị đơn lẻ, còn đây là array nên phải set arraySize
        /// rồi ghi từng phần tử.
        /// </summary>
        private static void SetStopOffsets(Elevator elevator, Vector2[] stops)
        {
            var so = new SerializedObject(elevator);
            var arr = so.FindProperty("stopOffsets");
            if (arr == null)
            {
                Debug.LogWarning("[Attrition] Elevator không có field 'stopOffsets'.");
                return;
            }

            arr.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
                arr.GetArrayElementAtIndex(i).vector2Value = stops[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Attrition] Field '{field}' không thấy trên {target.GetType().Name}"); return; }

            switch (value)
            {
                case int n: prop.intValue = n; break;
                case bool b: prop.boolValue = b; break;
                case Vector2 v: prop.vector2Value = v; break;
                case Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
