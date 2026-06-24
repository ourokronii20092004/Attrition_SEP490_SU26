using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Crossfade background theo ĐỘ CAO (liên tục, kiểu Afterimage chuyển vùng mượt). MỘT vùng duy nhất:
    /// player càng đi LÊN thì backgroundUnder càng MỜ, backgroundSurface càng RÕ — nội suy theo vị trí Y.
    ///
    /// Đặt component lên 1 GameObject có BoxCollider2D (IsTrigger) phủ khoảng chuyển tiếp dưới→trên.
    ///   - Đáy collider  = hoàn toàn Under (alpha Under=1, Surface=0).
    ///   - Đỉnh collider = hoàn toàn Surface (alpha Under=0, Surface=1).
    ///   - Ở giữa = pha trộn theo tỉ lệ.
    /// Local/visual thuần (mỗi máy tự xử lý theo player local của mình).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BackgroundHeightCrossfade : MonoBehaviour
    {
        [Tooltip("Background DƯỚI ĐẤT (rõ khi ở đáy vùng, mờ dần khi lên cao).")]
        [SerializeField] private GameObject backgroundUnder;
        [Tooltip("Background MẶT ĐẤT (mờ khi ở đáy, rõ dần khi lên cao).")]
        [SerializeField] private GameObject backgroundSurface;
        [Tooltip("Mượt hoá chuyển (0 = bám Y tức thì, lớn hơn = trễ mượt). Đơn vị: tốc độ alpha/giây.")]
        [SerializeField] private float smoothSpeed = 6f;

        [Header("---- MỐC ĐỘ CAO (Y world-space) ----")]
        [Tooltip("Đặt 2 mốc Y RÕ RÀNG thay vì dùng cạnh collider. Bật để fade chuẩn 0↔1.\n" +
                 "TẮT = dùng đáy/đỉnh collider (dễ lệch nếu nền đất không trùng đáy collider).")]
        [SerializeField] private bool useExplicitYMarkers = true;
        [Tooltip("Y mà player ĐỨNG ở tầng DƯỚI (tại đây Under hiện HOÀN TOÀN). Thường = cao độ nền hầm.")]
        [SerializeField] private float underY = 0f;
        [Tooltip("Y mà player ĐỨNG ở tầng TRÊN (tại đây Surface hiện HOÀN TOÀN). Thường = cao độ nền mặt đất.")]
        [SerializeField] private float surfaceY = 10f;

        private Collider2D _zone;
        private Transform _localPlayer;
        private float _blend; // 0 = hoàn toàn Under, 1 = hoàn toàn Surface

        private void Awake()
        {
            _zone = GetComponent<Collider2D>();
            _zone.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null || !pc.HasInputAuthority) return; // chỉ player local
            _localPlayer = pc.transform;
            if (backgroundUnder != null) backgroundUnder.SetActive(true);
            if (backgroundSurface != null) backgroundSurface.SetActive(true);
        }

        private void Start()
        {
            // Khởi tạo NGAY theo vị trí Y thật của player (player có thể spawn TRÊN MẶT ĐẤT, ngoài vùng
            // trigger → trước đây _blend=0 nên hiện nhầm Under). Tìm local player + chốt blend đúng từ đầu.
            TryFindLocalPlayer();
            _blend = ComputeTargetBlend();
            if (backgroundUnder != null) backgroundUnder.SetActive(true);
            if (backgroundSurface != null) backgroundSurface.SetActive(true);
        }

        private void TryFindLocalPlayer()
        {
            if (_localPlayer != null) return;
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc != null && pc.HasInputAuthority) { _localPlayer = pc.transform; break; }
            }
        }

        private void LateUpdate()
        {
            // Luôn bám player local (kể cả khi chưa từng vào trigger) → blend chuẩn theo độ cao ngay từ đầu.
            TryFindLocalPlayer();
            float target = ComputeTargetBlend();
            _blend = smoothSpeed > 0.01f
                ? Mathf.MoveTowards(_blend, target, smoothSpeed * Time.deltaTime)
                : target;

            ApplyAlpha(backgroundUnder, 1f - _blend);
            ApplyAlpha(backgroundSurface, _blend);
        }

        /// <summary>Tỉ lệ trộn theo độ cao player: tại underY=0 (Under), tại surfaceY=1 (Surface).</summary>
        private float ComputeTargetBlend()
        {
            if (_localPlayer == null) return _blend;
            float lo, hi;
            if (useExplicitYMarkers)
            {
                lo = underY; hi = surfaceY;
            }
            else
            {
                Bounds b = _zone.bounds;
                lo = b.min.y; hi = b.max.y;
            }
            return Mathf.Clamp01(Mathf.InverseLerp(lo, hi, _localPlayer.position.y));
        }

        private void ApplyAlpha(GameObject go, float a)
        {
            if (go == null) return;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == null) continue;
                var c = sr.color; c.a = a; sr.color = c;
            }
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Vector3 c = transform.position + (Vector3)box.offset;
                Vector3 s = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 1f);
                // Đáy xanh (Under) → đỉnh tím (Surface) để thấy hướng chuyển.
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
                Gizmos.DrawCube(c, s);
                Gizmos.color = new Color(0.6f, 0.3f, 0.9f, 0.9f);
                Gizmos.DrawWireCube(c, s);

                // 2 mốc Y: xanh = underY (Under hiện hẳn), tím = surfaceY (Surface hiện hẳn).
                if (useExplicitYMarkers)
                {
                    float halfW = s.x * 0.5f;
                    Gizmos.color = new Color(0.3f, 0.9f, 1f, 1f);
                    Gizmos.DrawLine(new Vector3(c.x - halfW, underY, 0f), new Vector3(c.x + halfW, underY, 0f));
                    Gizmos.color = new Color(0.7f, 0.4f, 1f, 1f);
                    Gizmos.DrawLine(new Vector3(c.x - halfW, surfaceY, 0f), new Vector3(c.x + halfW, surfaceY, 0f));
                }
            }
        }
    }
}
