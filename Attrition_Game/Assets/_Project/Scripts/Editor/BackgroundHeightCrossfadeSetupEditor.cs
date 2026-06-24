using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo sẵn 1 VÙNG crossfade background theo độ cao (Under mờ dần / Surface rõ dần khi đi lên).
    /// CÁCH DÙNG: chọn 2 object ParallaxBackground theo thứ tự [dưới đất, mặt đất] rồi chạy menu.
    /// Không chọn → tạo vùng với 2 ô trống để tự kéo.
    /// Menu: Tools/Attrition/Create Background Height Crossfade Zone
    /// </summary>
    public static class BackgroundHeightCrossfadeSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Background Height Crossfade Zone")]
        public static void Create()
        {
            var go = new GameObject("BGHeightCrossfadeZone");
            Undo.RegisterCreatedObjectUndo(go, "Create BG Height Crossfade Zone");
            var sv = SceneView.lastActiveSceneView;
            go.transform.position = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(20f, 12f); // cao = khoảng chuyển tiếp dưới→trên; chỉnh cho khớp room

            var fade = go.AddComponent<BackgroundHeightCrossfade>();
            var so = new SerializedObject(fade);

            var sel = Selection.gameObjects;
            if (sel != null && sel.Length >= 2)
            {
                so.FindProperty("backgroundUnder").objectReferenceValue = sel[0];
                so.FindProperty("backgroundSurface").objectReferenceValue = sel[1];
                Debug.Log($"[Attrition] Gán Under={sel[0].name}, Surface={sel[1].name}.");
            }
            else
            {
                Debug.LogWarning("[Attrition] Chưa chọn 2 ParallaxBackground → để TRỐNG 2 ô. " +
                                 "Kéo tay: Background Under = bg dưới đất, Background Surface = bg mặt đất.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
            Debug.Log("[Attrition] Đã tạo BGHeightCrossfadeZone. Đặt collider phủ KHOẢNG CHUYỂN TIẾP dưới→trên: " +
                      "đáy collider = hoàn toàn dưới đất, đỉnh = hoàn toàn mặt đất. Player đi lên → fade dần.");
        }
    }
}
