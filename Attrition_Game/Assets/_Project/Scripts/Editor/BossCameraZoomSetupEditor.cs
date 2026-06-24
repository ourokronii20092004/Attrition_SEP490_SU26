using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo vùng ZOOM camera cho phòng boss và TỰ gán bound + mức zoom vừa khít để KHÔNG lộ ngoài map.
    ///
    /// Cách dùng:
    ///  1. Chọn GameObject có CameraBoundsZone (collider giới hạn phòng boss) trong Hierarchy.
    ///  2. Menu Tools/Attrition/Create Boss Camera Zoom Zone.
    ///     → Tạo 1 GameObject "BossCameraZoomZone" trùng kích thước/vị trí bound đó, gán roomBounds = bound,
    ///       và set zoomedSize = mức lớn nhất vừa khít bound (theo cả cao lẫn ngang).
    ///  Nếu không chọn gì → tạo zone rỗng tại camera, bạn tự gán roomBounds sau.
    /// </summary>
    public static class BossCameraZoomSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Boss Camera Zoom Zone")]
        public static void CreateZoomZone()
        {
            var boundsGo = Selection.activeGameObject;
            Collider2D boundCol = boundsGo != null ? boundsGo.GetComponent<Collider2D>() : null;
            var boundsZone = boundsGo != null ? boundsGo.GetComponent<CameraBoundsZone>() : null;

            var go = new GameObject("BossCameraZoomZone");
            Undo.RegisterCreatedObjectUndo(go, "Create Boss Camera Zoom Zone");

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            float aspect = GetGameAspect();
            float zoom = 9f;

            if (boundCol != null)
            {
                // Đặt zone trùng vùng bound để player vào phòng là zoom.
                go.transform.position = boundCol.bounds.center;
                col.size = new Vector2(boundCol.bounds.size.x, boundCol.bounds.size.y);

                // Mức zoom lớn nhất vừa khít bound (theo cả chiều cao và chiều ngang).
                float maxByHeight = boundCol.bounds.extents.y;
                float maxByWidth = boundCol.bounds.extents.x / aspect;
                zoom = Mathf.Min(maxByHeight, maxByWidth) - 0.1f;
                if (zoom < 1f) zoom = 1f;
            }
            else
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) go.transform.position = sv.pivot;
                col.size = new Vector2(10f, 6f);
            }

            var zoomZone = go.AddComponent<CameraZoomZone>();
            var so = new SerializedObject(zoomZone);
            SetFloat(so, "zoomedSize", zoom);
            SetFloat(so, "aspect", aspect);
            if (boundCol != null) so.FindProperty("roomBounds").objectReferenceValue = boundCol;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            if (boundCol != null)
                Debug.Log($"[Attrition] Đã tạo BossCameraZoomZone khớp bound '{boundsGo.name}'. zoomedSize={zoom:F2} " +
                          $"(vừa khít, không lộ ngoài). aspect={aspect:F3}.");
            else
                Debug.LogWarning("[Attrition] Chưa chọn CameraBoundsZone → tạo zone rỗng. Hãy gán 'roomBounds' " +
                                 "(collider phòng) cho CameraZoomZone để clamp zoom không lộ ngoài map.");
        }

        private static float GetGameAspect()
        {
            // Tỉ lệ màn hình hiện tại; fallback 16:9 nếu chưa hợp lệ.
            if (Screen.height > 0) return (float)Screen.width / Screen.height;
            return 16f / 9f;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }
    }
}
