#if UNITY_EDITOR
using System.Collections.Generic;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Chuyển tile trên tilemap "Breakable" thành các <see cref="BreakableObject"/> thật.
    ///
    /// LUỒNG LÀM VIỆC: designer cứ vẽ tile bằng Tile Palette như bình thường → chạy tool này → mỗi CỤM tile
    /// liền kề trở thành 1 vật phá được (có rung, có gate hướng, vỡ theo cụm), và tile được XOÁ khỏi tilemap.
    ///
    /// VÌ SAO PHẢI CHUYỂN, không dùng trực tiếp tilemap:
    ///  • `BreakableObject` despawn chính NetworkObject của nó khi vỡ — tilemap không phải NetworkObject và
    ///    không despawn từng ô được.
    ///  • Hiệu ứng RUNG cần dịch transform riêng của vật thể; rung tilemap sẽ rung cả map.
    ///
    /// Cụm = các tile dính nhau theo 4 hướng (trên/dưới/trái/phải). Mỗi tile giữ nguyên sprite gốc, đặt
    /// thành 1 SpriteRenderer con → hình y như lúc vẽ, kể cả cụm nhiều loại tile khác nhau.
    ///
    /// Idempotent: chạy lại không tạo trùng vì tile đã bị xoá (lần 2 tìm thấy 0 cụm).
    /// Menu: Tools/Attrition/World/Convert Breakable Tiles To Objects
    /// </summary>
    public static class BreakableTileConvertEditor
    {
        private const string SceneDir = "Assets/_Project/Scenes/";
        private const string TilemapName = "Breakable";
        private const string ContainerName = "BreakableObjects";
        private const int DefaultHits = 6;

        private static readonly string[] AllScenes =
        {
            "The Darkest Path - Map 1",
            "Forest - Map 2",
            "Elf Valley -Map 3",
            "Dark Forest - Map 4",
            "Castle - Map 5",
        };

        [MenuItem("Tools/Attrition/World/Convert Breakable Tiles To Objects (current scene)")]
        public static void ConvertCurrent()
        {
            int n = ConvertActiveScene(out var report);
            if (n == 0)
            {
                Debug.Log($"[BreakableTiles] Không tìm thấy tile nào trên tilemap '{TilemapName}' " +
                          "(có thể đã chuyển xong ở lần chạy trước, hoặc scene chưa vẽ tile).");
                return;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[BreakableTiles] Đã tạo {n} vật phá được:\n{report}\n" +
                      "Kiểm tra trong Hierarchy → " + ContainerName + ", rồi SAVE scene để Fusion bake NetworkObject.");
        }

        [MenuItem("Tools/Attrition/World/Convert Breakable Tiles To Objects (ALL maps)")]
        public static void ConvertAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;
            int total = 0;

            try
            {
                foreach (var name in AllScenes)
                {
                    string path = SceneDir + name + ".unity";
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    {
                        Debug.LogWarning($"[BreakableTiles] Không thấy scene '{path}' — bỏ qua.");
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int n = ConvertActiveScene(out var report);
                    if (n > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log($"[BreakableTiles] {name}: {n} vật phá được.\n{report}");
                        total += n;
                    }
                    else Debug.Log($"[BreakableTiles] {name}: không có tile '{TilemapName}' nào.");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original))
                    EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }

            Debug.Log($"[BreakableTiles] XONG — tổng {total} vật phá được trên {AllScenes.Length} map.");
        }

        //  ─── CORE ───

        private static int ConvertActiveScene(out string report)
        {
            report = "";
            var tilemap = FindBreakableTilemap();
            if (tilemap == null) return 0;

            tilemap.CompressBounds();
            var cells = CollectCells(tilemap);
            if (cells.Count == 0) return 0;

            var clusters = GroupAdjacent(cells);

            // Container gom mọi vật phá được cho gọn Hierarchy.
            var container = GameObject.Find(ContainerName);
            if (container == null)
            {
                container = new GameObject(ContainerName);
                Undo.RegisterCreatedObjectUndo(container, "Create BreakableObjects container");
            }

            // Sao chép thông số render từ TilemapRenderer để vật thể mới vẽ ĐÚNG lớp như tile cũ.
            var tmRenderer = tilemap.GetComponent<TilemapRenderer>();
            int sortingLayerId = tmRenderer != null ? tmRenderer.sortingLayerID : 0;
            int sortingOrder = tmRenderer != null ? tmRenderer.sortingOrder : 0;
            int objLayer = tilemap.gameObject.layer;

            var sb = new System.Text.StringBuilder();
            int index = NextFreeIndex(container.transform);

            foreach (var cluster in clusters)
            {
                var go = BuildCluster(tilemap, cluster, container.transform, index,
                                      sortingLayerId, sortingOrder, objLayer);
                sb.AppendLine($"  • {go.name}: {cluster.Count} tile, tâm {go.transform.position}");
                index++;
            }

            // XOÁ tile SAU KHI đã đọc hết sprite. Ghi Undo trước để Ctrl+Z trả lại được tile.
            Undo.RegisterCompleteObjectUndo(tilemap, "Remove breakable tiles");
            foreach (var c in cells) tilemap.SetTile(c, null);
            tilemap.CompressBounds();
            EditorUtility.SetDirty(tilemap);

            report = sb.ToString().TrimEnd();
            return clusters.Count;
        }

        private static Tilemap FindBreakableTilemap()
        {
            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tm != null && tm.gameObject.name.Trim().ToLower() == TilemapName.ToLower()) return tm;
            return null;
        }

        private static List<Vector3Int> CollectCells(Tilemap tilemap)
        {
            var list = new List<Vector3Int>();
            var b = tilemap.cellBounds;
            for (int x = b.xMin; x < b.xMax; x++)
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    var c = new Vector3Int(x, y, 0);
                    if (tilemap.HasTile(c)) list.Add(c);
                }
            return list;
        }

        /// <summary>Nhóm tile DÍNH NHAU (4 hướng) thành từng cụm bằng flood-fill.</summary>
        private static List<List<Vector3Int>> GroupAdjacent(List<Vector3Int> cells)
        {
            var remaining = new HashSet<Vector3Int>(cells);
            var clusters = new List<List<Vector3Int>>();

            while (remaining.Count > 0)
            {
                Vector3Int seed = default;
                foreach (var c in remaining) { seed = c; break; }

                var cluster = new List<Vector3Int>();
                var stack = new Stack<Vector3Int>();
                stack.Push(seed);
                remaining.Remove(seed);

                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    cluster.Add(c);

                    TryTake(remaining, stack, new Vector3Int(c.x + 1, c.y, 0));
                    TryTake(remaining, stack, new Vector3Int(c.x - 1, c.y, 0));
                    TryTake(remaining, stack, new Vector3Int(c.x, c.y + 1, 0));
                    TryTake(remaining, stack, new Vector3Int(c.x, c.y - 1, 0));
                }

                clusters.Add(cluster);
            }

            // Sắp theo x rồi y để tên object (index) ổn định giữa các lần chạy, dễ đối chiếu.
            clusters.Sort((a, b) =>
            {
                int ax = MinX(a), bx = MinX(b);
                if (ax != bx) return ax.CompareTo(bx);
                return MinY(a).CompareTo(MinY(b));
            });
            return clusters;
        }

        private static void TryTake(HashSet<Vector3Int> remaining, Stack<Vector3Int> stack, Vector3Int c)
        {
            if (remaining.Remove(c)) stack.Push(c);
        }

        private static int MinX(List<Vector3Int> c)
        {
            int m = int.MaxValue;
            foreach (var v in c) if (v.x < m) m = v.x;
            return m;
        }

        private static int MinY(List<Vector3Int> c)
        {
            int m = int.MaxValue;
            foreach (var v in c) if (v.y < m) m = v.y;
            return m;
        }

        /// <summary>
        /// Dựng 1 vật phá được từ 1 cụm tile.
        /// Cấu trúc: root (NetworkObject + BoxCollider2D + BreakableObject) → "Visual" (shakeTarget)
        ///           → mỗi tile 1 SpriteRenderer con.
        /// Rung tác động lên "Visual" nên collider ở root KHÔNG lệch theo (vùng đánh trúng giữ nguyên).
        /// </summary>
        private static GameObject BuildCluster(Tilemap tilemap, List<Vector3Int> cluster, Transform parent,
                                               int index, int sortingLayerId, int sortingOrder, int objLayer)
        {
            // Tâm cụm = trung bình tâm các cell (world).
            Vector3 sum = Vector3.zero;
            foreach (var c in cluster) sum += tilemap.GetCellCenterWorld(c);
            Vector3 center = sum / cluster.Count;

            var go = new GameObject($"Breakable_{index}");
            Undo.RegisterCreatedObjectUndo(go, "Create Breakable from tiles");
            go.transform.SetParent(parent, true);
            go.transform.position = center;

            // Layer 'Enemy' để OverlapCircle của PlayerCombat (targetLayers) quét trúng.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayer >= 0 ? enemyLayer : objLayer;

            go.AddComponent<NetworkObject>();

            // Collider phủ đúng khung cụm. isTrigger = false: đòn đánh quét với useTriggers = false.
            var cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2((MaxX(cluster) - MinX(cluster) + 1) * cellSize.x,
                                   (MaxY(cluster) - MinY(cluster) + 1) * cellSize.y);
            col.offset = Vector2.zero;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;

            int drawn = 0;
            foreach (var c in cluster)
            {
                var sprite = tilemap.GetSprite(c);
                if (sprite == null) continue;   // tile không có sprite (vd rule tile chưa resolve)

                var tileGo = new GameObject($"Tile_{c.x}_{c.y}");
                tileGo.transform.SetParent(visual.transform, false);
                tileGo.transform.position = tilemap.GetCellCenterWorld(c);

                var sr = tileGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = tilemap.GetColor(c);
                sr.sortingLayerID = sortingLayerId;
                sr.sortingOrder = sortingOrder;

                // Giữ nguyên lật/quay mà designer đặt cho tile (Tilemap lưu trong transform matrix).
                var m = tilemap.GetTransformMatrix(c);
                tileGo.transform.localRotation = m.rotation;
                var s = m.lossyScale;
                // Matrix mặc định có scale (1,1,1); lật ngang/dọc cho ra giá trị âm → giữ lại.
                tileGo.transform.localScale = new Vector3(
                    Mathf.Approximately(s.x, 0f) ? 1f : s.x,
                    Mathf.Approximately(s.y, 0f) ? 1f : s.y,
                    1f);
                drawn++;
            }

            if (drawn == 0)
                Debug.LogWarning($"[BreakableTiles] '{go.name}': không đọc được sprite của tile nào " +
                                 "→ vật thể sẽ VÔ HÌNH. Kiểm tra tile asset (rule tile / tile trống?).");

            var breakable = go.AddComponent<BreakableObject>();
            var so = new SerializedObject(breakable);
            SetInt(so, "hitsToBreak", DefaultHits);
            SetInt(so, "breakOnlyFromSide", (int)BreakableObject.BreakSide.FromRight);
            var shake = so.FindProperty("shakeTarget");
            if (shake != null) shake.objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static int MaxX(List<Vector3Int> c)
        {
            int m = int.MinValue;
            foreach (var v in c) if (v.x > m) m = v.x;
            return m;
        }

        private static int MaxY(List<Vector3Int> c)
        {
            int m = int.MinValue;
            foreach (var v in c) if (v.y > m) m = v.y;
            return m;
        }

        /// <summary>Index kế tiếp chưa dùng trong container — tránh trùng tên khi vẽ thêm tile rồi chạy lại.</summary>
        private static int NextFreeIndex(Transform container)
        {
            int max = -1;
            foreach (Transform child in container)
            {
                if (!child.name.StartsWith("Breakable_")) continue;
                if (int.TryParse(child.name.Substring("Breakable_".Length), out int n) && n > max) max = n;
            }
            return max + 1;
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[BreakableTiles] Không thấy field '{field}' trên BreakableObject.");
        }
    }
}
#endif
