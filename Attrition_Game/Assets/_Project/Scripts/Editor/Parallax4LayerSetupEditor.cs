using UnityEngine;
using UnityEditor;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tạo ParallaxBackground 4 LỚP dùng sprite 1,2,3,4 (DarkRoad_ServerdFang) — GIỐNG HỆT cách bố trí
    /// của ParallaxBackground 3 lớp cũ (scale, parallaxFactor tăng dần, tiled, sorting), chỉ khác số lớp
    /// và bộ sprite. Lớp xa nhất (1) gần như đứng yên; lớp gần nhất (4) di chuyển nhanh nhất.
    ///
    /// Menu: Tools/Attrition/Create Parallax Background 4-Layer (Surface)
    /// </summary>
    public static class Parallax4LayerSetupEditor
    {
        private const string BgFolder = "Assets/_Project/Art/Environments/DarkRoad_ServerdFang/";

        [MenuItem("Tools/Attrition/Create Parallax Background 4-Layer (Surface)")]
        public static void Create()
        {
            // Load sprite 1-4.
            var sprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{BgFolder}{i + 1}.png");
                if (sprites[i] == null)
                {
                    Debug.LogError($"[Attrition] Không tìm thấy {BgFolder}{i + 1}.png");
                    return;
                }
            }

            var root = new GameObject("ParallaxBackground_Surface");
            Undo.RegisterCreatedObjectUndo(root, "Create Parallax 4-Layer");
            var sv = SceneView.lastActiveSceneView;
            root.transform.position = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            // 4 lớp: xa→gần. parallaxFactor tăng dần; sortingOrder tăng dần; z giảm dần (gần hơn).
            // Màu sáng dần ở lớp gần (giống tool cũ: lớp xa tối/mờ, lớp gần rõ).
            CreateLayer(root, "BG1_Far",   sprites[0], -100, 0.05f, 5f, 10f,  0f, 50f, new Color(0.65f, 0.6f, 0.75f, 1f));
            CreateLayer(root, "BG2",       sprites[1],  -90, 0.15f, 4.5f, 10f, -1f, 40f, new Color(0.5f, 0.48f, 0.6f, 0.95f));
            CreateLayer(root, "BG3",       sprites[2],  -80, 0.30f, 4f, 10f,  -2f, 30f, new Color(0.4f, 0.38f, 0.48f, 0.95f));
            CreateLayer(root, "BG4_Near",  sprites[3],  -70, 0.50f, 4f, 10f,  -3f, 20f, new Color(0.3f, 0.28f, 0.35f, 1f));

            Selection.activeGameObject = root;
            Debug.Log("[Attrition] Đã tạo ParallaxBackground_Surface 4 lớp (sprite 1-4). " +
                      "Đặt vào room, chỉnh vị trí Y gốc nếu cần. Bố trí giống ParallaxBackground 3 lớp cũ.");
        }

        private static GameObject CreateLayer(GameObject parent, string name, Sprite sprite,
            int sortingOrder, float parallaxFactor, float scaleX, float scaleY, float posY, float posZ, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform);
            obj.transform.localPosition = new Vector3(0f, posY, posZ);
            obj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(sprite.bounds.size.x * 3f, sprite.bounds.size.y);

            var p = obj.AddComponent<ParallaxLayer>();
            p.parallaxFactor = parallaxFactor;
            p.infiniteHorizontal = true;
            p.followCameraY = true;
            return obj;
        }
    }
}
