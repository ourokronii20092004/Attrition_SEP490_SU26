using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo nhanh cụm Thang máy + Cần gạt kiểu Hollow Knight (Map 5): 1 Elevator (bệ kinematic
    /// chạy A↔B) + 1 Lever (đánh vào để đổi chiều). Lever đã nối sẵn vào Elevator.
    /// Menu: Tools/Attrition/Create Elevator + Lever (Map 5)
    /// Sau khi chạy: đặt vị trí bệ + cần, chỉnh pointBOffset (độ cao đi lên), gán sprite,
    /// đảm bảo bệ nằm trên groundLayer + Lever ở layer nằm trong targetLayers của PlayerCombat,
    /// rồi SAVE scene để Fusion bake NetworkObject.
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
            SetPrivate(elevator, "pointBOffset", new Vector2(0f, 8f));

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
            Debug.Log("[Attrition] Đã tạo Elevator + Lever. Chỉnh pointBOffset (độ cao/khoảng đi), đặt cần gạt " +
                      "trong tầm với, gán sprite, đảm bảo bệ ở layer 'Ground' và cần ở layer trong targetLayers " +
                      "của PlayerCombat, rồi SAVE scene để Fusion bake NetworkObject.");
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
