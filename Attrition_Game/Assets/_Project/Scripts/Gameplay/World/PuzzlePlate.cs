using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Bệ kích hoạt (pressure plate) cho puzzle. Active khi có ít nhất 1 player đứng lên.
    /// Mặc định: nhả ra thì tắt (momentary). Bật "latching" để giữ active sau lần đầu đạp.
    /// IsActive là Networked → PuzzleController (host) đọc để kiểm tra điều kiện giải.
    /// Gắn lên GameObject có Collider2D (isTrigger).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PuzzlePlate : NetworkBehaviour
    {
        [Tooltip("True = đạp 1 lần là giữ luôn (cho puzzle cần đạp đúng tổ hợp). False = nhả ra thì tắt.")]
        [SerializeField] private bool latching = false;

        [Tooltip("Layer của player. Để quét xem có ai đứng trên bệ. Phải chứa layer 'Player'.")]
        [SerializeField] private LayerMask playerLayers = ~0;

        [Networked] public NetworkBool IsActive { get; set; }

        private Collider2D _col;

        // Dùng lại giữa các tick — quét mỗi tick mà cấp phát mảng mới là rác GC thuần vô ích.
        private static readonly Collider2D[] _hits = new Collider2D[8];

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
        }

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            int player = LayerMask.NameToLayer("Player");
            if (player >= 0) playerLayers = 1 << player;
        }

        /// <summary>
        /// HOST tự QUÉT xem có player nào đứng trên bệ, mỗi tick — KHÔNG dùng OnTriggerEnter2D/Exit2D.
        ///
        /// VÌ SAO: trigger callback của Unity chỉ đáng tin cho object mà peer này thực sự simulate.
        /// Host KHÔNG simulate player của client (cùng lý do đã ghi ở RoomTransitionZone.FixedUpdateNetwork),
        /// nên client đứng lên bệ thì host không nhận được callback → IsActive không bao giờ bật →
        /// CoopPlateDoorController thấy thiếu nút và cửa không mở. Mà puzzle này CỐ Ý bắt 2 người mỗi
        /// người 1 nút, nên luôn có 1 bệ do client đạp → gần như không bao giờ giải được.
        /// Quét chủ động ở đây thấy được MỌI player vì host có StateAuthority trên tất cả.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _col == null) return;
            if (latching && IsActive) return;   // đã chốt → khỏi quét nữa

            bool occupied = HasPlayerOnPlate();
            if (occupied) IsActive = true;
            else if (!latching) IsActive = false;
        }

        private bool HasPlayerOnPlate()
        {
            var bounds = _col.bounds;

            // BẮT BUỘC dùng Runner.GetPhysicsScene2D() thay vì Physics2D tĩnh — cùng lý do như
            // PlayerController.CheckGround: physics của Fusion nằm ở scene riêng của runner.
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = playerLayers,
                useTriggers = true,   // collider player có thể là trigger tuỳ setup
            };
            int n = Runner.GetPhysicsScene2D().OverlapBox(
                bounds.center, bounds.size, 0f, filter, _hits);

            for (int i = 0; i < n; i++)
            {
                if (_hits[i] == null) continue;
                var pc = _hits[i].GetComponentInParent<PlayerController>();
                // Xác chết nằm trên bệ KHÔNG tính (giống luật ở RoomTransitionZone).
                if (pc != null && !pc.IsDead) return true;
            }
            return false;
        }
    }
}
