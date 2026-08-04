#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Đổi Map 2–5 từ <see cref="SceneAreaIntro"/> (banner bật một lần lúc load scene) sang
    /// <see cref="AreaNameZone"/> theo VÙNG — giống Map 1.
    ///
    /// Mỗi map 1 khu (theo yêu cầu): zone phủ TRỌN tilemap Ground của scene, nên đi tới đâu cũng thuộc khu đó
    /// và banner hiện đúng một lần khi bước vào (AreaNameZone tự chống spam bằng biến tĩnh _currentArea).
    ///
    /// Tên khu LẤY TỪ SceneAreaIntro đang có (giữ nguyên chữ designer đã đặt), fallback sang bảng dưới nếu
    /// scene không có component đó.
    ///
    /// XOÁ component SceneAreaIntro sau khi chuyển — không để cả hai cùng chạy, nếu không vào map sẽ thấy
    /// banner hai lần (một từ intro, một từ zone).
    ///
    /// Idempotent: chạy lại chỉ cập nhật vùng phủ + tên, không tạo trùng.
    /// Menu: Tools/Attrition/World/Convert Area Intro To Zone (Map 2-5)
    /// </summary>
    public static class AreaNameZoneConvertEditor
    {
        private const string SceneDir = "Assets/_Project/Scenes/";
        private const string ZoneName = "AreaNameZone";

        // Map 1 đã dùng AreaNameZone (2 khu, designer tự chia) → KHÔNG đụng tới.
        private static readonly (string scene, string fallbackArea)[] Targets =
        {
            ("Forest - Map 2",     "Forest Of Life"),
            ("Elf Valley -Map 3",  "Elf Valley"),
            ("Dark Forest - Map 4", "Dark Forest"),
            ("Castle - Map 5",     "The Final Castle"),
        };

        [MenuItem("Tools/Attrition/World/Convert Area Intro To Zone (Map 2-5)")]
        public static void ConvertAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;
            int done = 0;

            try
            {
                foreach (var (scene, fallback) in Targets)
                    if (Convert(scene, fallback)) done++;
            }
            finally
            {
                if (!string.IsNullOrEmpty(original))
                    EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }

            Debug.Log($"[AreaZone] Xong {done}/{Targets.Length} map. Vào map để kiểm: banner tên khu phải hiện " +
                      "MỘT LẦN sau khi scene ổn định (~1.2s), không hiện lại khi đi qua lại trong cùng khu.");
        }

        private static bool Convert(string sceneName, string fallbackArea)
        {
            string path = SceneDir + sceneName + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"[AreaZone] Không thấy scene '{path}'.");
                return false;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            // 1) Lấy tên khu từ SceneAreaIntro hiện có (ưu tiên chữ designer đã đặt).
            string areaName = fallbackArea;
            var intro = Object.FindFirstObjectByType<SceneAreaIntro>(FindObjectsInactive.Include);
            if (intro != null)
            {
                var so = new SerializedObject(intro);
                var p = so.FindProperty("areaName");
                if (p != null && !string.IsNullOrWhiteSpace(p.stringValue)) areaName = p.stringValue;
            }

            // 2) Vùng phủ = bounds tilemap Ground (nơi player thực sự đi được).
            if (!TryGetGroundBounds(out var bounds))
            {
                Debug.LogError($"[AreaZone] {sceneName}: không tìm thấy tilemap Ground → không biết phủ vùng nào.");
                return false;
            }

            // 3) Tạo/cập nhật zone.
            //
            // Dọn xác object rỗng tên "AreaNameZone" do lần chạy LỖI trước để lại (object đã tạo nhưng
            // AddComponent thất bại). Nếu không dọn, FindFirstObjectByType<AreaNameZone> không thấy nó
            // (vì thiếu script) → tool tạo object mới, để lại hai object trùng tên.
            var existing = Object.FindFirstObjectByType<AreaNameZone>(FindObjectsInactive.Include);
            GameObject go = existing != null ? existing.gameObject : FindStrayZoneObject();

            if (go == null)
            {
                go = new GameObject(ZoneName);
                Undo.RegisterCreatedObjectUndo(go, "Create AreaNameZone");
            }

            go.transform.position = bounds.center;

            // Collider PHẢI có TRƯỚC AreaNameZone: class đó khai báo [RequireComponent(typeof(Collider2D))]
            // nên Unity từ chối gắn script khi chưa có collider.
            //
            // KHÔNG dùng toán tử `??` với component Unity: nó là null-check tham chiếu THUẦN C#, bỏ qua
            // ngữ nghĩa null riêng của UnityEngine.Object (object đã destroy vẫn khác null về tham chiếu)
            // → có thể trả về collider "rỗng" rồi ném MissingComponentException ở dòng gán isTrigger.
            // So sánh `== null` mới đi qua toán tử Unity đã override.
            var col2d = go.GetComponent<Collider2D>();
            if (col2d == null)
            {
                col2d = Undo.AddComponent<BoxCollider2D>(go);
                if (col2d == null)
                {
                    Debug.LogError($"[AreaZone] {sceneName}: không gắn được BoxCollider2D vào '{go.name}' → bỏ qua map này.");
                    return false;
                }
            }

            col2d.isTrigger = true;

            // Chỉ đặt kích thước khi là BoxCollider2D. Scene có thể đã dùng Polygon/Composite do designer vẽ
            // tay — ghi đè hình đó sẽ phá vùng họ chỉnh, nên giữ nguyên và chỉ báo lại.
            if (col2d is BoxCollider2D box)
            {
                box.offset = Vector2.zero;
                // Nới nhẹ để player ở sát mép map vẫn nằm trong vùng (tránh khe hở ở biên).
                box.size = new Vector2(bounds.size.x + 4f, bounds.size.y + 4f);
            }
            else
            {
                Debug.LogWarning($"[AreaZone] {sceneName}: '{go.name}' đang dùng {col2d.GetType().Name} " +
                                 "(không phải BoxCollider2D) → GIỮ NGUYÊN hình collider, chỉ set isTrigger + tên khu.");
            }

            var zone = go.GetComponent<AreaNameZone>();
            if (zone == null)
            {
                zone = Undo.AddComponent<AreaNameZone>(go);
                if (zone == null)
                {
                    Debug.LogError($"[AreaZone] {sceneName}: không gắn được AreaNameZone vào '{go.name}' → bỏ qua map này.");
                    return false;
                }
            }

            var zso = new SerializedObject(zone);
            var np = zso.FindProperty("areaName");
            if (np != null) np.stringValue = areaName;
            zso.ApplyModifiedPropertiesWithoutUndo();

            // 4) XOÁ SceneAreaIntro — nếu giữ lại thì banner hiện 2 lần.
            if (intro != null)
            {
                var host = intro.gameObject;
                Undo.DestroyObjectImmediate(intro);
                // Object rỗng chỉ tồn tại để mang SceneAreaIntro → dọn luôn cho sạch hierarchy.
                if (host != go && host.transform.childCount == 0
                    && host.GetComponents<Component>().Length == 1)   // chỉ còn Transform
                    Undo.DestroyObjectImmediate(host);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // Đọc kích thước từ bounds collider (đúng cho mọi loại Collider2D, không chỉ Box).
            var size = col2d.bounds.size;
            Debug.Log($"[AreaZone] {sceneName}: khu '{areaName}', vùng phủ {size.x:F0}x{size.y:F0} " +
                      $"tại {bounds.center}. SceneAreaIntro: {(intro != null ? "đã xoá" : "không có")}.");
            return true;
        }

        /// <summary>
        /// Tìm GameObject tên "AreaNameZone" CÒN SÓT từ lần chạy lỗi (đã tạo object nhưng chưa gắn được
        /// script). Tái dùng nó thay vì tạo thêm object trùng tên. Trả null nếu không có.
        /// </summary>
        private static GameObject FindStrayZoneObject()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == ZoneName) return t.gameObject;
            }
            return null;
        }

        /// <summary>Bounds world của mọi tilemap Ground/HiddenGround (cùng cách nhận diện với MapSilhouetteBaker).</summary>
        private static bool TryGetGroundBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            int groundLayer = LayerMask.NameToLayer("Ground");
            int hiddenGround = LayerMask.NameToLayer("HiddenGround");

            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int l = tm.gameObject.layer;
                string n = tm.gameObject.name.Trim().ToLower();
                bool isGround = (l == groundLayer && groundLayer >= 0)
                                || (l == hiddenGround && hiddenGround >= 0)
                                || n == "ground" || n == "hiddenground";
                if (!isGround) continue;

                tm.CompressBounds();
                var b = tm.localBounds;
                b.center += tm.transform.position;
                if (!found) { bounds = b; found = true; }
                else bounds.Encapsulate(b);
            }
            return found;
        }
    }
}
#endif
