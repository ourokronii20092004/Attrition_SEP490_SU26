using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo nhanh 1 vùng CHUYỂN SCENE (RoomTransitionZone) — kéo player sang Map kế tiếp.
    /// Menu: Tools/Attrition/Create Scene Transition Zone
    /// Sau khi chạy: chỉnh vị trí + kích thước BoxCollider ở rìa map, điền tên scene đích (Next Scene
    /// Name) khớp Build Settings, rồi SAVE scene để Fusion bake NetworkObject.
    /// startActive=true: đi qua là chuyển ngay. Để FALSE nếu vùng chỉ mở sau khi đánh boss (gọi Activate()).
    /// </summary>
    public static class SceneTransitionZoneSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Scene Transition Zone")]
        public static void CreateZone()
        {
            var go = new GameObject("SceneTransitionZone");
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Transition Zone");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) go.transform.position = sv.pivot;

            go.AddComponent<NetworkObject>();

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2f, 4f);
            col.isTrigger = true;

            var zone = go.AddComponent<RoomTransitionZone>();
            SetPrivate(zone, "startActive", true);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Attrition] Đã tạo Scene Transition Zone. Đặt ở rìa map, chỉnh BoxCollider, điền " +
                      "'Next Scene Name' (khớp Build Settings), SAVE scene để Fusion bake NetworkObject. " +
                      "startActive=false nếu vùng chỉ mở sau khi đánh boss (gọi Activate()).");
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Attrition] Field '{field}' không tìm thấy trên {target.GetType().Name}"); return; }

            switch (value)
            {
                case bool b: prop.boolValue = b; break;
                case int n: prop.intValue = n; break;
                case float f: prop.floatValue = f; break;
                case string s: prop.stringValue = s; break;
                case Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
