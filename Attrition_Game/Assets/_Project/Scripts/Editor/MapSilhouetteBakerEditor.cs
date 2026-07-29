using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool TỰ SINH ảnh silhouette địa hình từ Tilemap của scene đang mở + tạo/điền MapDataSO.
    ///   - Quét mọi Tilemap, gộp cellBounds → 1 lưới chung.
    ///   - Ô có tile = TƯỜNG (màu đậm). Vùng bao quanh trong khung map = ĐƯỜNG ĐI (màu nhạt hơn).
    ///   - Xuất PNG vào Assets/_Project/Art/Maps/<scene>_silhouette.png, gán vào MapDataSO.
    ///   - Tự set worldBounds (theo Tilemap) + quét Checkpoint điền marker.
    /// Menu: Tools/Attrition/Bake Map Silhouette (current scene)
    /// </summary>
    public static class MapSilhouetteBakerEditor
    {
        private const int PixelsPerCell = 4;     // độ phân giải: mỗi ô tile = 4x4 px
        private static readonly Color WallColor = new Color(0.78f, 0.80f, 0.92f, 1f);   // tường: sáng đậm
        private static readonly Color PathColor = new Color(0.30f, 0.32f, 0.45f, 1f);   // đường đi: nhạt hơn
        private static readonly Color EmptyColor = new Color(0f, 0f, 0f, 0f);            // ngoài map: trong suốt

        /// <summary>Mọi scene gameplay cần có mặt trên World Map (khớp Build Settings).</summary>
        private static readonly string[] GameplayScenes =
        {
            "The Darkest Path - Map 1",
            "Forest - Map 2",
            "Elf Valley -Map 3",
            "Dark Forest - Map 4",
            "Castle - Map 5",
        };

        /// <summary>
        /// Bake LẦN LƯỢT mọi map: mở từng scene → bake silhouette + MapData (checkpointId = DisplayName)
        /// → tự thêm vào MapRegistry. Dùng khi World Map không hiện điểm tele: nguyên nhân thường là
        /// MapData thiếu (Map 2..5 chưa bake) hoặc checkpointId cũ không khớp DisplayName hiện tại.
        /// </summary>
        [MenuItem("Tools/Attrition/Bake ALL Maps (World Map)")]
        public static void BakeAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;
            int ok = 0;

            foreach (var name in GameplayScenes)
            {
                string path = $"Assets/_Project/Scenes/{name}.unity";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning($"[MapBaker] Không thấy scene: {path} — bỏ qua.");
                    continue;
                }

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Bake();
                ok++;
            }

            // Trả lại scene ban đầu cho designer.
            if (!string.IsNullOrEmpty(original) && System.IO.File.Exists(original))
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

            Debug.Log($"[MapBaker] BAKE ALL xong: {ok}/{GameplayScenes.Length} map. " +
                      "Mở World Map (M) để kiểm tra — điểm tele chỉ hiện SAU khi đã REST tại checkpoint đó.");
        }

        [MenuItem("Tools/Attrition/Bake Map Silhouette (current scene)")]
        public static void Bake()
        {
            var allTilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            if (allTilemaps == null || allTilemaps.Length == 0)
            {
                Debug.LogError("[MapBaker] Không tìm thấy Tilemap nào trong scene.");
                return;
            }

            // CHỈ lấy tilemap thuộc layer "Ground" (bỏ Background/Foreground/Decor...). Nhận diện qua:
            //  - GameObject layer == Ground/HiddenGround, HOẶC
            //  - tên GameObject ĐÚNG BẰNG "Ground" (KHÔNG dùng Contains — "Background" cũng chứa "ground").
            int groundLayer = LayerMask.NameToLayer("Ground");
            int hiddenGround = LayerMask.NameToLayer("HiddenGround");
            var list = new List<Tilemap>();
            foreach (var tm in allTilemaps)
            {
                int l = tm.gameObject.layer;
                bool byLayer = (l == groundLayer && groundLayer >= 0) || (l == hiddenGround && hiddenGround >= 0);
                string n = tm.gameObject.name.Trim().ToLower();
                bool byName = n == "ground" || n == "hiddenground";
                if (byLayer || byName) list.Add(tm);
            }
            if (list.Count == 0)
            {
                Debug.LogError("[MapBaker] Không tìm thấy Tilemap layer 'Ground'. Đảm bảo tilemap nền để layer Ground " +
                               "hoặc tên chứa 'Ground'.");
                return;
            }
            var tilemaps = list.ToArray();

            // 1) Gộp cellBounds (toạ độ ô) của các tilemap Ground.
            BoundsInt cb = tilemaps[0].cellBounds;
            int minX = cb.xMin, minY = cb.yMin, maxX = cb.xMax, maxY = cb.yMax;
            foreach (var tm in tilemaps)
            {
                var b = tm.cellBounds;
                minX = Mathf.Min(minX, b.xMin); minY = Mathf.Min(minY, b.yMin);
                maxX = Mathf.Max(maxX, b.xMax); maxY = Mathf.Max(maxY, b.yMax);
            }
            int cellsW = Mathf.Max(1, maxX - minX);
            int cellsH = Mathf.Max(1, maxY - minY);

            // 2) Lưới đánh dấu ô có tile (tường = Ground).
            bool[,] wall = new bool[cellsW, cellsH];
            foreach (var tm in tilemaps)
            {
                var b = tm.cellBounds;
                for (int x = b.xMin; x < b.xMax; x++)
                    for (int y = b.yMin; y < b.yMax; y++)
                        if (tm.HasTile(new Vector3Int(x, y, 0)))
                        {
                            int gx = x - minX, gy = y - minY;
                            if (gx >= 0 && gx < cellsW && gy >= 0 && gy < cellsH) wall[gx, gy] = true;
                        }
            }

            BakeTextureAndAsset(tilemaps[0], minX, minY, cellsW, cellsH, wall);
        }

        // (phần vẽ texture + tạo asset ở chunk sau)
        private static void BakeTextureAndAsset(Tilemap refTm, int minX, int minY, int cellsW, int cellsH, bool[,] wall)
        {
            // 3) "Đường đi" = ô RỖNG nhưng nằm GIỮA các tường (interior). Phát hiện bằng flood-fill từ
            // viền ngoài: ô rỗng chạm tới viền = NGOÀI map (trong suốt); ô rỗng không chạm viền = đường đi.
            bool[,] outside = new bool[cellsW, cellsH];
            var stack = new Stack<Vector2Int>();
            for (int x = 0; x < cellsW; x++) { TryPush(stack, outside, wall, x, 0, cellsW, cellsH); TryPush(stack, outside, wall, x, cellsH - 1, cellsW, cellsH); }
            for (int y = 0; y < cellsH; y++) { TryPush(stack, outside, wall, 0, y, cellsW, cellsH); TryPush(stack, outside, wall, cellsW - 1, y, cellsW, cellsH); }
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                TryPush(stack, outside, wall, p.x + 1, p.y, cellsW, cellsH);
                TryPush(stack, outside, wall, p.x - 1, p.y, cellsW, cellsH);
                TryPush(stack, outside, wall, p.x, p.y + 1, cellsW, cellsH);
                TryPush(stack, outside, wall, p.x, p.y - 1, cellsW, cellsH);
            }

            // 4) Vẽ texture: tường đậm, đường đi (rỗng & không outside) nhạt, ngoài map trong suốt.
            int texW = cellsW * PixelsPerCell, texH = cellsH * PixelsPerCell;
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            var px = new Color[texW * texH];
            for (int cx = 0; cx < cellsW; cx++)
                for (int cy = 0; cy < cellsH; cy++)
                {
                    Color c = wall[cx, cy] ? WallColor : (outside[cx, cy] ? EmptyColor : PathColor);
                    for (int ix = 0; ix < PixelsPerCell; ix++)
                        for (int iy = 0; iy < PixelsPerCell; iy++)
                            px[(cy * PixelsPerCell + iy) * texW + (cx * PixelsPerCell + ix)] = c;
                }
            tex.SetPixels(px);
            tex.Apply();

            SaveTextureAndAsset(refTm, minX, minY, cellsW, cellsH, tex);
        }

        private static void TryPush(Stack<Vector2Int> stack, bool[,] outside, bool[,] wall, int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            if (outside[x, y] || wall[x, y]) return;
            outside[x, y] = true;
            stack.Push(new Vector2Int(x, y));
        }

        private static void SaveTextureAndAsset(Tilemap refTm, int minX, int minY, int cellsW, int cellsH, Texture2D tex)
        {
            string scene = SceneManager.GetActiveScene().name;
            string safe = scene.Replace(" ", "_").Replace("-", "");
            const string dir = "Assets/_Project/Art/Maps";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string pngPath = $"{dir}/{safe}_silhouette.png";

            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(pngPath);

            // Cấu hình import: Sprite, không nén để giữ nét.
            var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);

            // worldBounds = vùng world mà lưới ô phủ (góc min ô → world).
            Vector3 wMin = refTm.CellToWorld(new Vector3Int(minX, minY, 0));
            Vector3 wMax = refTm.CellToWorld(new Vector3Int(minX + cellsW, minY + cellsH, 0));
            var bounds = new Bounds();
            bounds.SetMinMax(wMin, wMax);

            // Tạo / cập nhật MapDataSO.
            string soPath = $"{dir}/{safe}_MapData.asset";
            var data = AssetDatabase.LoadAssetAtPath<MapDataSO>(soPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MapDataSO>();
                AssetDatabase.CreateAsset(data, soPath);
            }
            data.sceneName = scene;
            if (string.IsNullOrEmpty(data.displayName)) data.displayName = scene;
            data.silhouette = sprite;
            data.worldBounds = bounds;

            // Quét Checkpoint điền marker. checkpointId PHẢI = Checkpoint.DisplayName vì World Map lọc
            // marker qua WorldMapState.IsCheckpointDiscovered(id), mà discovery được ghi theo DisplayName.
            data.checkpoints.Clear();
            foreach (var cp in Object.FindObjectsByType<Attrition.Gameplay.World.Checkpoint>(FindObjectsSortMode.None))
            {
                if (cp == null) continue;
                data.checkpoints.Add(new MapDataSO.CheckpointMarker { checkpointId = cp.DisplayName, worldPos = cp.transform.position });
            }

            EditorUtility.SetDirty(data);

            // Tự ĐĂNG KÝ vào MapRegistry (Resources/MapRegistry) — trước đây phải kéo tay nên map mới
            // bake xong vẫn không hiện trên World Map.
            bool registered = RegisterInRegistry(data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MapBaker] Đã bake '{scene}': {pngPath} + {soPath} ({data.checkpoints.Count} checkpoint, " +
                      $"bounds {bounds.size}). MapRegistry: {(registered ? "đã thêm" : "đã có sẵn")}.");
        }

        /// <summary>
        /// Thêm MapData vào asset `Resources/MapRegistry` nếu chưa có. Trả true nếu VỪA thêm.
        /// Tự tạo registry nếu chưa tồn tại.
        /// </summary>
        private static bool RegisterInRegistry(MapDataSO data)
        {
            const string resDir = "Assets/Resources";
            const string regPath = "Assets/Resources/MapRegistry.asset";

            if (!AssetDatabase.IsValidFolder(resDir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var reg = AssetDatabase.LoadAssetAtPath<MapRegistrySO>(regPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<MapRegistrySO>();
                AssetDatabase.CreateAsset(reg, regPath);
            }

            if (reg.maps == null) reg.maps = new List<MapDataSO>();
            if (reg.maps.Contains(data)) return false;

            reg.maps.Add(data);
            EditorUtility.SetDirty(reg);
            return true;
        }
    }
}
