using UnityEngine;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Hiệu ứng SHADOW DASH kiểu Afterimage: trong lúc dash (chỉ khi đã mở khoá shadow dash), spawn các
    /// bản sao SPRITE mờ dần tại vị trí player → tạo vệt bóng. Khi dash HỒI XONG (cooldown hết) sau khi
    /// vừa dùng, chớp 1 nhịp sáng ở sprite để báo "dash đã sẵn sàng".
    ///
    /// Chạy thuần cục bộ (Render/Update, KHÔNG networked): mỗi máy tự đọc trạng thái dash đã sync trên
    /// PlayerController rồi tự vẽ afterimage cho mọi player nhìn thấy. Gắn lên player prefab (cùng cấp
    /// PlayerController). Tự tìm SpriteRenderer trong con nếu không gán.
    /// </summary>
    public class ShadowDashEffect : MonoBehaviour
    {
        [Header("---- NGUỒN ----")]
        [Tooltip("SpriteRenderer của player để nhân bản. Bỏ trống = tự tìm trong con.")]
        [SerializeField] private SpriteRenderer sourceSprite;

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

        private void Awake()
        {
            _pc = GetComponent<PlayerController>();
            if (sourceSprite == null) sourceSprite = GetComponentInChildren<SpriteRenderer>();
            if (sourceSprite != null) _srBaseColor = sourceSprite.color;
        }

        private void LateUpdate()
        {
            if (_pc == null || sourceSprite == null) return;
            if (!_pc.HasShadowDashAbility) return; // chưa mở khoá → không có hiệu ứng shadow

            bool dashing = _pc.IsDashing;

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
