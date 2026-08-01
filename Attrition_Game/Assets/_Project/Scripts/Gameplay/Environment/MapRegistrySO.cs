using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Danh sách TẤT CẢ map (MapDataSO) trong game — World Map đọc để hiển thị mọi map (Map1/2/3).
    /// Thêm map mới chỉ cần tạo MapDataSO + kéo vào đây, không sửa code.
    /// 1 asset duy nhất, để trong Resources để load runtime dễ.
    /// </summary>
    [CreateAssetMenu(fileName = "MapRegistry", menuName = "Attrition/World Map/Map Registry")]
    public class MapRegistrySO : ScriptableObject
    {
        [Tooltip("Tất cả map theo thứ tự hiển thị trên World Map.")]
        public List<MapDataSO> maps = new List<MapDataSO>();

        private static MapRegistrySO _cached;

        /// <summary>Load registry từ Resources (đặt asset tên 'MapRegistry' trong 1 thư mục Resources).</summary>
        public static MapRegistrySO Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MapRegistrySO>("MapRegistry");
            return _cached;
        }

        public MapDataSO GetByScene(string sceneName)
        {
            foreach (var m in maps)
                if (m != null && m.sceneName == sceneName) return m;
            return null;
        }

        /// <summary>Trả map sở hữu checkpoint khi ID là duy nhất; null nếu thiếu hoặc bị trùng.</summary>
        public MapDataSO GetByCheckpoint(string checkpointId)
        {
            if (string.IsNullOrEmpty(checkpointId)) return null;

            MapDataSO found = null;
            foreach (var map in maps)
            {
                if (map == null || !map.checkpoints.Exists(cp => cp.checkpointId == checkpointId)) continue;
                if (found != null) return null;
                found = map;
            }
            return found;
        }
    }
}
