using UnityEngine;
using System.Collections.Generic;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Gắn vào một BoxCollider2D (IsTrigger) đặt ở rìa Room.
    /// Dịch chuyển người chơi sang Room kế tiếp mượt mà khi đủ số lượng người chơi
    /// (1 người trong Solo, 2 người trong Co-op) tập trung tại điểm chuyển.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class RoomTransitionTrigger : MonoBehaviour
    {
        [Tooltip("Điểm đến ở Room tiếp theo. Đặt sao cho khớp với chiều đi của người chơi.")]
        public Transform targetPosition;

        private HashSet<PlayerController> _playersInTrigger = new HashSet<PlayerController>();
        private bool _transitionRunning;

        /// <summary>
        /// Máy này có quyền QUYẾT ĐỊNH mở cửa không? Chỉ HOST — vì client dùng
        /// ClientPhysicsSimulation.SimulateForward và chỉ simulate player CỦA MÌNH, nên trigger của
        /// đồng đội KHÔNG chạy trên máy client → client luôn đếm thiếu người, coop sẽ kẹt cửa.
        /// </summary>
        private static bool IsHost
        {
            get
            {
                var r = Attrition.Networking.NetworkLauncher.Instance != null
                    ? Attrition.Networking.NetworkLauncher.Instance.Runner : null;
                return r == null || r.IsServer;   // r == null: solo chạy không qua launcher → cứ cho phép
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                var player = other.GetComponentInParent<PlayerController>();
                if (player != null && !_playersInTrigger.Contains(player))
                {
                    _playersInTrigger.Add(player);
                    CheckTransition();
                }
            }
        }

        /// <summary>
        /// HOST xét lại mỗi frame: người thứ hai có thể bước vào ở máy khác, hoặc đồng đội hồi sinh
        /// làm đổi số người cần có. Chỉ dựa vào OnTriggerEnter2D thì dễ kẹt cửa trong coop.
        /// </summary>
        private void Update()
        {
            if (_transitionRunning || _playersInTrigger.Count == 0) return;
            CheckTransition();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                var player = other.GetComponentInParent<PlayerController>();
                if (player != null && _playersInTrigger.Contains(player))
                {
                    _playersInTrigger.Remove(player);
                }
            }
        }

        /// <summary>
        /// CHỈ HOST quyết định (xem <see cref="IsHost"/>). Client không thấy trigger của đồng đội nên
        /// nếu để client tự xét thì coop kẹt cửa vĩnh viễn.
        /// </summary>
        private void CheckTransition()
        {
            if (targetPosition == null || _transitionRunning) return;
            if (!IsHost) return;

            int requiredPlayers = 1; // Mặc định solo

            // Coop: cần TẤT CẢ player còn sống cùng đứng ở cửa.
            if (Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop)
            {
                var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                int activePlayers = 0;
                foreach (var p in allPlayers)
                {
                    if (p != null && p.Object != null && p.Object.IsValid && !p.isDeadNetworked)
                        activePlayers++;
                }
                requiredPlayers = Mathf.Max(1, activePlayers);
            }

            // Chỉ tính player CÒN SỐNG & hợp lệ (xác chết nằm trong vùng không tính).
            int inTrigger = 0;
            foreach (var p in _playersInTrigger)
            {
                if (p != null && p.Object != null && p.Object.IsValid && !p.isDeadNetworked)
                    inTrigger++;
            }

            if (inTrigger >= requiredPlayers) ExecuteTransition();
        }

        private void ExecuteTransition()
        {
            _transitionRunning = true;
            StartCoroutine(TransitionRoutine());
        }

        private System.Collections.IEnumerator TransitionRoutine()
        {
            // Màn đen trên MỌI máy: RpcRequestFastTravel (bên dưới) đã bắn RpcTravelLoading tới mọi peer
            // → mỗi máy tự nháy đen. Ở đây host fade trước cho khớp nhịp của chính nó.
            yield return StartCoroutine(SceneFader.FadeOut(0.5f));

            // Player của HOST (vừa có Input vừa có State authority) để gọi RPC teleport-toàn-đội.
            PlayerController hostPlayer = null;
            foreach (var player in _playersInTrigger)
            {
                if (player != null && player.HasInputAuthority) { hostPlayer = player; break; }
            }

            if (hostPlayer != null)
            {
                // Teleport MỌI player + bắn loading/fade tới MỌI máy. Vị trí đi qua _pendingTeleportSeq
                // ([Networked]) nên client cũng SNAP đúng chỗ, không bị prediction đè.
                // KHÔNG còn tự ghi rb.position cho từng player như trước: với player của máy khác,
                // ghi tay sẽ bị teleport networked ghi đè (hoặc giật 2 nhịp).
                hostPlayer.RpcRequestFastTravel(targetPosition.position);
            }
            else
            {
                // Host không đứng trong cửa (chỉ xảy ra ở solo lạ / test): teleport trực tiếp — host có
                // StateAuthority trên mọi player nên TeleportTo là hợp lệ và vẫn networked.
                foreach (var player in _playersInTrigger)
                {
                    if (player != null && player.HasStateAuthority)
                        player.TeleportTo(targetPosition.position);
                }
            }

            // Camera của máy này: xoá giới hạn phòng CŨ để không bị kẹt trong lúc chuyển. Bounds phòng
            // MỚI do CameraBoundsZone tự set khi player local bước vào (mỗi máy tự chạy cho player mình).
            ClearLocalCameraConfiner();

            // Đợi network đồng bộ + camera kịp di chuyển.
            // Realtime: timeScale có thể = 0 (solo dừng game ở overlay/hội thoại) → WaitForSeconds thường
            // sẽ treo mãi ở đây, kẹt luôn màn đen vừa fade ra. Xem ghi chú ở SceneFader.FadeOut.
            yield return new WaitForSecondsRealtime(0.2f);

            yield return StartCoroutine(SceneFader.FadeIn(0.5f));

            // Xoá danh sách để không dính trigger lại ngay.
            _playersInTrigger.Clear();
            _transitionRunning = false;
        }

        /// <summary>Bỏ confiner phòng cũ trên MÁY NÀY (local visual, không liên quan network).</summary>
        private static void ClearLocalCameraConfiner()
        {
            var cam = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (cam == null) return;

            var confiner = cam.GetComponent<Unity.Cinemachine.CinemachineConfiner2D>();
            if (confiner == null) return;

            confiner.BoundingShape2D = null;
            confiner.InvalidateBoundingShapeCache();
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                // Vẽ màu xanh dương nhạt cho vùng dịch chuyển trong cửa sổ Scene
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
                
                // Lấy vị trí trung tâm của Collider
                Vector3 center = transform.position + (Vector3)(col.offset);
                
                // Nhân kích thước của BoxCollider với scale của Transform
                Vector3 size = new Vector3(
                    col.size.x * transform.lossyScale.x, 
                    col.size.y * transform.lossyScale.y, 
                    transform.lossyScale.z
                );

                // Vẽ hình hộp chữ nhật mờ
                Gizmos.DrawCube(center, size);

                // Vẽ viền ngoài
                Gizmos.color = new Color(0f, 0.5f, 1f, 1f);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
