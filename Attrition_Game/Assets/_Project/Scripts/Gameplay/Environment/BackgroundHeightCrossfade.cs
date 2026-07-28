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

        [Header("---- PHẠM VI ROOM (tùy chọn) ----")]
        [Tooltip("TOÀN SCENE: cả map chia 2 tầng theo Y (dưới underY = hầm, trên surfaceY = mặt đất). " +
                 "Bật cái này → CHỈ CẦN 1 component cho cả scene, áp background theo Y ở MỌI NƠI " +
                 "(teleport/đi tới room nào cũng đúng). Bỏ qua roomBounds + dải X.")]
        [SerializeField] private bool wholeScene = true;
        [Tooltip("(Chỉ khi TẮT wholeScene) Collider phủ CẢ ROOM mà background này áp dụng. " +
                 "Bỏ trống = dùng dải X của collider vùng chuyển tiếp.")]
        [SerializeField] private Collider2D roomBounds;

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
        private bool _initialSnapped;  // đã chốt blend ban đầu theo player thật chưa

        private void Awake()
        {
            _zone = GetComponent<Collider2D>();
            _zone.isTrigger = true;
        }

        private void Start()
        {
            TryFindLocalPlayer();
            InitialSnapIfReady();
        }

        // Chốt blend ban đầu theo VỊ TRÍ Y THẬT của player (dù trong hay ngoài vùng), 1 lần khi đã có
        // player → load ở mặt đất hiện surface, ở dưới hiện under. Player networked có thể spawn muộn
        // nên gọi lại trong LateUpdate tới khi thành công.
        private void InitialSnapIfReady()
        {
            if (_initialSnapped || _localPlayer == null) return;
            // Mới vào game: chỉ chốt khi player THUỘC phạm vi room này (tránh áp background room này khi
            // player đang ở room khác). Ngoài scope → chờ, snap khi player vào (qua LateUpdate).
            if (!PlayerInScope()) { _initialSnapped = true; return; }
            float t = ComputeTargetBlend();
            _blend = t >= 0.5f ? 1f : 0f;
            ApplyAlpha(backgroundUnder, 1f - _blend);
            ApplyAlpha(backgroundSurface, _blend);
            _initialSnapped = true;
        }

        private void TryFindLocalPlayer()
        {
            if (_localPlayer != null) return;
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc != null && pc.HasInputAuthority) { _localPlayer = pc.transform; break; }
            }
        }

        // CHỈ tác động khi player local nằm trong DẢI X của vùng (cùng "cột" với khu này) — bất kể Y.
        // Như vậy: teleport lên mặt đất (cùng X, Y cao) vẫn đổi đúng; sang room khác (khác X) thì không.
        // Player có thuộc PHẠM VI background này không?
        //  - Có roomBounds → player nằm trong room đó (X+Y) → áp dụng (teleport tới đâu trong room cũng đúng).
        //  - Không → fallback: cùng DẢI X của vùng chuyển tiếp nhỏ.
        private bool PlayerInScope()
        {
            if (_localPlayer == null) return false;
            if (wholeScene) return true;                 // toàn scene: áp theo Y ở mọi nơi
            if (roomBounds != null)
                return roomBounds.OverlapPoint(_localPlayer.position);
            if (_zone == null) return false;
            var b = _zone.bounds;
            float x = _localPlayer.position.x;
            return x >= b.min.x && x <= b.max.x;
        }

        private void LateUpdate()
        {
            TryFindLocalPlayer();
            if (!_initialSnapped) { InitialSnapIfReady(); return; } // chờ player spawn để chốt đúng tầng

            // Chỉ xử lý khi player thuộc phạm vi background này. Ngoài → không đụng (room/khu khác lo).
            if (!PlayerInScope()) return;

            float target = ComputeTargetBlend();
            _blend = smoothSpeed > 0.01f
                ? Mathf.MoveTowards(_blend, target, smoothSpeed * Time.deltaTime)
                : target;

            // Blend đứng yên (đa số thời gian: player không đổi độ cao) → khỏi ghi lại màu cho mọi sprite.
            if (Mathf.Abs(_blend - _appliedBlend) < 0.002f) return;
            _appliedBlend = _blend;

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

        // Cache renderer để không gọi GetComponentsInChildren (cấp phát mảng) mỗi frame — đây là rác GC
        // đều đặn mỗi frame, đúng loại chi phí gây hitch dồn cục khi GC chạy.
        private SpriteRenderer[] _underSr, _surfaceSr;
        private float _appliedBlend = -1f;

        private void ApplyAlpha(GameObject go, float a)
        {
            if (go == null) return;
            var list = go == backgroundUnder
                ? (_underSr ??= go.GetComponentsInChildren<SpriteRenderer>(true))
                : (_surfaceSr ??= go.GetComponentsInChildren<SpriteRenderer>(true));
            for (int i = 0; i < list.Length; i++)
            {
                var sr = list[i];
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
