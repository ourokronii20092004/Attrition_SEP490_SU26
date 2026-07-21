using UnityEngine;
using UnityEditor;

namespace Attrition.Gameplay.Environment.Editor
{
    /// <summary>
    /// Editor tool: tự động tạo GameObject "ParallaxBackground" với 3 lớp parallax
    /// sử dụng ảnh từ DarkRoad_ServerdFang.
    /// Menu: Tools > Create Parallax Background (Map1)
    /// </summary>
    public class ParallaxSetupEditor
    {
        [MenuItem("Tools/Create Parallax Background (Map1)")]
        public static void CreateParallaxBackground()
        {
            string basePath = "Assets/_Project/Art/Environments/DarkRoad_ServerdFang/";
            Sprite bgColor = AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "backgroundColor.png");
            Sprite bgShapes = AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "backgroundShapes.png");

            if (bgColor == null)
            {
                Debug.LogError("[Parallax] Không tìm thấy backgroundColor.png! Kiểm tra đường dẫn: " + basePath);
                return;
            }
            if (bgShapes == null)
            {
                Debug.LogError("[Parallax] Không tìm thấy backgroundShapes.png! Kiểm tra đường dẫn: " + basePath);
                return;
            }

            // Tạo root object
            GameObject root = new GameObject("ParallaxBackground");
            Undo.RegisterCreatedObjectUndo(root, "Create Parallax Background");
            root.transform.position = Vector3.zero;

            GameObject layer1 = CreateLayer(root, "BG_Layer1_Sky", bgColor,
                sortingOrder: -100, parallaxFactor: 0.05f,
                scaleX: 5f, scaleY: 10f, posY: 0f, posZ: 50f,
                color: new Color(0.6f, 0.55f, 0.7f, 1f)); // tím nhạt

            GameObject layer2 = CreateLayer(root, "BG_Layer2_FarShapes", bgShapes,
                sortingOrder: -90, parallaxFactor: 0.15f,
                scaleX: 4f, scaleY: 10f, posY: -1f, posZ: 40f,
                color: new Color(0.25f, 0.22f, 0.3f, 0.85f)); // tối hơn, mờ nhẹ

            GameObject layer3 = CreateLayer(root, "BG_Layer3_NearShapes", bgShapes,
                sortingOrder: -80, parallaxFactor: 0.35f,
                scaleX: 4f, scaleY: 10f, posY: -2f, posZ: 30f,
                color: new Color(0.15f, 0.13f, 0.18f, 0.95f)); // gần đen

            Selection.activeGameObject = root;
            Debug.Log("[Parallax] Đã tạo ParallaxBackground với 3 lớp! Hãy đặt nó vào scene Map 1.");
        }

        private static GameObject CreateLayer(GameObject parent, string name, Sprite sprite,
            int sortingOrder, float parallaxFactor,
            float scaleX, float scaleY, float posY, float posZ,
            Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform);
            obj.transform.localPosition = new Vector3(0f, posY, posZ);
            obj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(sprite.bounds.size.x * 3f, sprite.bounds.size.y);

            var parallax = obj.AddComponent<ParallaxLayer>();
            parallax.parallaxFactor = parallaxFactor;
            parallax.infiniteHorizontal = true;
            parallax.followCameraY = true;

            return obj;
        }
    }
}
