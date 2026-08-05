using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Trạng thái BẢN ĐỒ TỔNG (fog of war + điểm rest đã khám phá), sống xuyên scene (static).
    /// Nguồn dữ liệu duy nhất cho World Map UI + FogTracker.
    ///
    /// - fogVisited: tập ô "scene:cellX:cellY" đã xua sương.
    /// - discoveredCheckpoints: id (Checkpoint.DisplayName) các điểm rest ĐÃ REST.
    /// - pendingTravel: lệnh fast-travel cross-map đang chờ (đặt trước khi load scene mới).
    ///
    /// Load từ save (LoadFrom) lúc vào game; ghi ngược vào SaveSlotData (WriteTo) khi save.
    /// SOLO: SaveManager. COOP: tùy hệ online (MVP có thể chỉ giữ trong phiên).
    /// </summary>
    public static class WorldMapState
    {
        private static readonly HashSet<string> _fog = new HashSet<string>();
        private static readonly HashSet<string> _checkpoints = new HashSet<string>();

        // Pending cross-map travel: scene đích + id checkpoint đích (null = không có).
        public static string PendingTravelScene;
        public static string PendingTravelCheckpointId;

        public static string CellKey(string scene, int cx, int cy) => $"{scene}:{cx}:{cy}";

        public static bool IsFogVisited(string scene, int cx, int cy) => _fog.Contains(CellKey(scene, cx, cy));

        /// <summary>Đánh dấu 1 ô đã xua sương. Trả về true nếu MỚI (để biết có cần save).</summary>
        public static bool MarkFogVisited(string scene, int cx, int cy) => _fog.Add(CellKey(scene, cx, cy));

        public static IReadOnlyCollection<string> AllFog => _fog;

        public static bool IsCheckpointDiscovered(string id) => !string.IsNullOrEmpty(id) && _checkpoints.Contains(id);

        public static bool MarkCheckpointDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return _checkpoints.Add(id);
        }

        public static IReadOnlyCollection<string> AllDiscoveredCheckpoints => _checkpoints;

        public static bool IsMapDiscovered(MapDataSO map)
        {
            if (map == null) return false;

            string prefix = map.sceneName + ":";
            foreach (var key in _fog)
                if (key.StartsWith(prefix, System.StringComparison.Ordinal)) return true;

            foreach (var checkpoint in map.checkpoints)
                if (IsCheckpointDiscovered(checkpoint.checkpointId)) return true;

            return false;
        }

        public static void LoadFrom(Attrition.Persistence.SaveSlotData data)
        {
            _fog.Clear();
            _checkpoints.Clear();
            if (data == null) return;
            if (data.fogVisited != null) foreach (var k in data.fogVisited) if (!string.IsNullOrEmpty(k)) _fog.Add(k);
            if (data.discoveredCheckpoints != null) foreach (var c in data.discoveredCheckpoints) if (!string.IsNullOrEmpty(c)) _checkpoints.Add(c);
        }

        public static void WriteTo(Attrition.Persistence.SaveSlotData data)
        {
            if (data == null) return;
            data.fogVisited = new List<string>(_fog);
            data.discoveredCheckpoints = new List<string>(_checkpoints);
        }

        /// <summary>
        /// COOP: nạp từ dữ liệu phòng trên server (fog lưu ở SessionEntity.FogJson, checkpoint lưu
        /// thành world-state row). Trước đây coop mất sạch fog + checkpoint khi reopen phòng.
        /// Truyền null cho phần nào không có để giữ nguyên phần đó.
        ///
        /// <paramref name="sessionId"/> quyết định cách hoà (giống <see cref="BossDefeatState.LoadFromIds"/>):
        /// phòng KHÁC → thay thế; CÙNG phòng (fetch lại khi đổi map) → HỢP NHẤT. Bản cũ luôn Clear()
        /// nên đổi map là mất fog + checkpoint vừa mở của map trước (server chưa kịp lưu).
        /// </summary>
        public static void LoadFromCoop(IEnumerable<string> fogCells, IEnumerable<string> checkpointIds,
                                       string sessionId)
        {
            bool differentSession = _loadedSessionId != sessionId;
            if (differentSession)
            {
                _fog.Clear();
                _checkpoints.Clear();
                _loadedSessionId = sessionId;
            }

            if (fogCells != null)
                foreach (var k in fogCells) if (!string.IsNullOrEmpty(k)) _fog.Add(k);
            if (checkpointIds != null)
                foreach (var c in checkpointIds) if (!string.IsNullOrEmpty(c)) _checkpoints.Add(c);
        }

        // Phòng coop đã nạp (null = chưa nạp). Xem LoadFromCoop.
        private static string _loadedSessionId;

        /// <summary>Xoá sạch (vd khi bắt đầu game mới). Không đụng save.</summary>
        public static void Clear()
        {
            _fog.Clear();
            _checkpoints.Clear();
            _loadedSessionId = null;
            PendingTravelScene = null;
            PendingTravelCheckpointId = null;
        }
    }
}
