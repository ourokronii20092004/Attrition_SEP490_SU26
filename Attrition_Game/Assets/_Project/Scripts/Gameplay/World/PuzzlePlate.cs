using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Bệ kích hoạt (pressure plate). IsActive [Networked] do HOST giữ; puzzle/cửa đọc cờ này.
    ///
    /// PHÁT HIỆN NGƯỜI ĐỨNG TRÊN BỆ DÙNG *HAI* ĐƯỜNG SONG SONG (cố ý, không phải trùng lặp):
    ///   (A) Máy sở hữu player bắt OnTriggerEnter/Exit2D của CHÍNH player mình → gửi RPC lên host.
    ///   (B) HOST tự quét overlap mỗi tick.
    /// Lý do phải có cả hai: trigger callback chỉ đáng tin cho player mà peer đó tự simulate (host
    /// KHÔNG simulate player của client), còn quét overlap thì phụ thuộc player có nằm trong physics
    /// scene mà ta truy vấn hay không (Fusion có thể dùng physics scene riêng của runner). Mỗi đường
    /// bù đúng điểm yếu của đường kia; cả hai cùng ghi vào một tập _occupants nên không xung đột.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PuzzlePlate : NetworkBehaviour
    {
        [Tooltip("True = đạp 1 lần là giữ luôn. False = rời bệ thì tắt.")]
        [SerializeField] private bool latching;

        [Networked] public NetworkBool IsActive { get; set; }

        private Collider2D _col;

        // (A) Player local đang đứng trên bệ (đếm collider: 1 player có thể có nhiều collider).
        private readonly Dictionary<PlayerController, int> _localOccupants = new Dictionary<PlayerController, int>();
        // Host: peer nào đang đứng trên bệ theo đường RPC. Một người rời KHÔNG được tắt bệ nếu người kia còn đứng.
        private readonly HashSet<PlayerRef> _rpcOccupied = new HashSet<PlayerRef>();

        // Dùng lại giữa các tick — quét mỗi tick mà cấp phát mảng mới là rác GC vô ích.
        private static readonly Collider2D[] _hits = new Collider2D[16];

        private void Awake() => _col = GetComponent<Collider2D>();

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        public override void Spawned()
        {
            if (_col == null) _col = GetComponent<Collider2D>();
            if (HasStateAuthority) IsActive = false;

            // CHẨN ĐOÁN: in đúng những điều kiện quyết định bệ có chạy được hay không.
            Debug.Log($"[Plate:{name}] Spawned. HasStateAuthority={HasStateAuthority} "
                      + $"colliderNull={_col == null} isTrigger={(_col != null && _col.isTrigger)} "
                      + $"latching={latching} Mode={Attrition.Persistence.GameLaunch.Mode}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (latching && IsActive) return;   // đã chốt → khỏi xét nữa

            // Peer ngắt kết nối khi đang đứng trên bệ sẽ không gửi Exit → bỏ PlayerRef đã rời phòng,
            // nếu không bệ kẹt active vĩnh viễn.
            if (_rpcOccupied.Count > 0)
                _rpcOccupied.RemoveWhere(p => !IsStillInRoom(p));

            bool occupied = _rpcOccupied.Count > 0 || HostSeesPlayerOnPlate();

            if (occupied) IsActive = true;
            else if (!latching) IsActive = false;
        }

        private bool IsStillInRoom(PlayerRef player)
        {
            if (Runner == null) return false;
            foreach (var p in Runner.ActivePlayers)
                if (p == player) return true;
            return false;
        }

        /// <summary>
        /// (B) Host quét xem có player nào chồng lấn bệ. Thử physics scene của runner TRƯỚC (Fusion
        /// thường simulate ở đó), rồi thử physics scene mặc định — không đoán xem cái nào đúng, cứ
        /// hỏi cả hai. Chỉ chạy trên host và chỉ khi đường RPC chưa thấy ai.
        /// </summary>
        private bool HostSeesPlayerOnPlate()
        {
            if (_col == null) return false;

            var b = _col.bounds;
            // Không lọc theo layer: player có thể ở layer khác tuỳ prefab, và ta đã lọc chắc chắn bằng
            // GetComponentInParent<PlayerController> bên dưới. useTriggers = true vì collider player
            // có thể là trigger tuỳ setup.
            var filter = new ContactFilter2D();
            filter.NoFilter();          // xoá mọi bộ lọc mặc định (layerMask/depth/trigger)
            filter.useTriggers = true;  // collider player có thể là trigger tuỳ setup

            if (Runner != null)
            {
                int n = Runner.GetPhysicsScene2D().OverlapBox(b.center, b.size, 0f, filter, _hits);
                if (AnyLivingPlayer(n)) return true;
            }

            int m = Physics2D.OverlapBox(b.center, b.size, 0f, filter, _hits);
            return AnyLivingPlayer(m);
        }

        private static bool AnyLivingPlayer(int count)
        {
            for (int i = 0; i < count && i < _hits.Length; i++)
            {
                if (_hits[i] == null) continue;
                var pc = _hits[i].GetComponentInParent<PlayerController>();
                // Xác chết nằm trên bệ KHÔNG tính (cùng luật với RoomTransitionZone).
                if (pc != null && !pc.IsDead) return true;
            }
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.HasInputAuthority || player.IsDead) return;

            _localOccupants.TryGetValue(player, out int count);
            _localOccupants[player] = count + 1;
            if (count > 0) return;      // đã báo rồi (collider thứ 2 của cùng player)

            // CHẨN ĐOÁN phía GỬI: log này nằm trên máy của người đạp bệ (có thể là bản build).
            // Có dòng này mà host không có dòng "nhận RPC" ⇒ mất ở đường truyền/quyền RPC.
            Debug.Log($"[Plate:{name}] GỬI occupied=TRUE (máy này sở hữu player). IsServer={Runner != null && Runner.IsServer}");
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
        /// Peer báo host: player của tôi vừa lên/rời bệ. KHÔNG nhận PlayerRef từ payload (client có thể
        /// khai người khác) — Fusion tự gắn peer gửi qua RpcInfo.Source. Host tự gọi cho chính mình thì
        /// Source có thể là None tuỳ GameMode → rơi về LocalPlayer.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcSetOccupied(NetworkBool occupied, RpcInfo info = default)
        {
            if (latching && IsActive) return;

            PlayerRef player = info.Source;
            if (player == PlayerRef.None && Runner != null && Runner.IsServer)
                player = Runner.LocalPlayer;
            if (player == PlayerRef.None) return;

            if (occupied) _rpcOccupied.Add(player);
            else _rpcOccupied.Remove(player);

            // CHẨN ĐOÁN phía NHẬN: log này CHỈ xuất hiện trên HOST.
            Debug.Log($"[Plate:{name}] HOST NHẬN RPC occupied={occupied} từ peer {player} "
                      + $"→ tổng người trên bệ = {_rpcOccupied.Count}");

            // Cập nhật ngay, không chờ tick sau — cửa phản hồi tức thì khi vừa đạp.
            if (_rpcOccupied.Count > 0) IsActive = true;
            else if (!latching && !HostSeesPlayerOnPlate()) IsActive = false;
        }
    }
}
