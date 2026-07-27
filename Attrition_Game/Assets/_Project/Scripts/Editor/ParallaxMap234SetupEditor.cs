using UnityEngine;
using UnityEditor;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tạo ParallaxBackground TĨNH cho Map 2, 3, 4 — GIỐNG cách bố trí của Map 1
    /// (ParallaxSetupEditor): mỗi lớp là 1 SpriteRenderer tiled + ParallaxLayer, parallaxFactor
    /// tăng dần từ xa→gần, sortingOrder tăng dần, z giảm dần. KHÁC Map 1: KHÔNG có phần chuyển
    /// background (không dùng ParallaxBackgroundCrossfade). Mỗi map chỉ 1 ParallaxBackground.
    ///
    /// Số lớp = đúng bộ sprite user chỉ định:
    ///   Map 2 (Forest-Druid): Background + Midleground.
    ///   Map 3 (Valley_Elf): BG_1 + BG_2 + BG_3.
    ///   Map 4 (DarkForest-DemonKin/Backgrounds): 7 phần (xa→gần).
    ///
    /// Menu: Tools/Attrition/Parallax/...
    /// </summary>
    public static class ParallaxMap234SetupEditor
    {
        private const string EnvRoot = "Assets/_Project/Art/Environments/";

        [MenuItem("Tools/Attrition/Parallax/Create Parallax Background (Map2 Forest)")]
        public static void CreateMap2()
        {
            // Forest-Druid: 2 lớp — xa (Background), gần (Midleground). "Midleground" là chính tả file.
            var specs = new[]
            {
                new LayerSpec("BG1_Far_Background", "Forest-Druid/Background.png", -100, 0.05f, new Color(0.85f, 0.87f, 0.95f, 1f)),
                new LayerSpec("BG2_Near_MiddleGround", "Forest-Druid/Midleground.png", -90, 0.30f, new Color(0.7f, 0.75f, 0.8f, 1f)),
            };
            Build("ParallaxBackground_Forest", specs);
        }

        [MenuItem("Tools/Attrition/Parallax/Create Parallax Background (Map3 Valley)")]
        public static void CreateMap3()
        {
            // Valley_Elf: 3 lớp — BG_1 (xa) → BG_3 (gần).
            var specs = new[]
            {
                new LayerSpec("BG1_Far",  "Valley_Elf/BG_1.png", -100, 0.05f, new Color(0.85f, 0.85f, 0.9f, 1f)),
                new LayerSpec("BG2_Mid",  "Valley_Elf/BG_2.png",  -90, 0.20f, new Color(0.8f, 0.8f, 0.85f, 1f)),
                new LayerSpec("BG3_Near", "Valley_Elf/BG_3.png",  -80, 0.40f, new Color(0.75f, 0.75f, 0.8f, 1f)),
            };
            Build("ParallaxBackground_Valley", specs);
        }

        [MenuItem("Tools/Attrition/Parallax/Create Parallax Background (Map4 DarkForest)")]
        public static void CreateMap4()
        {
            // DarkForest-DemonKin/Backgrounds: 7 phần, xa→gần. BACKGROUND xa nhất; WOODS-Fourth gần nhất.
            var specs = new[]
            {
                new LayerSpec("BG1_Background",     "DarkForest-DemonKin/Backgrounds/BACKGROUND.png",        -100, 0.02f, new Color(0.5f, 0.52f, 0.62f, 1f)),
                new LayerSpec("BG2_BushBackground", "DarkForest-DemonKin/Backgrounds/BUSH - BACKGROUND.png",  -95, 0.08f, new Color(0.45f, 0.48f, 0.58f, 1f)),
                new LayerSpec("BG3_WoodsFirst",     "DarkForest-DemonKin/Backgrounds/WOODS - First.png",      -90, 0.15f, new Color(0.4f, 0.43f, 0.52f, 1f)),
                new LayerSpec("BG4_VinesSecond",    "DarkForest-DemonKin/Backgrounds/VINES - Second.png",     -85, 0.22f, new Color(0.36f, 0.4f, 0.48f, 1f)),
                new LayerSpec("BG5_WoodsSecond",    "DarkForest-DemonKin/Backgrounds/WOODS - Second.png",     -80, 0.30f, new Color(0.32f, 0.35f, 0.43f, 1f)),
                new LayerSpec("BG6_WoodsThird",     "DarkForest-DemonKin/Backgrounds/WOODS - Third.png",      -75, 0.40f, new Color(0.28f, 0.3f, 0.38f, 1f)),
                new LayerSpec("BG7_WoodsFourth",    "DarkForest-DemonKin/Backgrounds/WOODS - Fourth.png",     -70, 0.50f, new Color(0.24f, 0.26f, 0.33f, 1f)),
            };
            Build("ParallaxBackground_DarkForest", specs);
        }

        private struct LayerSpec
        {
            public string name, path;
            public int sortingOrder;
            public float parallaxFactor;
            public Color color;
            public LayerSpec(string name, string path, int sortingOrder, float parallaxFactor, Color color)
            {
                this.name = name; this.path = path; this.sortingOrder = sortingOrder;
                this.parallaxFactor = parallaxFactor; this.color = color;
            }
        }

        private static void Build(string rootName, LayerSpec[] specs)
        {
            var root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + rootName);
            var sv = SceneView.lastActiveSceneView;
            root.transform.position = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            // z giảm dần theo lớp (lớp đầu xa nhất → z lớn nhất). Trải đều 50→20 như tool Map 1.
            float zFar = 50f, zNear = 20f;
            int n = specs.Length;

            for (int i = 0; i < n; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnvRoot + specs[i].path);
                if (sprite == null)
                {
                    Debug.LogError($"[Attrition] Không tìm thấy sprite: {EnvRoot}{specs[i].path}");
                    Object.DestroyImmediate(root);
                    return;
                }
                float t = n > 1 ? (float)i / (n - 1) : 0f;
                float z = Mathf.Lerp(zFar, zNear, t);
                CreateLayer(root, specs[i], sprite, z);
            }

            Selection.activeGameObject = root;
            Debug.Log($"[Attrition] Đã tạo {rootName} ({n} lớp). Đặt vào scene, chỉnh Y gốc + scale nếu cần. " +
                      "Không có chuyển background (theo yêu cầu).");
        }

        private static void CreateLayer(GameObject parent, LayerSpec spec, Sprite sprite, float posZ)
        {
            var obj = new GameObject(spec.name);
            obj.transform.SetParent(parent.transform);
            obj.transform.localPosition = new Vector3(0f, 0f, posZ);
            // Scale giống tool Map 1 (rộng để phủ, cao gấp bội để không lộ viền dọc).
            obj.transform.localScale = new Vector3(4f, 4f, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = spec.sortingOrder;
            sr.color = spec.color;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(sprite.bounds.size.x * 3f, sprite.bounds.size.y);

            var p = obj.AddComponent<ParallaxLayer>();
            p.parallaxFactor = spec.parallaxFactor;
            p.infiniteHorizontal = true;
            p.followCameraY = true;
        }
    }
}
