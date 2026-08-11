using UnityEngine;
using TMPro;
using Attrition.Controllers;
using Attrition.Persistence;

namespace Attrition.Gameplay.Enemy
{
    /// <summary>
    /// UI thế-giới gắn trên quái: thanh máu TRÊN ĐẦU + số sát thương nổi khi bị đánh.
    /// Tự dựng runtime (không cần prefab). Đọc HP từ EnemyController, chạy trên mọi máy.
    /// Số sát thương chỉ hiện khi GameSettings.ShowDamageNumbers bật.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyWorldUI : MonoBehaviour
    {
        [Header("---- VỊ TRÍ ----")]
        [Tooltip("Khoảng hở giữa ĐỈNH sprite quái và đáy thanh máu (units).")]
        [SerializeField] private float barHeadGap = 0.3f;
        [Tooltip("Kích thước thanh máu trước khi nhân hệ số theo tier.")]
        [SerializeField] private Vector2 barSize = new Vector2(1.6f, 0.28f);

        private EnemyController _enemy;
        private EnemyStats _stats;
        private Transform _barRoot;
        private Transform _fill;
        private Transform _trailFill;
        private float _shownFraction = 1f;
        private float _trailFraction = 1f;
        private bool _everDamaged;
        private Transform _nameLabel;
        private float _headLocalY;
        private float _barScale = 1f;

        private void Awake()
        {
            _enemy = GetComponent<EnemyController>();
            _stats = GetComponent<EnemyStats>();

            // Đo TRƯỚC khi tạo quad con: HeadLocalY quét SpriteRenderer trong children nên dựng
            // thanh máu trước sẽ khiến nó tự bắt vào chính mình.
            _headLocalY = HeadLocalY();

            BuildBar();
            BuildNameLabel();
        }

        /// <summary>
        /// MaxHP để tính tỉ lệ thanh máu. EnemyController.maxHealth KHÔNG networked nên trên client
        /// luôn = 1 (default) → thanh máu client kẹt đầy. EnemyStats.MaxHP là [Networked] → đồng bộ
        /// đúng cả 2 máy. Ưu tiên MaxHP networked, fallback maxHealth (prefab cũ / chưa có stats).
        /// </summary>
        private int MaxHpForBar()
        {
            if (_stats != null && _stats.MaxHP > 0) return _stats.MaxHP;
            return Mathf.Max(1, _enemy.maxHealth);
        }

        private void BuildNameLabel()
        {
            // Nguồn tên: EnemyStats.EnemyId ("axe_demon") → "Axe Demon"; fallback tên gameobject.
            var stats = GetComponent<EnemyStats>();
            string raw = stats != null && !string.IsNullOrEmpty(stats.EnemyId) ? stats.EnemyId : name;
            string display = WorldNameLabel.Prettify(raw);

            // Boss to + đỏ, elite cam, thường xám nhạt.
            var tier = stats != null ? stats.Tier : Attrition.Data.EnemyTier.Normal;
            Color color; float size;
            switch (tier)
            {
                case Attrition.Data.EnemyTier.Boss:  color = new Color(1f, 0.4f, 0.35f); size = 5.2f; break;
                case Attrition.Data.EnemyTier.Elite: color = new Color(1f, 0.7f, 0.3f);  size = 4.6f; break;
                default:                             color = new Color(0.85f, 0.82f, 0.78f); size = 3f; break;
            }

            // Tên nằm NGAY TRÊN thanh máu. Chiều cao thanh trong local space = barSize.y * mul / scaleY
            // (xem NormalizedScale), nên phải chia lại cho scale của quái mới không đè lên thanh.
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            if (scaleY < 0.0001f) scaleY = 1f;
            float nameY = _headLocalY + (barSize.y * _barScale + 0.18f) / scaleY;
            var labelObj = WorldNameLabel.Attach(transform, display, new Vector3(0f, nameY, 0f), color, size);
            if (labelObj != null) _nameLabel = labelObj.transform;
        }

        private void BuildBar()
        {
            // Root world-space (sprite-based, không cần Canvas → rẻ và luôn quay mặt camera 2D)
            var root = new GameObject("HealthBar");
            _barRoot = root.transform;
            _barRoot.SetParent(transform, false);

            var stats = GetComponent<EnemyStats>();
            var tier = stats != null ? stats.Tier : Attrition.Data.EnemyTier.Normal;

            // Boss to nhất, elite to hơn quái thường. Thanh máu giờ nằm TRÊN ĐẦU mọi tier
            // (trước đây boss/quái thường đặt dưới chân → chìm trong tile sàn, không nhìn thấy).
            _barScale = tier switch
            {
                Attrition.Data.EnemyTier.Boss  => 2.6f,
                Attrition.Data.EnemyTier.Elite => 1.9f,
                _                              => 1.4f,
            };

            _barRoot.localPosition = new Vector3(0f, _headLocalY, 0f);
            _barRoot.localScale = NormalizedScale(_barScale);

            // Boss luôn hiện thanh máu; quái thường/elite ẩn tới khi bị đánh lần đầu.
            bool alwaysVisible = tier == Attrition.Data.EnemyTier.Boss;
            _everDamaged = alwaysVisible;
            _barRoot.gameObject.SetActive(alwaysVisible);

            // Fill/Trail là EM của BG, KHÔNG phải con: CreateQuad đặt kích thước bằng localScale, nên
            // làm con của BG sẽ bị nhân dồn scale (rộng 1.6 × 1.56 = 2.5 → tràn hẳn ra ngoài khung đen,
            // đúng lỗi "khung đen ngắn hơn thanh máu"). Là em thì cùng hệ toạ độ _barRoot, sortingOrder
            // vẫn lo thứ tự vẽ.
            CreateQuad("BG", _barRoot, new Color(0f, 0f, 0f, 0.75f), barSize, 0);
            var fillSize = new Vector2(barSize.x - 0.04f, barSize.y - 0.04f);
            _trailFill = CreateQuad("Trail", _barRoot, new Color(1f, 0.85f, 0.3f, 1f), fillSize, 1).transform;
            _fill = CreateQuad("Fill", _barRoot, new Color(0.78f, 0.15f, 0.15f, 1f), fillSize, 2).transform;
        }

        /// <summary>
        /// Scale local để thanh máu có kích thước THẬT (world units) bằng barSize * mul, bất kể quái
        /// được scale bao nhiêu.
        ///
        /// VÌ SAO CẦN: thanh máu là con của quái nên nó thừa hưởng localScale của quái, mà scale này
        /// chênh nhau tới 5 lần giữa các prefab (Cultist 0.32 / Gollux 0.82 / Crab 1.72). Dùng chung
        /// một hệ số nhân thì cùng "elite" mà Cultist ra thanh nhỏ hơn Crab 5 lần — đúng lỗi "elite
        /// thanh máu quá nhỏ". Chia lại cho scale của quái → mọi con cùng tier ra thanh bằng nhau.
        /// </summary>
        private Vector3 NormalizedScale(float mul)
        {
            var ls = transform.lossyScale;
            float sx = Mathf.Abs(ls.x) < 0.0001f ? 1f : Mathf.Abs(ls.x);
            float sy = Mathf.Abs(ls.y) < 0.0001f ? 1f : Mathf.Abs(ls.y);
            return new Vector3(mul / sx, mul / sy, 1f);
        }

        /// <summary>
        /// Y (local) ngay TRÊN ĐẦU quái, suy từ bounds sprite thật thay vì hằng số.
        /// Quái elite cao gấp mấy lần quái thường nên một offset cố định sẽ rơi vào giữa bụng elite.
        /// Gọi TRƯỚC khi tạo các quad con để không tự bắt vào thanh máu.
        /// </summary>
        private float HeadLocalY()
        {
            float highest = float.MinValue;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                if (sr != null && sr.sprite != null) highest = Mathf.Max(highest, sr.bounds.max.y);

            // Không có sprite → dựa vào collider thân, cuối cùng mới tới hằng số.
            if (highest == float.MinValue)
            {
                var col = GetComponentInChildren<Collider2D>();
                if (col != null) highest = col.bounds.max.y;
            }
            if (highest == float.MinValue) return 1.2f;

            // bounds là world-space; localPosition nằm trong local space của quái (đã bị localScale
            // của quái nhân vào) → chia lại cho scale, chặn chia 0.
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            if (scaleY < 0.0001f) return 1.2f;

            return (highest - transform.position.y) / scaleY + barHeadGap;
        }

        private GameObject CreateQuad(string name, Transform parent, Color color, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = color;
            WorldNameLabel.SetTopSortingLayer(sr, 50 + order);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return go;
        }

        private static Sprite _white;
        private static Sprite WhiteSprite()
        {
            if (_white != null) return _white;
            var tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            return _white;
        }

        /// <summary>Gọi từ EnemyController.RPC_NotifyDamageTaken trên mọi máy.</summary>
        public void OnDamaged(int amount)
        {
            _everDamaged = true;
            if (_barRoot != null) _barRoot.gameObject.SetActive(true);

            if (GameSettings.ShowDamageNumbers && amount > 0)
                SpawnPopup(amount);
        }

        private void SpawnPopup(int amount)
        {
            var go = new GameObject("DmgPopup");
            // Nổi lên từ ngay trên đầu quái (cùng mốc với thanh máu) thay vì offset cố định.
            go.transform.position = transform.position
                + Vector3.up * (_headLocalY * Mathf.Abs(transform.lossyScale.y) + 0.45f);
            var tm = go.AddComponent<TextMeshPro>();
            tm.text = amount.ToString();
            tm.fontSize = 4f;
            tm.alignment = TextAlignmentOptions.Center;
            tm.color = new Color(1f, 0.85f, 0.3f, 1f);
            WorldNameLabel.SetTopSortingLayer(tm.renderer, 60);
            go.AddComponent<FloatingNumber>();
        }

        // Chữ ký frame trước: thanh máu chỉ đổi khi HP đổi / trail đang chạy. Ghi lại các transform
        // (mỗi con quái, mỗi frame) khi chẳng có gì đổi là chi phí thuần vô ích — Unity còn phải đánh
        // dấu transform dirty và đồng bộ lại toàn bộ hierarchy đó.
        private float _lastSig = float.NaN;
        private float _lastSignX;

        private void Update()
        {
            if (_enemy == null) return;

            // CHỐNG LẬT: quái quay mặt bằng cách lật localScale.x âm (xem EnemyAnimation.FaceDirection),
            // con của nó — nhãn tên + thanh máu — bị lộn ngược trái phải theo. Phải xử lý ĐỘC LẬP với
            // thanh máu: nhãn tên hiện ngay từ lúc spawn, nên nếu gộp vào nhánh _everDamaged bên dưới
            // thì quái chưa bị đánh mà đang nhìn sang trái sẽ hiện tên viết ngược.
            float signXNow = Mathf.Sign(transform.localScale.x);
            if (signXNow != _lastSignX)
            {
                _lastSignX = signXNow;
                UnflipX(_nameLabel, signXNow);
                UnflipX(_barRoot, signXNow);
            }

            if (!_everDamaged || _fill == null) return;

            int max = MaxHpForBar();
            float target = Mathf.Clamp01((float)_enemy.CurrentHealth / max);

            float sig = target * 4096f + _trailFraction * 16f;
            if (sig == _lastSig) return;
            _lastSig = sig;

            _shownFraction = target;
            _trailFraction = Mathf.MoveTowards(_trailFraction, target, Time.deltaTime * 1.5f);

            float w = (barSize.x - 0.04f) * _shownFraction;
            _fill.localScale = new Vector3(w, barSize.y - 0.04f, 1f);
            // neo trái: dịch theo nửa phần hụt
            float missing = (barSize.x - 0.04f) - w;
            _fill.localPosition = new Vector3(-missing * 0.5f, 0f, 0f);

            float tw = (barSize.x - 0.04f) * _trailFraction;
            _trailFill.localScale = new Vector3(tw, barSize.y - 0.04f, 1f);
            float tmissing = (barSize.x - 0.04f) - tw;
            _trailFill.localPosition = new Vector3(-tmissing * 0.5f, 0f, 0f);

            if (_enemy.IsDead && _barRoot != null) _barRoot.gameObject.SetActive(false);
        }

        /// <summary>Giữ hướng đọc xuôi khi quái lật: chỉ đổi DẤU của scale.x, giữ nguyên độ lớn.</summary>
        private static void UnflipX(Transform t, float signX)
        {
            if (t == null) return;
            var s = t.localScale;
            t.localScale = new Vector3(Mathf.Abs(s.x) * signX, s.y, s.z);
        }
    }

    /// <summary>Hiệu ứng số sát thương: bay lên + mờ dần rồi tự huỷ.</summary>
    public class FloatingNumber : MonoBehaviour
    {
        private TextMeshPro _tm;
        private float _life;
        private const float Duration = 0.8f;

        private void Awake() => _tm = GetComponent<TextMeshPro>();

        private void Update()
        {
            _life += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 1.5f;

            if (_tm != null)
            {
                float a = 1f - (_life / Duration);
                var c = _tm.color; c.a = Mathf.Clamp01(a); _tm.color = c;
            }

            if (_life >= Duration) Destroy(gameObject);
        }
    }
}
