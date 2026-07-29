using UnityEngine;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Hiệu ứng DASH: (1) KHÓI bụi ở chân mỗi lần dash — luôn có, kể cả chưa mở khoá shadow dash;
    /// (2) Afterimage kiểu Afterimage-game — CHỈ khi đã mở khoá shadow dash, spawn bản sao SPRITE mờ
    /// dần tại vị trí player → tạo vệt bóng; (3) chớp 1 nhịp sáng khi dash HỒI XONG để báo sẵn sàng.
    ///
    /// Chạy thuần cục bộ (Render/Update, KHÔNG networked): mỗi máy tự đọc trạng thái dash đã sync trên
    /// PlayerController rồi tự vẽ cho mọi player nhìn thấy. Gắn lên player prefab (cùng cấp
    /// PlayerController). Tự tìm SpriteRenderer trong con nếu không gán.
    /// </summary>
    public class ShadowDashEffect : MonoBehaviour
    {
        [Header("---- NGUỒN ----")]
        [Tooltip("SpriteRenderer của player để nhân bản. Bỏ trống = tự tìm trong con.")]
        [SerializeField] private SpriteRenderer sourceSprite;

        [Header("---- KHÓI DASH ----")]
        [Tooltip("Các frame khói theo thứ tự (sheet 'Free Smoke Fx  Pixel 04' _0.._3). Trống = tắt khói.")]
        [SerializeField] private Sprite[] smokeFrames;
        [Tooltip("Khoảng cách giữa 2 cụm khói trong lúc dash (giây). 0 = chỉ 1 cụm lúc bắt đầu.")]
        [SerializeField] private float smokeInterval = 0.07f;
        [Tooltip("Thời gian 1 cụm khói chạy hết 4 frame rồi tan (giây).")]
        [SerializeField] private float smokeLifetime = 0.28f;
        [Tooltip("Vị trí khói so với gốc player — mặc định ở chân (đáy collider ~ -1.2).")]
        [SerializeField] private Vector2 smokeOffset = new Vector2(-0.35f, -1.15f);
        [Tooltip("Phóng to cụm khói (sprite gốc chỉ ~21x9px).")]
        [SerializeField] private float smokeScale = 1.5f;
        [Tooltip("Màu/độ đậm khói.")]
        [SerializeField] private Color smokeColor = new Color(1f, 1f, 1f, 0.75f);

        [Header("---- AFTERIMAGE ----")]
        [Tooltip("Giãn cách spawn mỗi vệt bóng (giây).")]
        [SerializeField] private float spawnInterval = 0.04f;
        [Tooltip("Thời gian mỗi vệt tồn tại rồi mờ hết (giây).")]
        [SerializeField] private float ghostLifetime = 0.35f;
        [Tooltip("Màu vệt bóng (alpha = độ đậm ban đầu).")]
        [SerializeField] private Color ghostColor = new Color(0.4f, 0.5f, 1f, 0.6f);
        [Tooltip("Sorting order lệch so với sprite gốc (âm = phía sau).")]
        [SerializeField] private int sortingOffset = -1;

        [Header("---- BÁO HỒI CHIÊU ----")]
        [Tooltip("Màu chớp 1 nhịp khi dash vừa hồi xong (báo sẵn sàng dùng lại).")]
        [SerializeField] private Color readyFlashColor = new Color(0.6f, 0.75f, 1f, 1f);
        [Tooltip("Thời gian chớp báo hồi (giây).")]
        [SerializeField] private float readyFlashDuration = 0.18f;

        private PlayerController _pc;
        private float _nextGhostTime;
        private bool _prevReady = true;
        private float _flashUntil = -1f;
        private Color _srBaseColor;
        private float _nextSmokeTime;
        private bool _prevDashing;

        private void Awake()
        {
            _pc = GetComponent<PlayerController>();
            if (sourceSprite == null) sourceSprite = GetComponentInChildren<SpriteRenderer>();
            if (sourceSprite != null) _srBaseColor = sourceSprite.color;
        }

        private void LateUpdate()
        {
            if (_pc == null || sourceSprite == null) return;
            // Guard: IsDashing là [Networked] → đọc trước khi Spawned() sẽ ném InvalidOperationException.
            if (_pc.Object == null || !_pc.Object.IsValid) return;

            bool dashing = _pc.IsDashing;

            // ── KHÓI: luôn có, KHÔNG phụ thuộc shadow dash (dash cơ bản cũng phải bốc bụi) ──
            // Cụm đầu spawn ngay frame IsDashing bật lên, sau đó rải theo smokeInterval.
            if (dashing)
            {
                if (!_prevDashing) _nextSmokeTime = 0f;
                if (smokeInterval > 0f ? Time.time >= _nextSmokeTime : !_prevDashing)
                {
                    SpawnSmoke();
                    _nextSmokeTime = Time.time + smokeInterval;
                }
            }
            _prevDashing = dashing;

            // Dưới đây là phần SHADOW dash — chỉ khi đã mở khoá.
            if (!_pc.HasShadowDashAbility) return;

            // ── Afterimage trong lúc dash ──
            if (dashing)
            {
                if (Time.time >= _nextGhostTime)
                {
                    SpawnGhost();
                    _nextGhostTime = Time.time + spawnInterval;
                }
            }

            // ── Báo hồi chiêu: cooldown chuyển từ CHƯA hồi → ĐÃ hồi = chớp 1 nhịp ──
            bool ready = _pc.IsDashReady;
            if (ready && !_prevReady) _flashUntil = Time.time + readyFlashDuration;
            _prevReady = ready;

            ApplyReadyFlash();
        }

        /// <summary>
        /// Spawn 1 cụm khói ở CHÂN player, lật theo hướng dash và đặt LÙI về phía sau (khói bốc lên
        /// từ chỗ vừa đạp chân, không bay theo người). Dùng SpriteRenderer + đổi frame bằng tay thay vì
        /// Animator: chỉ 4 frame, không cần state machine/controller nào.
        /// </summary>
        private void SpawnSmoke()
        {
            if (smokeFrames == null || smokeFrames.Length == 0) return;

            bool right = _pc.IsFacingRight;
            var go = new GameObject("DashSmoke");

            // offset.x là khoảng LÙI phía sau → đảo dấu theo hướng mặt.
            var pos = transform.position
                      + new Vector3(right ? smokeOffset.x : -smokeOffset.x, smokeOffset.y, 0f);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(right ? smokeScale : -smokeScale, smokeScale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = smokeFrames[0];
            sr.sortingLayerID = sourceSprite.sortingLayerID;
            sr.sortingOrder = sourceSprite.sortingOrder + sortingOffset;
            sr.color = smokeColor;

            go.AddComponent<DashSmokeAnim>().Init(smokeFrames, smokeLifetime, smokeColor);
        }

        private void SpawnGhost()
        {
            var go = new GameObject("DashGhost");
            go.transform.SetPositionAndRotation(sourceSprite.transform.position, sourceSprite.transform.rotation);
            go.transform.localScale = sourceSprite.transform.lossyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sourceSprite.sprite;
            sr.flipX = sourceSprite.flipX;
            sr.flipY = sourceSprite.flipY;
            sr.sortingLayerID = sourceSprite.sortingLayerID;
            sr.sortingOrder = sourceSprite.sortingOrder + sortingOffset;
            sr.color = ghostColor;

            var fade = go.AddComponent<DashGhostFade>();
            fade.Init(ghostColor, ghostLifetime);
        }

        private void ApplyReadyFlash()
        {
            if (_flashUntil < 0f) return;
            if (Time.time < _flashUntil)
            {
                sourceSprite.color = readyFlashColor;
            }
            else
            {
                sourceSprite.color = _srBaseColor;
                _flashUntil = -1f;
            }
        }
    }

    /// <summary>
    /// Chạy 4 frame khói rồi tan + huỷ. KHÔNG dùng Animator vì chỉ có 1 chuỗi frame thẳng — Animator
    /// sẽ kéo theo controller + state machine cho việc mà 10 dòng này làm xong.
    /// Alpha mờ dần ở NỬA SAU vòng đời để khói tan mượt chứ không mất đột ngột.
    /// </summary>
    public class DashSmokeAnim : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private Color _base;
        private float _life;
        private float _t;

        public void Init(Sprite[] frames, float life, Color baseColor)
        {
            _sr = GetComponent<SpriteRenderer>();
            _frames = frames;
            _life = Mathf.Max(0.01f, life);
            _base = baseColor;
        }

        private void Update()
        {
            if (_sr == null || _frames == null || _frames.Length == 0) { Destroy(gameObject); return; }

            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);

            // Frame theo tiến trình; kẹp ở frame cuối để không tràn mảng khi k == 1.
            int i = Mathf.Min(_frames.Length - 1, (int)(k * _frames.Length));
            _sr.sprite = _frames[i];

            var c = _base;
            c.a = _base.a * (k < 0.5f ? 1f : 1f - (k - 0.5f) * 2f);
            _sr.color = c;

            if (_t >= _life) Destroy(gameObject);
        }
    }

    /// <summary>Tự mờ dần rồi huỷ 1 vệt bóng afterimage. Tách riêng để mỗi ghost tự quản vòng đời.</summary>
    public class DashGhostFade : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Color _start;
        private float _life;
        private float _t;

        public void Init(Color start, float life)
        {
            _sr = GetComponent<SpriteRenderer>();
            _start = start;
            _life = Mathf.Max(0.01f, life);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_t / _life);
            if (_sr != null)
            {
                var c = _start;
                c.a = _start.a * k;
                _sr.color = c;
            }
            if (_t >= _life) Destroy(gameObject);
        }
    }
}
