using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Bẫy môi trường (gai, hố, dung nham...). Player chạm vào → mất 15% Max HP +
    /// hồi sinh tại điểm đất an toàn cuối (BR-38/39). Logic ở PlayerController.HazardHit.
    /// Gắn vào GameObject có Collider2D (isTrigger). Không cần NetworkObject:
    /// mỗi PlayerController tự xử lý phần networked của mình.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        [Tooltip("Giãn cách tối thiểu giữa 2 lần trúng bẫy của cùng 1 player (giây).")]
        [SerializeField] private float retriggerCooldown = 1.0f;

        [Header("---- VÙNG VỚI THÊM ----")]
        [Tooltip("Bẫy 'với' thêm bao nhiêu world-unit ngoài mép tile (1 tile = 1 unit). Chống việc player " +
                 "rơi vào RÃNH HẸP mà collider người chèn giữa 2 vách, không chạm đáy gai nên không chết " +
                 "và kẹt luôn dưới đó. 0 = tắt, chỉ dùng trigger sát mép như cũ.")]
        [SerializeField] private float extraReach = 0.14f;

        [Tooltip("Nhịp quét vùng với thêm (giây). Không cần mỗi frame — 0.1s là quá đủ để bắt player kẹt.")]
        [SerializeField] private float scanInterval = 0.1f;

        private Collider2D _col;
        private float _nextScanTime;

        // Cache player để không FindObjectsByType mỗi nhịp quét. Refresh thưa vì player chỉ đổi khi
        // spawn/despawn (vào trận, đổi scene, đồng đội kết nối).
        private readonly List<PlayerController> _players = new List<PlayerController>();
        private float _nextPlayerRefresh;

        // Cooldown THEO TỪNG PLAYER, không dùng 1 biến chung: cả map chỉ có 1 Tilemap hazard nên nếu
        // dùng chung, player A trúng bẫy sẽ khoá luôn player B trong coop (B đi vào gai mà không mất máu).
        private readonly Dictionary<PlayerController, float> _lastHitByPlayer = new Dictionary<PlayerController, float>();

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake() => _col = GetComponent<Collider2D>();

        // ─── SỔ ĐĂNG KÝ TĨNH ───
        // PlayerController cần biết "chỗ này có gần bẫy không" trước khi ghi nhớ điểm đất an toàn.
        // Dùng danh sách tĩnh thay vì FindObjectsByType: hàm hỏi được gọi trong CheckGround (mỗi tick).
        private static readonly List<Hazard> _all = new List<Hazard>();

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() => _all.Remove(this);

        /// <summary>
        /// Collider này có đang nằm trong/sát vùng bẫy nào không?
        ///
        /// VÌ SAO CẦN: `PlayerController.CheckGround` ghi `_lastStableGround` mỗi khi player đứng yên trên
        /// đất — KỂ CẢ khi đang đứng dưới đáy hố gai. Rơi xuống hố là điểm đó bị ghi thành "đất an toàn",
        /// nên cú `HazardHit` sau đó kéo player về... chính đáy hố. Đúng lỗi "chết rồi hồi sinh vẫn nằm
        /// dưới chỗ hazard".
        ///
        /// Dùng `Collider2D.Distance` (phép tính trực tiếp) chứ không phải `Physics2D.Overlap*` — Fusion
        /// chạy physics scene riêng nên query scene mặc định luôn trả 0. Xem ghi chú ở Update().
        /// </summary>
        public static bool IsNearAnyHazard(Collider2D playerCol, float margin = 1.5f)
        {
            if (playerCol == null) return false;
            for (int i = 0; i < _all.Count; i++)
            {
                var h = _all[i];
                if (h == null || h._col == null || !h._col.enabled) continue;
                var d = h._col.Distance(playerCol);
                if (d.isValid && d.distance <= margin) return true;
            }
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);

        /// <summary>
        /// Quét thêm vùng RỘNG HƠN mép tile để bắt player kẹt trong rãnh hẹp — trường hợp trigger thường
        /// không bắt được vì collider người chèn ngang giữa 2 vách, không chạm đáy gai.
        ///
        /// VÌ SAO KHÔNG PHÓNG TO COLLIDER: `TilemapCollider2D` sinh hình tự động từ tile, không có tham số
        /// "nới ra". Đổi sang CompositeCollider2D + extrusion sẽ thay cả cách collider hoạt động — rủi ro
        /// hơn nhiều so với việc với thêm một chút ở đây.
        ///
        /// VÌ SAO KHÔNG DÙNG Physics2D.Overlap*: Fusion chạy PHYSICS SCENE RIÊNG, scene mặc định RỖNG nên
        /// mọi `Physics2D.*` query đều trả 0 (cùng bẫy đã gặp ở EnemyAoEDamage.SnapToGround). `Collider2D.
        /// Distance` là phép tính collider-với-collider TRỰC TIẾP, không qua scene query → chạy đúng ở cả
        /// hai loại scene.
        /// </summary>
        private void Update()
        {
            if (extraReach <= 0f || _col == null) return;
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + Mathf.Max(0.02f, scanInterval);

            RefreshPlayersIfNeeded();

            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p == null || p.IsDead) continue;

                var pc = p.GetComponent<Collider2D>();
                if (pc == null || !pc.enabled) continue;   // xác chết bị tắt collider → bỏ qua

                // distance < 0 = đang lồng nhau (trigger thường đã bắt); 0..extraReach = sát mép, kẹt rãnh.
                var d = _col.Distance(pc);
                if (!d.isValid || d.distance > extraReach) continue;

                TryHit(pc);
            }
        }

        private void RefreshPlayersIfNeeded()
        {
            if (Time.time < _nextPlayerRefresh && _players.Count > 0) return;
            _nextPlayerRefresh = Time.time + 2f;

            _players.Clear();
            foreach (var p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (p != null) _players.Add(p);
        }

        private void TryHit(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDead) return;

            if (_lastHitByPlayer.TryGetValue(player, out float last)
                && Time.time - last < retriggerCooldown) return;

            _lastHitByPlayer[player] = Time.time;
            player.HazardHit();
        }
    }
}
