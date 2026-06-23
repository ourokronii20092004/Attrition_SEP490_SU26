using UnityEngine;
using Unity.Cinemachine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Gắn script này cùng với một BoxCollider2D hoặc PolygonCollider2D (IsTrigger = true).
    /// Kéo dãn Collider này bao phủ toàn bộ giới hạn của căn phòng.
    /// Khi Player bước vào, Camera sẽ bị giới hạn không được đi ra khỏi vùng này.
    /// (Giống phong cách Hollow Knight)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CameraBoundsZone : MonoBehaviour
    {
        private Collider2D _boundsCollider;
        private CinemachineConfiner2D _confiner;

        private static CinemachineCamera _cachedCamera;

        private void Awake()
        {
            _boundsCollider = GetComponent<Collider2D>();
            _boundsCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Kiểm tra xem có phải là local player không
            if (other.CompareTag("Player"))
            {
                var player = other.GetComponentInParent<PlayerController>();
                // Chỉ set bounds cho người chơi Local (có màn hình của mình)
                if (player != null && player.HasInputAuthority)
                {
                    UpdateCameraBounds();
                }
            }
        }

        private void UpdateCameraBounds()
        {
            // Cache lại Camera để tránh việc tìm kiếm gây lag (nhất là ở đầu game)
            if (_cachedCamera == null)
            {
                _cachedCamera = FindAnyObjectByType<CinemachineCamera>();
            }

            if (_cachedCamera != null)
            {
                // Tìm hoặc thêm Confiner2D
                if (_confiner == null)
                {
                    _confiner = _cachedCamera.GetComponent<CinemachineConfiner2D>();
                    if (_confiner == null)
                    {
                        _confiner = _cachedCamera.gameObject.AddComponent<CinemachineConfiner2D>();
                    }
                }

                // Nếu đang là bounds hiện tại thì không cần tính toán lại
                if (_confiner.BoundingShape2D == _boundsCollider) return;

                // Gán Collider của vùng này làm giới hạn cho Camera
                _confiner.BoundingShape2D = _boundsCollider;
                
                // Khởi tạo lại bộ đệm tính toán để Confiner áp dụng ngay lập tức
                _confiner.InvalidateBoundingShapeCache();
            }
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Màu vàng nhạt
                if (col is BoxCollider2D box)
                {
                    Vector3 center = transform.position + (Vector3)box.offset;
                    Vector3 size = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, transform.lossyScale.z);
                    Gizmos.DrawCube(center, size);
                    Gizmos.color = new Color(1f, 1f, 0f, 1f);
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }
    }
}
