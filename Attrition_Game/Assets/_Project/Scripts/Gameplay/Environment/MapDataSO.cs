using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Dữ liệu BẢN ĐỒ của 1 scene/map: ảnh silhouette địa hình (tự sinh từ Tilemap), vùng world ảnh
    /// phủ, kích thước ô fog, và danh sách checkpoint (để chấm marker đúng vị trí).
    /// 1 asset cho mỗi map (Map1/2/3). World Map đọc qua MapRegistrySO.
    /// </summary>
    [CreateAssetMenu(fileName = "MapData", menuName = "Attrition/World Map/Map Data")]
    public class MapDataSO : ScriptableObject
    {
        [System.Serializable]
        public struct CheckpointMarker
        {
            public string checkpointId;  // = Checkpoint.DisplayName
            public Vector2 worldPos;     // vị trí world để map sang toạ độ ảnh
        }

        [Tooltip("Tên scene (KHÔNG path/đuôi). Phải khớp Build Settings + Checkpoint.checkpointScene.")]
        public string sceneName;
        [Tooltip("Tên hiển thị trên World Map (vd 'The Darkest Path').")]
        public string displayName;

        [Tooltip("Ảnh silhouette địa hình (do tool MapSilhouetteBaker tự sinh từ Tilemap).")]
        public Sprite silhouette;

        [Tooltip("Vùng WORLD mà ảnh silhouette phủ (min..max). Tool tự set theo bounds Tilemap.")]
        public Bounds worldBounds;

        [Tooltip("Vị trí OFFSET của map này trên BẢN ĐỒ TỔNG (đơn vị: cùng tỉ lệ map units). Dùng để xếp " +
                 "các map liền mạch cạnh nhau (kiểu Hollow Knight) dù scene tách rời. (0,0) = map gốc.")]
        public Vector2 worldMapOffset = Vector2.zero;

        [Tooltip("Tỉ lệ thu nhỏ khi ghép vào bản đồ tổng (1 = giữ nguyên world units). Chỉnh nếu các map " +
                 "lệch tỉ lệ. Để 1 cho hầu hết trường hợp.")]
        public float worldMapScale = 1f;

        [Tooltip("Kích thước 1 ô fog theo world units (nhỏ = mịn nhưng nặng hơn). ~2-3 hợp lý.")]
        public float fogCellSize = 2.5f;

        [Header("──── MÀU TRÊN BẢN ĐỒ TỔNG (M) ────")]
        [Tooltip("Màu NỀN địa hình của map này trên World Map. Mỗi khu một màu để nhìn là biết đang ở đâu " +
                 "(kiểu Afterimage). Tool 'Setup World Map Colors' điền sẵn theo thiết kế.")]
        public Color mapTint = Color.white;

        [Tooltip("Màu ĐƯỜNG VIỀN quanh địa hình. Nên ĐẬM HƠN mapTint để đường nét nổi bật.")]
        public Color outlineTint = new Color(0.15f, 0.12f, 0.10f, 1f);

        [Tooltip("Độ dày đường viền (pixel trên ảnh silhouette). 0 = không vẽ viền.")]
        [Range(0f, 6f)] public float outlineThickness = 2f;

        [Tooltip("Các checkpoint trong map (tool tự quét điền). worldPos để chấm marker.")]
        public List<CheckpointMarker> checkpoints = new List<CheckpointMarker>();

        /// <summary>Số ô fog theo trục X/Y dựa trên worldBounds + fogCellSize.</summary>
        public Vector2Int FogGridSize()
        {
            if (fogCellSize <= 0.01f) return Vector2Int.one;
            int gx = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.x / fogCellSize));
            int gy = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.y / fogCellSize));
            return new Vector2Int(gx, gy);
        }

        /// <summary>Quy vị trí world → toạ độ ô fog (cx, cy). Ngoài bounds → clamp.</summary>
        public Vector2Int WorldToCell(Vector2 world)
        {
            if (fogCellSize <= 0.01f) return Vector2Int.zero;
            int cx = Mathf.FloorToInt((world.x - worldBounds.min.x) / fogCellSize);
            int cy = Mathf.FloorToInt((world.y - worldBounds.min.y) / fogCellSize);
            var g = FogGridSize();
            return new Vector2Int(Mathf.Clamp(cx, 0, g.x - 1), Mathf.Clamp(cy, 0, g.y - 1));
        }

        /// <summary>Quy vị trí world → toạ độ chuẩn hoá 0..1 trong ảnh (để đặt marker trên UI).</summary>
        public Vector2 WorldToNormalized(Vector2 world)
        {
            return new Vector2(
                Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, world.x),
                Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, world.y));
        }
    }
}
