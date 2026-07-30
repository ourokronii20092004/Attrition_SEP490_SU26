using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Hiệu ứng NHẤP NHÔ lên xuống cho vật phẩm nằm trên sàn (item rơi ra, bình HP giấu trong map).
    ///
    /// VÌ SAO KHÔNG DI CHUYỂN THẲNG transform NÀY: `DroppedItem` có `NetworkTransform`, nó GHI ĐÈ
    /// `transform.position` mỗi frame Render từ state networked — mọi thay đổi cục bộ sẽ bị xoá ngay,
    /// hoặc tệ hơn: nếu đổi ở host thì vị trí nhấp nhô bị sync qua mạng thành rung giật cho client.
    /// Vì vậy chỉ nhấp nhô phần HÌNH (một child riêng), giữ nguyên gốc networked.
    ///
    /// Thuần cục bộ (Update, MonoBehaviour): mỗi máy tự vẽ, không tốn băng thông. Lệch pha theo vị trí
    /// nên nhiều item cạnh nhau không nhô lên đồng loạt trông như máy móc.
    ///
    /// Gắn lên cùng GameObject với DroppedItem / PickupItem. Nếu sprite nằm ở ROOT (trường hợp
    /// DroppedItem: `[RequireComponent(typeof(SpriteRenderer))]`), component tự tạo child "BobVisual" và
    /// chuyển việc vẽ sang đó.
    /// </summary>
    public class FloatBobEffect : MonoBehaviour
    {
        [Header("---- NHẤP NHÔ ----")]
        [Tooltip("Biên độ lên/xuống (units). 0.18 ≈ 3px ở PPU 16 — thấy rõ mà không trông như đang bay.")]
        [SerializeField] private float amplitude = 0.18f;
        [Tooltip("Số nhịp mỗi giây.")]
        [SerializeField] private float frequency = 1.1f;
        [Tooltip("Nâng cả cụm lên bấy nhiêu để đáy không lún vào sàn khi ở pha thấp nhất.")]
        [SerializeField] private float baseLift = 0.18f;

        [Header("---- XOAY NHẸ (tùy chọn) ----")]
        [Tooltip("Góc lắc tối đa (độ). 0 = không lắc.")]
        [SerializeField] private float tiltDegrees = 0f;

        /// <summary>Transform sẽ nhấp nhô. Gán tay nếu prefab đã có child hình riêng; bỏ trống = tự lo.</summary>
        [SerializeField] private Transform visual;

        private Vector3 _baseLocalPos;
        private float _phase;

        private void Awake()
        {
            if (visual == null) visual = ResolveVisual();
            if (visual != null) _baseLocalPos = visual.localPosition;

            // Lệch pha theo toạ độ: hai item cạnh nhau không nhô lên cùng lúc.
            _phase = (transform.position.x * 1.7f + transform.position.y * 0.9f) % (Mathf.PI * 2f);
        }

        /// <summary>
        /// Tìm child để nhấp nhô. Sprite ở root → tạo child "BobVisual", copy thông tin vẽ sang đó rồi TẮT
        /// renderer gốc (không xoá: DroppedItem.Spawned() vẫn gán `sprite` vào renderer gốc, ta mirror lại).
        /// </summary>
        private Transform ResolveVisual()
        {
            var existing = transform.Find("BobVisual");
            if (existing != null) return existing;

            // Có child nào đã mang SpriteRenderer sẵn? Dùng luôn, không tạo thêm.
            foreach (Transform child in transform)
                if (child.GetComponent<SpriteRenderer>() != null) return child;

            _rootRenderer = GetComponent<SpriteRenderer>();
            if (_rootRenderer == null) return null;   // không có hình → không nhấp nhô gì

            var go = new GameObject("BobVisual");
            go.transform.SetParent(transform, false);

            _mirrorRenderer = go.AddComponent<SpriteRenderer>();
            _mirrorRenderer.sprite = _rootRenderer.sprite;
            _mirrorRenderer.color = _rootRenderer.color;
            _mirrorRenderer.sortingLayerID = _rootRenderer.sortingLayerID;
            _mirrorRenderer.sortingOrder = _rootRenderer.sortingOrder;
            _mirrorRenderer.flipX = _rootRenderer.flipX;

            _rootRenderer.enabled = false;   // tránh vẽ 2 lần
            return go.transform;
        }

        private SpriteRenderer _rootRenderer;
        private SpriteRenderer _mirrorRenderer;

        private void LateUpdate()
        {
            if (visual == null) return;

            // Mirror sprite: DroppedItem gán icon trong Spawned() (sau Awake) nên phải đồng bộ tiếp.
            if (_mirrorRenderer != null && _rootRenderer != null
                && _mirrorRenderer.sprite != _rootRenderer.sprite)
                _mirrorRenderer.sprite = _rootRenderer.sprite;

            float t = Time.time * frequency * Mathf.PI * 2f + _phase;
            float y = baseLift + Mathf.Sin(t) * amplitude;
            visual.localPosition = _baseLocalPos + new Vector3(0f, y, 0f);

            if (tiltDegrees > 0.01f)
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * tiltDegrees);
        }
    }
}
