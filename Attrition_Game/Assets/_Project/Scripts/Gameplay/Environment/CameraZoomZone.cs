using UnityEngine;
using Unity.Cinemachine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Vùng ZOOM camera (kiểu Hollow Knight: phòng boss kéo xa để dễ quan sát).
    /// Gắn cùng 1 Collider2D (IsTrigger). Khi LOCAL player vào vùng → camera lerp tới zoomedSize
    /// (ortho lớn hơn = nhìn xa hơn). Rời vùng → trả về defaultSize.
    ///
    /// Chỉ đổi OrthographicSize của CinemachineCamera (local, không cần networked — mỗi client tự zoom
    /// camera của mình). Mượt bằng lerp trong Update.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CameraZoomZone : MonoBehaviour
    {
        [Tooltip("Cỡ ortho khi ở trong vùng (lớn hơn = zoom out, nhìn rộng hơn). Phòng boss nên ~8-10.")]
        [SerializeField] private float zoomedSize = 9f;
        [Tooltip("GIỚI HẠN ortho TỐI ĐA cứng (theo PHÒNG THẬT). Camera sẽ KHÔNG zoom quá mức này dù bound rộng " +
                 "hơn. Đặt = nửa chiều cao vùng phòng thật (vd phòng cao 14 units → 7). 0 = không giới hạn cứng.")]
        [SerializeField] private float maxZoomHardCap = 0f;
        [Tooltip("Cỡ ortho mặc định khi rời vùng. 0 = tự lấy cỡ hiện tại của camera lúc lần đầu vào vùng.")]
        [SerializeField] private float defaultSize = 0f;
        [Tooltip("Tốc độ chuyển zoom (đơn vị ortho / giây).")]
        [SerializeField] private float lerpSpeed = 4f;
        [Tooltip("Collider GIỚI HẠN của phòng (thường là CameraBoundsZone). Gán để clamp zoom KHÔNG lộ ra ngoài. " +
                 "Bỏ trống = thử dùng confiner runtime (kém tin cậy).")]
        [SerializeField] private Collider2D roomBounds;
        [Tooltip("Tỉ lệ khung hình (width/height) của game để clamp zoom theo chiều ngang. 16:9 = 1.777, 4:3 = 1.333.")]
        [SerializeField] private float aspect = 16f / 9f;

        private static CinemachineCamera _cachedCamera;
        private static CinemachineConfiner2D _cachedConfiner;
        private bool _inside;
        private float _targetSize;
        private bool _hasDefault;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
            if (defaultSize > 0f) { _targetSize = defaultSize; _hasDefault = true; }

            // Tự tìm bound phòng nếu chưa gán: lấy CameraBoundsZone nào BAO TRÙM tâm zone này.
            if (roomBounds == null)
            {
                Vector2 center = (Vector2)transform.position + col.offset;
                CameraBoundsZone best = null; float bestArea = float.MaxValue;
                foreach (var bz in FindObjectsByType<CameraBoundsZone>(FindObjectsSortMode.None))
                {
                    var bc = bz.GetComponent<Collider2D>();
                    if (bc == null || !bc.bounds.Contains(center)) continue;
                    float area = bc.bounds.size.x * bc.bounds.size.y; // chọn bound nhỏ nhất chứa zone (phòng sát nhất)
                    if (area < bestArea) { bestArea = area; best = bz; }
                }
                if (best != null) roomBounds = best.GetComponent<Collider2D>();
            }
        }

        /// <summary>
        /// Zoom KHÔNG được vượt quá vùng giới hạn phòng → camera không lộ ra ngoài.
        /// Ưu tiên roomBounds gán sẵn (đáng tin); nếu trống thì thử confiner runtime.
        /// Clamp theo CẢ nửa chiều cao (orthoSize ≤ extents.y) lẫn nửa chiều ngang (orthoSize ≤ extents.x/aspect).
        /// </summary>
        private float ClampZoomToBounds(float desired)
        {
            // 1. Giới hạn cứng theo phòng thật (nếu đặt) — quan trọng nhất khi bound vẽ rộng hơn phòng.
            if (maxZoomHardCap > 0.1f) desired = Mathf.Min(desired, maxZoomHardCap);

            // 2. Giới hạn theo bound camera (nếu có) — tránh lộ ngoài vùng confiner.
            Bounds b;
            if (roomBounds != null) b = roomBounds.bounds;
            else if (_cachedConfiner != null && _cachedConfiner.BoundingShape2D != null) b = _cachedConfiner.BoundingShape2D.bounds;
            else return desired;

            float a = aspect > 0.01f ? aspect : (16f / 9f);
            float maxByHeight = b.extents.y;
            float maxByWidth = b.extents.x / a;
            float maxAllowed = Mathf.Min(maxByHeight, maxByWidth) - 0.05f;
            if (maxAllowed <= 0.1f) return desired;
            return Mathf.Min(desired, maxAllowed);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.HasInputAuthority) return;

            EnsureCamera();
            // ÉP confiner dùng bound phòng này NGAY — không chờ CameraBoundsZone (tránh camera follow
            // player ra sát mép rồi lộ ngoài khi confiner chưa kịp set/bound sai).
            ApplyConfinerBounds();
            // Lần đầu vào vùng: nếu chưa chốt defaultSize, lấy cỡ hiện tại làm mốc trả về.
            if (!_hasDefault && _cachedCamera != null)
            {
                defaultSize = _cachedCamera.Lens.OrthographicSize;
                _hasDefault = true;
            }
            _inside = true;
            _targetSize = ClampZoomToBounds(zoomedSize);
        }

        /// <summary>Ép CinemachineConfiner2D dùng roomBounds của phòng này (thêm component nếu chưa có) + bake lại.</summary>
        private void ApplyConfinerBounds()
        {
            if (_cachedCamera == null || roomBounds == null) return;
            if (_cachedConfiner == null)
                _cachedConfiner = _cachedCamera.GetComponent<CinemachineConfiner2D>()
                                  ?? _cachedCamera.gameObject.AddComponent<CinemachineConfiner2D>();
            if (_cachedConfiner.BoundingShape2D != roomBounds)
            {
                _cachedConfiner.BoundingShape2D = roomBounds;
                _cachedConfiner.InvalidateBoundingShapeCache();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.HasInputAuthority) return;

            _inside = false;
            _targetSize = _hasDefault ? defaultSize : zoomedSize;
        }

        private void Update()
        {
            // Refresh tham chiếu mỗi frame: CinemachineConfiner2D có thể được CameraBoundsZone thêm
            // vào lúc RUNTIME (sau khi zone này đã cache null) → phải tìm lại để clamp hoạt động.
            EnsureCamera();
            if (_cachedCamera == null) return;

            float cur = _cachedCamera.Lens.OrthographicSize;
            float zoomTarget = ClampZoomToBounds(zoomedSize);
            float target = _inside ? zoomTarget : (_hasDefault ? defaultSize : cur);
            if (Mathf.Abs(cur - target) < 0.01f) return;

            float next = Mathf.MoveTowards(cur, target, lerpSpeed * Time.deltaTime);
            var lens = _cachedCamera.Lens;
            lens.OrthographicSize = next;
            _cachedCamera.Lens = lens;
        }

        private static void EnsureCamera()
        {
            if (_cachedCamera == null) _cachedCamera = FindAnyObjectByType<CinemachineCamera>();
            // Confiner có thể chưa tồn tại lúc đầu (CameraBoundsZone AddComponent khi player vào bound).
            if (_cachedConfiner == null && _cachedCamera != null)
                _cachedConfiner = _cachedCamera.GetComponent<CinemachineConfiner2D>();
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.18f);
                Vector3 center = transform.position + (Vector3)box.offset;
                Vector3 size = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 1f);
                Gizmos.DrawCube(center, size);
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
