using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo sẵn hệ thống CROSSFADE BACKGROUND theo khu vực (giữ nguyên 2 object ParallaxBackground
    /// với số layer khác nhau — 3 lớp dưới đất, 4 lớp mặt đất). Tạo:
    ///   - 1 object "BackgroundCrossfade" (ParallaxBackgroundCrossfade) đã gán 2 background.
    ///   - 2 vùng "BGZone_Underground" (regionId 0) + "BGZone_Surface" (regionId 1), nối sẵn crossfade.
    ///
    /// CÁCH DÙNG: chọn (Selection) 2 object ParallaxBackground trong Hierarchy theo thứ tự
    /// [dưới đất, mặt đất] rồi chạy menu. Nếu không chọn → tool tạo crossfade rỗng để bạn tự kéo.
    /// Menu: Tools/Attrition/Create Background Crossfade + Zones
    /// </summary>
    public static class BackgroundCrossfadeSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Background Crossfade + Zones")]
        public static void Create()
        {
            // Lấy 2 background từ Selection (thứ tự: index 0 = dưới đất, index 1 = mặt đất).
            var selected = Selection.gameObjects;

            var crossfadeGo = new GameObject("BackgroundCrossfade");
            Undo.RegisterCreatedObjectUndo(crossfadeGo, "Create Background Crossfade");
            var crossfade = crossfadeGo.AddComponent<ParallaxBackgroundCrossfade>();

            var cso = new SerializedObject(crossfade);
            var bgProp = cso.FindProperty("backgrounds");
            if (selected != null && selected.Length >= 2)
            {
                bgProp.arraySize = selected.Length;
                for (int i = 0; i < selected.Length; i++)
                    bgProp.GetArrayElementAtIndex(i).objectReferenceValue = selected[i];
                Debug.Log($"[Attrition] Gán {selected.Length} background vào crossfade theo thứ tự chọn: " +
                          string.Join(", ", System.Array.ConvertAll(selected, g => g.name)));
            }
            else
            {
                bgProp.arraySize = 2; // để trống 2 ô cho bạn tự kéo (index 0 = dưới đất, 1 = mặt đất)
                Debug.LogWarning("[Attrition] Chưa chọn 2 ParallaxBackground → tạo crossfade với 2 ô TRỐNG. " +
                                 "Hãy kéo tay: Element 0 = bg dưới đất, Element 1 = bg mặt đất.");
            }
            cso.FindProperty("startIndex").intValue = 0; // bắt đầu dưới đất
            cso.ApplyModifiedPropertiesWithoutUndo();

            // 2 vùng trigger.
            var sv = SceneView.lastActiveSceneView;
            Vector3 basePos = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            CreateZone("BGZone_Underground", basePos + Vector3.down * 4f, 0, crossfade);
            CreateZone("BGZone_Surface", basePos + Vector3.up * 4f, 1, crossfade);

            Selection.activeGameObject = crossfadeGo;
            Debug.Log("[Attrition] Đã tạo BackgroundCrossfade + 2 BGZone (Underground regionId=0, Surface regionId=1). " +
                      "Đặt 2 vùng ở ranh giới dưới/trên trong room, chỉnh BoxCollider phủ lối đi.");
        }

        private static void CreateZone(string name, Vector3 pos, int regionId, ParallaxBackgroundCrossfade crossfade)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Background Zone");
            go.transform.position = pos;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(20f, 6f); // phủ ngang lối đi; chỉnh lại cho khớp room

            var zone = go.AddComponent<BackgroundZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("regionId").intValue = regionId;
            so.FindProperty("crossfade").objectReferenceValue = crossfade;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
