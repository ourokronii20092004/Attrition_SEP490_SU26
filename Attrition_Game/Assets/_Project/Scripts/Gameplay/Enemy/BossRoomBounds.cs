using UnityEngine;

namespace Attrition.Gameplay.Enemy
{
    /// <summary>
    /// Lấy CHIỀU NGANG của phòng boss — dùng cho các skill "trải hết chiều ngang map":
    /// Elf skill 5 (sét rơi khắp phòng), DemonKin skill 1/4, ArchDemon skill 3/4.
    ///
    /// NGUỒN SỰ THẬT: `CameraBoundsZone` — collider mà designer đã kéo phủ đúng căn phòng để giới hạn
    /// camera (kiểu Hollow Knight). Tái dùng nó thay vì thêm một ô "chiều rộng phòng" nữa trên mỗi boss:
    /// một nguồn số liệu, không lệch nhau, designer không phải nhập 2 lần.
    ///
    /// Không tìm thấy vùng nào chứa boss → trả về khoảng fallback quanh boss, để skill vẫn chạy (ngắn hơn)
    /// chứ không biến mất im lặng trong phòng chưa đặt CameraBoundsZone.
    /// </summary>
    public static class BossRoomBounds
    {
        /// <summary>
        /// Biên trái/phải của phòng chứa `worldPos`. `fallbackHalfWidth` là nửa chiều rộng dùng khi
        /// không có CameraBoundsZone nào phủ điểm đó.
        /// </summary>
        public static void GetHorizontal(Vector2 worldPos, float fallbackHalfWidth, out float minX, out float maxX)
        {
            if (TryGetRoom(worldPos, out var room))
            {
                minX = room.min.x;
                maxX = room.max.x;
                return;
            }

            minX = worldPos.x - fallbackHalfWidth;
            maxX = worldPos.x + fallbackHalfWidth;
        }

        /// <summary>
        /// Hộp bao của căn phòng chứa `worldPos`. false nếu chỗ đó chưa đặt CameraBoundsZone nào.
        ///
        /// Dùng cho cả skill boss ("trải hết chiều ngang phòng") lẫn LEASH của elite (thôi đuổi khi player
        /// ra khỏi phòng) — cùng một khái niệm "căn phòng", nên cùng một nguồn số liệu.
        /// </summary>
        public static bool TryGetRoom(Vector2 worldPos, out Bounds room)
        {
            var zones = Object.FindObjectsByType<Attrition.Gameplay.Environment.CameraBoundsZone>(
                FindObjectsSortMode.None);

            Collider2D best = null;
            float bestArea = float.MaxValue;

            foreach (var z in zones)
            {
                if (z == null) continue;
                var col = z.GetComponent<Collider2D>();
                if (col == null) continue;
                if (!col.bounds.Contains(new Vector3(worldPos.x, worldPos.y, col.bounds.center.z))) continue;

                // Phòng lồng nhau (vùng lớn của khu + vùng nhỏ của phòng boss) → chọn vùng NHỎ NHẤT chứa
                // điểm này, vì đó mới là căn phòng thật sự.
                float area = col.bounds.size.x * col.bounds.size.y;
                if (area < bestArea) { bestArea = area; best = col; }
            }

            if (best != null) { room = best.bounds; return true; }

            room = default;
            return false;
        }
    }
}
