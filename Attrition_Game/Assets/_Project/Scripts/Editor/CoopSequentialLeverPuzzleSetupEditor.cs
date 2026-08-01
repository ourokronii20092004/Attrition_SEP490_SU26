using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo nhanh puzzle COOP "dẫm plate nối tiếp" 2 chặng: 2 PuzzlePlate + 2 Door + 1 controller.
    /// Menu: Tools/Attrition/Create Coop Sequential Plate Puzzle
    /// Bố trí gợi ý: Plate_0 + Door_0 ở phòng ngoài; Plate_1 + Door_1 nằm SAU Door_0 (P2 chỉ vào được
    /// khi P1 dẫm Plate_0 mở Door_0). Sau khi chạy: đặt lại vị trí, gán sprite, SAVE scene để Fusion bake.
    /// </summary>
    public static class CoopSequentialLeverPuzzleSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Coop Sequential Plate Puzzle")]
        public static void CreatePuzzle()
        {
            var root = new GameObject("CoopSequentialLeverPuzzle");
            Undo.RegisterCreatedObjectUndo(root, "Create Coop Sequential Lever Puzzle");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) root.transform.position = sv.pivot;

            var door0 = CreateDoor(root, "Door_0", new Vector3(3f, 1.5f, 0f));
            var plate0 = CreatePlate(root, "Plate_0", new Vector3(-2f, 0f, 0f));
            var door1 = CreateDoor(root, "Door_1", new Vector3(9f, 1.5f, 0f));
            var plate1 = CreatePlate(root, "Plate_1", new Vector3(6f, 0f, 0f));

            var ctrlGo = new GameObject("Controller");
            ctrlGo.transform.SetParent(root.transform);
            ctrlGo.transform.localPosition = Vector3.zero;
            ctrlGo.AddComponent<NetworkObject>();
            var ctrl = ctrlGo.AddComponent<CoopSequentialLeverPuzzle>();
            SetPrivateArray(ctrl, "plates", new Object[] { plate0, plate1 });
            SetPrivateArray(ctrl, "doors", new Object[] { door0, door1 });

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[Attrition] Đã tạo Coop Sequential Plate Puzzle (2 chặng). Đặt Plate_1/Door_1 SAU " +
                      "Door_0 để P2 vào sau khi P1 mở cửa. Gán sprite, SAVE scene để Fusion bake NetworkObject.");
        }

        private static Door CreateDoor(GameObject root, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            go.transform.localPosition = localPos;
            go.AddComponent<NetworkObject>();
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 3f);
            col.isTrigger = false;
            var visual = CreateVisual(go, "DoorVisual", new Vector2(1f, 3f), new Color(0.4f, 0.25f, 0.15f));
            var door = go.AddComponent<Door>();
            SetPrivate(door, "blockingCollider", col);
            SetPrivate(door, "doorVisual", visual);
            SetPrivate(door, "startOpen", false);
            return door;
        }

        private static PuzzlePlate CreatePlate(GameObject root, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            go.transform.localPosition = localPos;
            go.AddComponent<NetworkObject>();
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 0.4f);
            col.isTrigger = true;
            CreateVisual(go, "PlateVisual", new Vector2(1.2f, 0.25f), new Color(0.7f, 0.6f, 0.2f));
            return go.AddComponent<PuzzlePlate>();
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
                case bool b: prop.boolValue = b; break;
                case Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Attrition] Field '{field}' không thấy trên {target.GetType().Name}"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
