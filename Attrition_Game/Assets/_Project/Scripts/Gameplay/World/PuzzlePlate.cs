using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Bệ kích hoạt (pressure plate). Máy SỞ HỮU player bắt trigger cục bộ rồi báo host qua RPC;
    /// host giữ IsActive [Networked] để các puzzle/cửa đọc cùng một nguồn sự thật.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PuzzlePlate : NetworkBehaviour
    {
        [Tooltip("True = đạp 1 lần là giữ luôn. False = rời bệ thì tắt.")]
        [SerializeField] private bool latching;

        [Networked] public NetworkBool IsActive { get; set; }

        // Một player prefab có thể có nhiều collider. Đếm collider để chỉ gửi enter đầu tiên / exit cuối.
        private readonly Dictionary<PlayerController, int> _localOccupants = new Dictionary<PlayerController, int>();
        // Host cần biết từng peer đang đứng trên bệ; một người rời không được tắt bệ nếu người kia còn đứng.
        private readonly HashSet<PlayerRef> _occupants = new HashSet<PlayerRef>();

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        public override void Spawned()
        {
            if (HasStateAuthority) IsActive = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || latching || _occupants.Count == 0) return;

            // Peer ngắt kết nối khi đang đứng trên plate sẽ không gửi Exit → bỏ PlayerRef đã rời,
            // nếu không plate/door bị kẹt mở vĩnh viễn.
            _occupants.RemoveWhere(p => !Runner.ActivePlayers.Contains(p));
            IsActive = _occupants.Count > 0;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.HasInputAuthority || player.IsDead) return;

            _localOccupants.TryGetValue(player, out int count);
            _localOccupants[player] = count + 1;
            if (count > 0) return;

            RpcSetOccupied(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.HasInputAuthority) return;
            if (!_localOccupants.TryGetValue(player, out int count)) return;

            if (count > 1) { _localOccupants[player] = count - 1; return; }
            _localOccupants.Remove(player);
            RpcSetOccupied(false);
        }

        /// <summary>
        /// Trigger của player client không đáng tin trên host vì host không simulate physics local của
        /// client. Ngược lại, mỗi peer luôn thấy trigger của CHÍNH player mình. Gửi kết quả đó lên host
        /// thay vì quét nhầm PhysicsScene2D của runner như bản trước (quét trả 0 → plate0 không bật).
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcSetOccupied(NetworkBool occupied, RpcInfo info = default)
        {
            if (latching && IsActive) return;

            // Không nhận PlayerRef từ payload (client có thể khai người khác). Fusion tự gắn peer gửi RPC.
            // Host gọi RPC cho chính mình có thể trả Source=None tuỳ GameMode → dùng LocalPlayer.
            PlayerRef player = info.Source;
            if (player == PlayerRef.None && Runner != null && Runner.IsServer)
                player = Runner.LocalPlayer;
            // Trust boundary: chỉ peer đang thực sự ở trong room mới được đổi trạng thái plate.
            if (player == PlayerRef.None || Runner == null || !Runner.ActivePlayers.Contains(player)) return;

            if (occupied) _occupants.Add(player);
            else _occupants.Remove(player);

            if (_occupants.Count > 0) IsActive = true;
            else if (!latching) IsActive = false;
        }
    }
}
