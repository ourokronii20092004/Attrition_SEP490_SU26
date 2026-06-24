using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo sẵn hệ thống ĐỔI BACKGROUND THEO KHU VỰC (kiểu Afterimage) cho 1 room:
    ///   - Gắn RegionBackgroundSwitcher lên ParallaxBackground (tự lấy các SpriteRenderer layer).
    ///   - Tạo 2 BackgroundZone: "Underground" (giữ bg hiện tại) + "Surface" (bg = 1,2,3,4).
    ///
    /// Cách dùng: CHỌN object "ParallaxBackground" (cha các BG_Layer) trong Hierarchy → chạy menu.
    /// Sau đó đặt 2 vùng BackgroundZone ở ranh giới dưới-đất / trên-mặt-đất trong room, chỉnh BoxCollider.
    /// Menu: Tools/Attrition/Create Region Background Switch (Map1)
    /// </summary>
    public static class RegionBackgroundSetupEditor
    {
        private const string BgFolder = "Assets/_Project/Art/Environments/DarkRoad_ServerdFang/";

        [MenuItem("Tools/Attrition/Create Region Background Switch (Map1)")]
        public static void CreateRegionSwitch()
        {
            var parallax = Selection.activeGameObject;
            if (parallax == null || parallax.GetComponentInChildren<SpriteRenderer>() == null)
            {
                Debug.LogError("[Attrition] Hãy CHỌN object 'ParallaxBackground' (có các SpriteRenderer layer) trong Hierarchy rồi chạy lại.");
                return;
            }

            // Load bộ sprite mặt đất 1-4.
            var surfaceSprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                surfaceSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{BgFolder}{i + 1}.png");
                if (surfaceSprites[i] == null)
                    Debug.LogWarning($"[Attrition] Không tìm thấy {BgFolder}{i + 1}.png — layer {i} sẽ chỉ fade, không đổi sprite.");
            }

            // 1. Switcher lên ParallaxBackground (lấy các layer SpriteRenderer xa→gần).
            var switcher = parallax.GetComponent<RegionBackgroundSwitcher>();
            if (switcher == null) switcher = parallax.AddComponent<RegionBackgroundSwitcher>();
            var layers = parallax.GetComponentsInChildren<SpriteRenderer>();
            var sw = new SerializedObject(switcher);
            var layersProp = sw.FindProperty("layers");
            layersProp.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
                layersProp.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
            sw.ApplyModifiedPropertiesWithoutUndo();

            // Bộ sprite "underground" = sprite HIỆN TẠI của các layer (để đi xuống thì trả về bg cũ).
            var undergroundSet = new Sprite[layers.Length];
            for (int i = 0; i < layers.Length; i++) undergroundSet[i] = layers[i].sprite;

            // 2. Hai vùng BackgroundZone.
            var sv = SceneView.lastActiveSceneView;
            Vector3 basePos = sv != null ? sv.pivot : Vector3.zero;

            CreateZone("BGZone_Underground", basePos + Vector3.down * 4f, undergroundSet, 0, switcher);
            CreateZone("BGZone_Surface", basePos + Vector3.up * 4f, surfaceSprites, 1, switcher);

            Selection.activeGameObject = switcher.gameObject;
            Debug.Log("[Attrition] Đã tạo RegionBackgroundSwitcher + 2 BackgroundZone (Underground/Surface, bg 1-4). " +
                      "Đặt 2 vùng ở ranh giới dưới/trên trong room, chỉnh BoxCollider phủ lối đi. " +
                      "Underground giữ bg hiện tại, Surface đổi sang 1-4.");
        }

        private static void CreateZone(string name, Vector3 pos, Sprite[] set, int regionId, RegionBackgroundSwitcher switcher)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Background Zone");
            go.transform.position = pos;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(20f, 6f); // phủ ngang lối đi; chỉnh lại cho khớp room

            var zone = go.AddComponent<BackgroundZone>();
            var so = new SerializedObject(zone);
            var setProp = so.FindProperty("backgroundSet");
            setProp.arraySize = set != null ? set.Length : 0;
            if (set != null)
                for (int i = 0; i < set.Length; i++)
                    setProp.GetArrayElementAtIndex(i).objectReferenceValue = set[i];
            so.FindProperty("regionId").intValue = regionId;
            so.FindProperty("switcher").objectReferenceValue = switcher;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
