using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Thêm dấu chấm than vàng trên các vùng Hazard để player nhận ra bẫy trước khi chạm.
    /// Dấu dùng TextMeshPro world-space, không cần sprite, script runtime hay NetworkObject.
    ///
    /// Với Hazard Tilemap: đặt một dấu ở giữa mỗi đoạn tile liên tục; đoạn dài đặt thêm dấu mỗi 8 tile.
    /// Với collider thường: đặt một dấu trên tâm collider.
    /// Idempotent: chạy lại cập nhật dấu cũ, không tạo trùng.
    /// </summary>
    public static class HazardWarningSignSetupEditor
    {
        private const string MarkerPrefix = "HazardWarning_";
        private const int MaxTilesPerMarker = 8;

        private static readonly string[] GameplayScenes =
        {
            "Assets/_Project/Scenes/The Darkest Path - Map 1.unity",
            "Assets/_Project/Scenes/Forest - Map 2.unity",
            "Assets/_Project/Scenes/Elf Valley -Map 3.unity",
            "Assets/_Project/Scenes/Dark Forest - Map 4.unity",
            "Assets/_Project/Scenes/Castle - Map 5.unity",
        };

        [MenuItem("Tools/Attrition/World/Setup Hazard Warnings (Current Scene)")]
        public static void SetupCurrentScene()
        {
            int count = SetupCurrent();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[HazardWarning] Đã đặt/cập nhật {count} dấu ! trong scene hiện tại. SAVE scene.");
        }

        [MenuItem("Tools/Attrition/World/Setup Hazard Warnings (All Gameplay Scenes)")]
        public static void SetupAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int scenes = 0;
            int markers = 0;
            foreach (string path in GameplayScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogWarning($"[HazardWarning] Không mở được {path}.");
                    continue;
                }

                markers += SetupCurrent();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenes++;
            }

            Debug.Log($"[HazardWarning] Xong {markers} dấu ! trong {scenes}/{GameplayScenes.Length} scene.");
        }

        private static int SetupCurrent()
        {
            int count = 0;
            foreach (var hazard in Object.FindObjectsByType<Hazard>(FindObjectsInactive.Include,
                                                                     FindObjectsSortMode.None))
            {
                RemoveOldMarkers(hazard.transform);

                var tilemap = hazard.GetComponent<Tilemap>();
                if (tilemap != null)
                    count += CreateTilemapMarkers(hazard.transform, tilemap);
                else
                {
                    var col = hazard.GetComponent<Collider2D>();
                    Vector3 position = col != null
                        ? new Vector3(col.bounds.center.x, col.bounds.max.y + 1.2f, 0f)
                        : hazard.transform.position + Vector3.up * 1.2f;
                    CreateMarker(hazard.transform, position, 0);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Gom tile theo từng đoạn ngang liên tục. Mỗi đoạn dài tối đa 8 tile cho một dấu để bẫy dài
        /// không chỉ có đúng một cảnh báo ở giữa map.
        /// </summary>
        private static int CreateTilemapMarkers(Transform parent, Tilemap tilemap)
        {
            int count = 0;
            int markerIndex = 0;
            BoundsInt bounds = tilemap.cellBounds;

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                int x = bounds.xMin;
                while (x < bounds.xMax)
                {
                    while (x < bounds.xMax && !tilemap.HasTile(new Vector3Int(x, y, 0))) x++;
                    int runStart = x;
                    while (x < bounds.xMax && tilemap.HasTile(new Vector3Int(x, y, 0))) x++;
                    int runLength = x - runStart;

                    for (int offset = 0; offset < runLength; offset += MaxTilesPerMarker)
                    {
                        int segmentLength = Mathf.Min(MaxTilesPerMarker, runLength - offset);
                        float centerX = runStart + offset + (segmentLength - 1) * 0.5f;
                        Vector3 world = tilemap.GetCellCenterWorld(
                            new Vector3Int(Mathf.RoundToInt(centerX), y, 0));
                        world.x += (centerX - Mathf.Round(centerX)) * tilemap.cellSize.x;
                        world.y += tilemap.cellSize.y * 1.5f;
                        world.z = 0f;

                        CreateMarker(parent, world, markerIndex++);
                        count++;
                    }
                }
            }

            // Tilemap rỗng (Map 5 chưa vẽ tile): vẫn để một dấu trên object để designer thấy chỗ cần sửa.
            if (count == 0)
            {
                CreateMarker(parent, parent.position + Vector3.up * 1.2f, markerIndex);
                count = 1;
            }
            return count;
        }

        private static void CreateMarker(Transform parent, Vector3 worldPosition, int index)
        {
            var go = new GameObject($"{MarkerPrefix}{index:00}");
            Undo.RegisterCreatedObjectUndo(go, "Create Hazard Warning");
            go.transform.SetParent(parent, true);
            go.transform.position = worldPosition;

            var text = go.AddComponent<TextMeshPro>();
            text.text = "!";
            text.fontSize = 5f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.82f, 0.05f, 1f);
            text.enableWordWrapping = false;
            text.sortingOrder = 50;
        }

        private static void RemoveOldMarkers(Transform parent)
        {
            var old = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith(MarkerPrefix)) old.Add(child.gameObject);
            }
            foreach (var go in old) Undo.DestroyObjectImmediate(go);
        }
    }
}
