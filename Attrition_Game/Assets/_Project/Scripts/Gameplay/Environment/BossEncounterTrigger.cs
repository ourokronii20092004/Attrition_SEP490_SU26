using UnityEngine;
using Attrition.Gameplay.Player;
using System.Collections.Generic;

namespace Attrition.Gameplay.Environment
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class BossEncounterTrigger : MonoBehaviour
    {
        /// <summary>
        /// AI boss cần kích hoạt. Khai kiểu `MonoBehaviour` (không phải `SeveredFangAI` như trước) để mọi
        /// boss implement <see cref="Attrition.Core.IBossEncounter"/> đều KÉO VÀO Ô NÀY được — Inspector
        /// không hiện được ô nhận interface thuần, nên nhận MonoBehaviour rồi cast.
        /// </summary>
        [Tooltip("AI boss (SeveredFang / Druid / Elf / DemonKin / ArchDemon). Phải implement IBossEncounter.")]
        public MonoBehaviour boss;

        /// <summary>Boss dưới dạng interface, null nếu ô 'boss' để trống hoặc gán sai loại component.</summary>
        public Attrition.Core.IBossEncounter BossEncounter => boss as Attrition.Core.IBossEncounter;


        private bool _isTriggered;
        private HashSet<PlayerController> _playersInTrigger = new HashSet<PlayerController>();

        /// <summary>
        /// CHỈ HOST được quyết định kích hoạt boss. Client dùng
        /// ClientPhysicsSimulation.SimulateForward và `PlayerController` chỉ SetIsSimulated cho player
        /// CỦA MÌNH → trigger của đồng đội KHÔNG chạy trên máy client, nên client luôn đếm thiếu người
        /// và coop bị "vào phòng boss mà boss không kích hoạt".
        /// runner == null (solo chạy trực tiếp scene) → vẫn cho phép.
        /// </summary>
        private static bool IsHost
        {
            get
            {
                var r = Attrition.Networking.NetworkLauncher.Instance != null
                    ? Attrition.Networking.NetworkLauncher.Instance.Runner : null;
                return r == null || r.IsServer;
            }
        }

        /// <summary>
        /// HOST xét lại MỖI FRAME: người thứ hai có thể bước vào ở máy khác, hoặc đồng đội vừa hồi sinh
        /// làm đổi số người cần có. Chỉ dựa vào OnTriggerEnter2D thì dễ kẹt (đúng bug user báo).
        /// </summary>
        private void Update()
        {
            if (_isTriggered || _playersInTrigger.Count == 0) return;
            CheckTrigger();
        }

        /// <summary>
        /// Cho phép kích hoạt lại trigger (dùng khi cả team chết mà boss còn sống → player quay lại đánh).
        /// Xoá danh sách player đang đứng trong vùng vì lúc wipe player đã bị teleport về checkpoint —
        /// nếu giữ lại, lần vào phòng sau sẽ đếm nhầm (tưởng vẫn còn người trong vùng).
        /// </summary>
        public void ResetTrigger()
        {
            _isTriggered = false;
            _playersInTrigger.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isTriggered) return;

            if (other.CompareTag("Player"))
            {
                var player = other.GetComponentInParent<PlayerController>();
                if (player != null && !_playersInTrigger.Contains(player))
                {
                    _playersInTrigger.Add(player);
                    CheckTrigger();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_isTriggered) return;

            if (other.CompareTag("Player"))
            {
                var player = other.GetComponentInParent<PlayerController>();
                if (player != null && _playersInTrigger.Contains(player))
                {
                    _playersInTrigger.Remove(player);
                }
            }
        }

        private void CheckTrigger()
        {
            if (_isTriggered) return;
            if (!IsHost) return;   // xem IsHost: client không thấy trigger của đồng đội

            int requiredPlayers = 1;

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

            // Chỉ tính player CÒN SỐNG & hợp lệ (xác chết trong vùng không tính).
            int inTrigger = 0;
            foreach (var p in _playersInTrigger)
            {
                if (p != null && p.Object != null && p.Object.IsValid && !p.isDeadNetworked)
                    inTrigger++;
            }

            if (inTrigger >= requiredPlayers)
            {
                _isTriggered = true;
                BossEncounter?.StartIntroSequence();
            }
        }
    }
}
