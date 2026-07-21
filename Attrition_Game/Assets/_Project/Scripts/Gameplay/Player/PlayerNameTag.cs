using UnityEngine;
using TMPro;
using Attrition.Persistence;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Nhãn TÊN + thanh máu nổi trên đầu mỗi player (sprite world-space, không cần Canvas).
    /// Hiện trên MỌI máy: ai cũng thấy tên + máu của cả mình lẫn đồng đội (HUD máu người kia).
    /// Tự dựng runtime. Local = xanh, remote = cam. Đọc HP/DisplayName networked nên luôn đồng bộ.
    /// </summary>
    public class PlayerNameTag : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.7f, 0f);
        private const float BarW = 1.1f, BarH = 0.14f;

        private PlayerController _player;
        private PlayerStats _stats;
        private bool _isLocal;
        private TextMeshPro _nameText;
        private Transform _fill;
        private Transform _trailFill;
        private float _shown = 1f;
        private float _trail = 1f;

        public static void Attach(PlayerController player, bool isLocal)
        {
            var go = new GameObject("PlayerNameTag");
            // Bám VisualRoot (đã nội suy mượt) thay vì root (snap 60Hz) → tên trôi cùng nhịp sprite,
            // không giật tương đối. VisualRoot detach khi chết → nametag theo xác; reattach khi sống lại.
            var parent = player.VisualRoot != null ? player.VisualRoot : player.transform;
            go.transform.SetParent(parent, false);
            var tag = go.AddComponent<PlayerNameTag>();
            tag._isLocal = isLocal;
            tag.Build(player, isLocal ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.95f, 0.6f, 0.25f));
        }

        private void Build(PlayerController player, Color color)
        {
            _player = player;
            transform.localPosition = offset;

            // Tên
            var textGo = new GameObject("Name");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            _nameText = textGo.AddComponent<TextMeshPro>();
            _nameText.text = "";
            _nameText.fontSize = 3.2f;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color = color;
            _nameText.sortingOrder = 80;

            // Thanh máu: nền đen + fill màu
            var bg = Quad("BarBG", transform, new Color(0f, 0f, 0f, 0.75f), BarW, BarH, 78);
            _trailFill = Quad("BarTrail", bg.transform, new Color(1f, 0.85f, 0.3f, 1f), BarW - 0.04f, BarH - 0.04f, 79).transform;
            _fill = Quad("BarFill", bg.transform, color, BarW - 0.04f, BarH - 0.04f, 80).transform;
            GameSettings.OnChanged += ApplyVisibility;
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            GameSettings.OnChanged -= ApplyVisibility;
        }

        private void ApplyVisibility()
        {
            bool visible = GameSettings.ShowPlayerNameplates && (_isLocal || GameSettings.ShowOtherPlayers);
            gameObject.SetActive(visible);
        }

        private GameObject Quad(string n, Transform parent, Color c, float w, float h, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnemyUISpriteCache.White();
            sr.color = c;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(w, h, 1f);
            return go;
        }

        private void Update()
        {
            if (_player == null) return;
            // Player có thể đã despawn (shutdown/về menu) → Object null/invalid. Đọc DisplayName/HP
            // ([Networked]) lúc đó ném InvalidOperationException. Guard trước khi đọc.
            if (_player.Object == null || !_player.Object.IsValid) return;

            // Tên (đọc networked DisplayName → mọi máy thấy giống nhau)
            string nm = _player.DisplayName.Value;
            if (_nameText != null && _nameText.text != nm) _nameText.text = nm;

            // Thanh máu. Đọc MaxHP LIVE từ PlayerStats (đã tính cấp/điểm/đồ) — KHÔNG dùng _player.maxHP
            // (field cache 1 lần lúc Spawned, không cập nhật khi MaxHP đổi → mẫu số sai sau khi mặc đồ/
            // lên cấp). _stats resolve 1 lần rồi cache tham chiếu.
            if (_stats == null) _stats = _player.GetComponent<PlayerStats>();
            int maxHp = _stats != null ? _stats.MaxHP : _player.maxHP;
            int max = Mathf.Max(1, maxHp);
            float target = Mathf.Clamp01((float)_player.HP / max);
            _shown = target;
            _trail = Mathf.MoveTowards(_trail, target, Time.deltaTime * 1.5f);
            if (_fill != null)
            {
                float w = (BarW - 0.04f) * _shown;
                _fill.localScale = new Vector3(w, BarH - 0.04f, 1f);
                _fill.localPosition = new Vector3(-((BarW - 0.04f) - w) * 0.5f, 0f, 0f);
            }
            if (_trailFill != null)
            {
                float tw = (BarW - 0.04f) * _trail;
                _trailFill.localScale = new Vector3(tw, BarH - 0.04f, 1f);
                _trailFill.localPosition = new Vector3(-((BarW - 0.04f) - tw) * 0.5f, 0f, 0f);
            }
        }
    }
}
