using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo nhanh "nhiệm vụ 2 nút mở cửa" (coop): 2 PuzzlePlate + 1 Door + 1 CoopPlateDoorController.
    /// Cả 2 player phải đứng đồng thời lên 2 nút thì cửa mới mở.
    /// Menu: Tools/Attrition/Create Coop Two-Button Door
    /// Sau khi chạy: chỉnh vị trí 2 nút (đặt cách xa nhau), cửa, rồi gán Sprite cho đẹp.
    /// LƯU Ý: đây là NetworkObject — cần Bake lại Fusion scene (Fusion sẽ tự bake khi save scene).
    /// </summary>
    public static class CoopDoorSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Coop Two-Button Door")]
        public static void CreateCoopDoor()
        {
            var root = new GameObject("CoopButtonDoor");
            Undo.RegisterCreatedObjectUndo(root, "Create Coop Two-Button Door");

            // Vị trí trước camera nếu có scene view
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) root.transform.position = sv.pivot;

            var doorGo = new GameObject("Door");
            doorGo.transform.SetParent(root.transform);
            doorGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            doorGo.AddComponent<NetworkObject>();
            var blockingCol = doorGo.AddComponent<BoxCollider2D>();
            blockingCol.size = new Vector2(1f, 3f);   // tường chắn dọc
            blockingCol.isTrigger = false;
            var doorVisual = CreateVisualChild(doorGo, "DoorVisual", new Vector2(1f, 3f), new Color(0.4f, 0.25f, 0.15f));
            var door = doorGo.AddComponent<Door>();
            SetPrivate(door, "blockingCollider", blockingCol);
            SetPrivate(door, "doorVisual", doorVisual);
            SetPrivate(door, "startOpen", false);

            var plateA = CreatePlate(root, "Plate_A", new Vector3(-4f, 0f, 0f));
            var plateB = CreatePlate(root, "Plate_B", new Vector3(4f, 0f, 0f));

            var ctrlGo = new GameObject("CoopPlateDoorController");
            ctrlGo.transform.SetParent(root.transform);
            ctrlGo.transform.localPosition = Vector3.zero;
            ctrlGo.AddComponent<NetworkObject>();
            var ctrl = ctrlGo.AddComponent<CoopPlateDoorController>();
            SetPrivate(ctrl, "plates", new[] { plateA, plateB });
            SetPrivate(ctrl, "door", door);
            SetPrivate(ctrl, "requireHold", true);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[Attrition] Đã tạo Coop Two-Button Door. Hãy đặt 2 nút cách xa nhau, gán sprite cửa/nút, " +
                      "rồi SAVE scene để Fusion bake NetworkObject. requireHold=true: rời nút thì cửa đóng lại.");
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
            CreateVisualChild(go, "PlateVisual", new Vector2(1.2f, 0.3f), new Color(0.8f, 0.7f, 0.2f));
            return go.AddComponent<PuzzlePlate>();
        }

        private static GameObject CreateVisualChild(GameObject parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeQuadSprite();
            sr.color = color;
            // Simple + scale: sprite 1x1 không có border nên Sliced render rỗng. Dùng Simple cho chắc.
            sr.drawMode = SpriteDrawMode.Simple;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.sortingOrder = 5;
            return go;
        }

        // Sprite 1x1 trắng (1 unit = 1 pixel) để scale transform ra đúng kích thước units.
        private static Sprite _quad;
        private static Sprite MakeQuadSprite()
        {
            if (_quad != null) return _quad;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _quad = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _quad;
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Attrition] Field '{field}' không tìm thấy trên {target.GetType().Name}"); return; }

            switch (value)
            {
                case bool b: prop.boolValue = b; break;
                case Object o: prop.objectReferenceValue = o; break;
                case System.Array arr:
                    prop.arraySize = arr.Length;
                    for (int i = 0; i < arr.Length; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = (Object)arr.GetValue(i);
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
