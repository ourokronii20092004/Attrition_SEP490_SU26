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

        private void CheckTransition()
        {
            if (targetPosition == null) return;

            // Đếm tổng số lượng người chơi đang có trong màn
            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            int requiredPlayers = 1; // Mặc định solo

            // Nếu đang trong chế độ Coop, có bao nhiêu Player thì cần bấy nhiêu người vào trigger
            if (Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop)
            {
                int activePlayers = 0;
                foreach (var p in allPlayers)
                {
                    if (p != null && !p.isDeadNetworked) activePlayers++;
                }
                requiredPlayers = Mathf.Max(1, activePlayers);
            }

            // Nếu đủ người chơi tập trung tại cửa
            if (_playersInTrigger.Count >= requiredPlayers)
            {
                ExecuteTransition();
            }
        }

        private void ExecuteTransition()
        {
            StartCoroutine(TransitionRoutine());
        }

        private System.Collections.IEnumerator TransitionRoutine()
        {
            // Bắt đầu Fade Out (Màn hình đen dần)
            yield return StartCoroutine(SceneFader.FadeOut(0.5f));

            // Dịch chuyển tất cả người chơi sang vị trí mới thông qua RPC (Host xử lý)
            // Lấy player local (có InputAuthority) để gửi yêu cầu
            PlayerController localPlayer = null;
            foreach (var player in _playersInTrigger)
            {
                if (player != null && player.HasInputAuthority)
                {
                    localPlayer = player;
                    break;
                }
            }

            if (localPlayer != null)
            {
                // Dịch chuyển cục bộ ngay lập tức để giấu độ trễ mạng (Network Latency)
                foreach (var player in _playersInTrigger)
                {
                    if (player != null)
                    {
                        var playerRb = player.GetComponent<Rigidbody2D>();
                        if (playerRb != null)
                        {
                            Vector3 delta = targetPosition.position - player.transform.position;
                            playerRb.position = targetPosition.position;
                            
                            // Ép Camera di chuyển ngay lập tức và xóa giới hạn của phòng cũ
                            if (player.HasInputAuthority)
                            {
                                var cam = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
                                if (cam != null)
                                {
                                    var confiner = cam.GetComponent<Unity.Cinemachine.CinemachineConfiner2D>();
                                    if (confiner != null)
                                    {
                                        confiner.BoundingShape2D = null;
                                        confiner.InvalidateBoundingShapeCache();
                                    }
                                    cam.OnTargetObjectWarped(player.transform, delta);
                                }
                            }
                        }
                    }
                }
                
                localPlayer.RpcRequestFastTravel(targetPosition.position);
            }
            else
            {
                // Fallback nếu vì lý do nào đó không tìm thấy local player
                foreach (var player in _playersInTrigger)
                {
                    if (player != null && player.HasStateAuthority)
                    {
                        player.TeleportTo(targetPosition.position);
                    }
                }
            }

            // Đợi một chút để đồng bộ network và Camera kịp di chuyển
            yield return new WaitForSeconds(0.2f);

            // Bắt đầu Fade In (Màn hình sáng dần)
            yield return StartCoroutine(SceneFader.FadeIn(0.5f));

            // Xoá danh sách sau khi dịch chuyển để tránh dính trigger lại lập tức
            _playersInTrigger.Clear();
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
