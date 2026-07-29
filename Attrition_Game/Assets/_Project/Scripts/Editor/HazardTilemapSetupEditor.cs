#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Bật SÁT THƯƠNG BẪY cho các Tilemap thuộc "Hazard" (theo physics layer HOẶC sorting layer).
    ///
    /// VÌ SAO CẦN: mỗi scene đang có 1 Tilemap tên "Hazard" ở layer 11, nhưng nó KHÔNG có collider và
    /// KHÔNG có script `Hazard` → đi vào gai/dung nham chẳng mất máu. Tool gắn:
    ///   - `TilemapCollider2D` với isTrigger = TRUE (bẫy chỉ cần phát hiện, không chặn đường player).
    ///   - `Hazard` (MonoBehaviour có sẵn) → gọi `PlayerController.HazardHit()`: -15% Max HP + hồi sinh
    ///     tại điểm đất an toàn cuối (BR-38/39).
    /// KHÔNG cần NetworkObject: mỗi PlayerController tự xử lý phần networked của mình.
    ///
    /// Nhận diện tilemap hazard (đủ 1 trong 3 là được):
    ///   - GameObject ở physics layer "Hazard", HOẶC
    ///   - TilemapRenderer có sorting layer "Hazard", HOẶC
    ///   - tên GameObject đúng bằng "Hazard" (không dùng Contains để tránh trùng nhầm).
    ///
    /// 2 menu: chỉ scene đang mở, hoặc TẤT CẢ scene gameplay.
    /// Idempotent — chạy lại không thêm trùng component.
    /// </summary>
    public static class HazardTilemapSetupEditor
    {
        private const string HazardLayerName = "Hazard";

        private static readonly string[] GameplayScenes =
        {
            "The Darkest Path - Map 1",
            "Forest - Map 2",
            "Elf Valley -Map 3",
            "Dark Forest - Map 4",
            "Castle - Map 5",
        };

        [MenuItem("Tools/Attrition/Hazard/Setup Hazard Tilemaps (current scene)")]
        public static void SetupCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                EditorUtility.DisplayDialog("Hazard Setup", "Mở một scene gameplay trước đã.", "OK");
                return;
            }

            int n = SetupActiveScene(out var names);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(n > 0
                ? $"[HazardSetup] '{scene.name}': đã bật {n} tilemap hazard ({string.Join(", ", names)}). SAVE scene."
                : $"[HazardSetup] '{scene.name}': không tìm thấy Tilemap hazard nào " +
                  "(cần GameObject ở layer 'Hazard', hoặc sorting layer 'Hazard', hoặc tên 'Hazard').");
        }

        [MenuItem("Tools/Attrition/Hazard/Setup Hazard Tilemaps (ALL maps)")]
        public static void SetupAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;
            int total = 0;

            foreach (var sceneName in GameplayScenes)
            {
                string path = $"Assets/_Project/Scenes/{sceneName}.unity";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning($"[HazardSetup] Không thấy scene: {path} — bỏ qua.");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = SetupActiveScene(out var names);
                total += n;

                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);   // lưu luôn, tránh mất khi mở scene kế tiếp
                    Debug.Log($"[HazardSetup] {sceneName}: {n} tilemap ({string.Join(", ", names)}) — đã lưu.");
                }
                else
                {
                    Debug.Log($"[HazardSetup] {sceneName}: không có tilemap hazard nào cần sửa.");
                }
            }

            if (!string.IsNullOrEmpty(original) && System.IO.File.Exists(original))
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

            Debug.Log($"[HazardSetup] XONG — tổng {total} tilemap hazard đã bật sát thương.");
        }

        //  CORE

        /// <summary>Gắn collider + Hazard cho mọi tilemap hazard trong scene đang mở. Trả số lượng đã xử lý.</summary>
        private static int SetupActiveScene(out List<string> names)
        {
            names = new List<string>();
            int hazardLayer = LayerMask.NameToLayer(HazardLayerName);

            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (tm == null || !IsHazard(tm, hazardLayer)) continue;

                var go = tm.gameObject;

                // Collider theo hình tile. isTrigger = true → player đi XUYÊN QUA nhưng vẫn bị trúng bẫy
                // (bẫy chặn đường sẽ làm player đứng trên gai mà không rơi, sai ý đồ thiết kế).
                var col = go.GetComponent<TilemapCollider2D>();
                if (col == null) col = Undo.AddComponent<TilemapCollider2D>(go);
                if (!col.isTrigger)
                {
                    Undo.RecordObject(col, "Hazard collider trigger");
                    col.isTrigger = true;
                }

                // Script gây sát thương (đã có sẵn trong project).
                if (go.GetComponent<Hazard>() == null) Undo.AddComponent<Hazard>(go);

                // Đưa về đúng physics layer để rõ ràng + khớp mọi raycast/query lọc theo layer Hazard.
                if (hazardLayer >= 0 && go.layer != hazardLayer)
                {
                    Undo.RecordObject(go, "Hazard layer");
                    go.layer = hazardLayer;
                }

                // Tilemap chưa vẽ tile nào → collider không sinh hình, bẫy vô hiệu. Báo rõ để designer
                // biết cần vẽ tile gai/dung nham (VD Castle - Map 5 hiện chưa vẽ ô hazard nào).
                tm.CompressBounds();
                if (tm.GetUsedTilesCount() == 0)
                    Debug.LogWarning($"[HazardSetup] '{go.name}' đã gắn collider + Hazard nhưng CHƯA VẼ TILE nào " +
                                     "→ chưa gây sát thương. Vẽ tile hazard lên tilemap này trong Tile Palette.");

                names.Add(go.name);
            }

            return names.Count;
        }

        /// <summary>Tilemap này có phải hazard? Nhận theo physics layer / sorting layer / tên.</summary>
        private static bool IsHazard(Tilemap tm, int hazardLayer)
        {
            if (hazardLayer >= 0 && tm.gameObject.layer == hazardLayer) return true;

            var rend = tm.GetComponent<TilemapRenderer>();
            if (rend != null && rend.sortingLayerName == HazardLayerName) return true;

            return tm.gameObject.name.Trim().Equals(HazardLayerName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
