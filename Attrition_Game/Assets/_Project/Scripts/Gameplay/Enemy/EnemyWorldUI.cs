using UnityEngine;
using TMPro;
using Attrition.Controllers;
using Attrition.Persistence;

namespace Attrition.Gameplay.Enemy
{
    /// <summary>
    /// UI thế-giới gắn trên quái: thanh máu dưới chân + số sát thương nổi khi bị đánh.
    /// Tự dựng runtime (không cần prefab). Đọc HP từ EnemyController, chạy trên mọi máy.
    /// Số sát thương chỉ hiện khi GameSettings.ShowDamageNumbers bật.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyWorldUI : MonoBehaviour
    {
        [Header("---- VỊ TRÍ ----")]
        [Tooltip("Lệch thanh máu so với gốc quái (dưới chân = y âm).")]
        [SerializeField] private Vector3 barOffset = new Vector3(0f, -0.6f, 0f);
        [Tooltip("Lệch điểm số sát thương nổi (trên đầu).")]
        [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1.2f, 0f);
        [Tooltip("Lệch nhãn tên (trên đầu quái).")]
        [SerializeField] private Vector3 nameOffset = new Vector3(0f, 0.95f, 0f);
        [SerializeField] private Vector2 barSize = new Vector2(1.2f, 0.14f);

        private EnemyController _enemy;
        private EnemyStats _stats;
        private Transform _barRoot;
        private Transform _fill;
        private Transform _trailFill;
        private float _shownFraction = 1f;
        private float _trailFraction = 1f;
        private bool _everDamaged;
        private Transform _nameLabel;

        private void Awake()
        {
            _enemy = GetComponent<EnemyController>();
            _stats = GetComponent<EnemyStats>();
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
                case Attrition.Data.EnemyTier.Boss:  color = new Color(1f, 0.4f, 0.35f); size = 4.2f; break;
                case Attrition.Data.EnemyTier.Elite: color = new Color(1f, 0.7f, 0.3f);  size = 3.4f; break;
                default:                             color = new Color(0.85f, 0.82f, 0.78f); size = 3f; break;
            }

            var labelObj = WorldNameLabel.Attach(transform, display, nameOffset, color, size);
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

            if (tier == Attrition.Data.EnemyTier.Boss)
            {
                _barRoot.localPosition = new Vector3(0f, -2.2f, 0f);
                _barRoot.localScale = new Vector3(3.5f, 2.0f, 1f);
                _barRoot.gameObject.SetActive(true);
                _everDamaged = true;
            }
            else
            {
                _barRoot.localPosition = barOffset;
                _barRoot.gameObject.SetActive(false); // ẩn tới khi bị đánh lần đầu
            }

            var bg = CreateQuad("BG", _barRoot, new Color(0f, 0f, 0f, 0.7f), barSize, 0);
            var fillSize = new Vector2(barSize.x - 0.04f, barSize.y - 0.04f);
            _trailFill = CreateQuad("Trail", bg.transform, new Color(1f, 0.85f, 0.3f, 1f), fillSize, 1).transform;
            _fill = CreateQuad("Fill", bg.transform, new Color(0.7f, 0.16f, 0.16f, 1f), fillSize, 2).transform;
        }

        private GameObject CreateQuad(string name, Transform parent, Color color, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = color;
            sr.sortingOrder = 50 + order;
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
            go.transform.position = transform.position + popupOffset;
            var tm = go.AddComponent<TextMeshPro>();
            tm.text = amount.ToString();
            tm.fontSize = 4f;
            tm.alignment = TextAlignmentOptions.Center;
            tm.color = new Color(1f, 0.85f, 0.3f, 1f);
            tm.sortingOrder = 60;
            go.AddComponent<FloatingNumber>();
        }

        // Chữ ký frame trước: thanh máu chỉ đổi khi HP đổi / trail đang chạy / quái quay mặt. Ghi lại
        // 4 transform (mỗi con quái, mỗi frame) khi chẳng có gì đổi là chi phí thuần vô ích — Unity còn
        // phải đánh dấu transform dirty và đồng bộ lại toàn bộ hierarchy đó.
        private float _lastSig = float.NaN;

        private void Update()
        {
            if (!_everDamaged || _enemy == null || _fill == null) return;

            int max = MaxHpForBar();
            float target = Mathf.Clamp01((float)_enemy.CurrentHealth / max);

            float signXNow = Mathf.Sign(transform.localScale.x);
            float sig = target * 4096f + _trailFraction * 16f + signXNow;
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

            // Chống lật UI (khi enemy quay mặt, transform.localScale.x bị lật âm, 
            // khiến chữ và thanh máu bị lộn ngược trái phải).
            float signX = signXNow;
            if (_nameLabel != null)
            {
                var ns = _nameLabel.localScale;
                _nameLabel.localScale = new Vector3(Mathf.Abs(ns.x) * signX, ns.y, ns.z);
            }
            if (_barRoot != null)
            {
                var bs = _barRoot.localScale;
                _barRoot.localScale = new Vector3(Mathf.Abs(bs.x) * signX, bs.y, bs.z);
            }
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
