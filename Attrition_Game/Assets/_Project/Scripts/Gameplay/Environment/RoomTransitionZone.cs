using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Vùng CHUYỂN SCENE (Map → Map kế tiếp), dựng theo cùng mẫu với RoomTransitionTrigger
    /// (vùng chuyển room trong cùng map) nhưng thay vì teleport thì LOAD scene khác.
    ///
    /// Luật vào vùng giống room transition:
    ///  - Solo: 1 player đứng trong vùng là đủ.
    ///  - Coop: cần TẤT CẢ player còn sống cùng đứng trong vùng.
    ///
    /// Cổng kích hoạt (activatable): mặc định TẮT (đánh boss xong mới bật qua SetActive()).
    /// Khi bật, vùng mới phản hồi player. Host cầm quyền: chỉ host gọi BeginGameplay (Fusion
    /// LoadScene), client tự follow scene của host. Dùng SceneFader để chuyển mượt.
    ///
    /// Gắn lên 1 GameObject có BoxCollider2D (IsTrigger). Phải là NetworkBehaviour để IsActive
    /// đồng bộ (client join muộn / boss đã chết vẫn thấy đúng).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class RoomTransitionZone : NetworkBehaviour
    {
        [Header("---- ĐÍCH ĐẾN ----")]
        [Tooltip("Tên scene Map kế tiếp (KHÔNG kèm path/đuôi). VD: 'Forest - Map 2'. Phải có trong Build Settings.")]
        [SerializeField] private string nextSceneName = "Forest - Map 2";

        [Header("---- CỔNG KÍCH HOẠT ----")]
        [Tooltip("Bật sẵn khi spawn? Để FALSE nếu vùng chỉ mở sau khi đánh boss.")]
        [SerializeField] private bool startActive = false;

        [Header("---- FADE ----")]
        [SerializeField] private float fadeOutDuration = 0.6f;

        /// <summary>Vùng đã được kích hoạt chưa (đồng bộ mạng).</summary>
        [Networked] public NetworkBool IsActive { get; set; }

        private readonly HashSet<PlayerController> _playersInZone = new HashSet<PlayerController>();
        private bool _transitionStarted;

        public override void Spawned()
        {
            if (HasStateAuthority) IsActive = startActive;
        }

        /// <summary>Bật cổng (host) — cho phép vùng chuyển scene. Gọi sau khi đánh boss xong.</summary>
        public void Activate()
        {
            if (!HasStateAuthority) return;
            IsActive = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && _playersInZone.Add(player)) CheckTransition();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null) _playersInZone.Remove(player);
        }

        private void CheckTransition()
        {
            if (!IsActive || _transitionStarted) return;
            if (string.IsNullOrEmpty(nextSceneName)) return;

            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            int requiredPlayers = 1;

            if (Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop)
            {
                int alive = 0;
                foreach (var p in allPlayers)
                    if (p != null && !p.isDeadNetworked) alive++;
                requiredPlayers = Mathf.Max(1, alive);
            }

            if (_playersInZone.Count >= requiredPlayers)
                StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            _transitionStarted = true;

            // Màn đen dần trên MỌI máy đang đứng trong vùng (mỗi client tự chạy fade cục bộ).
            yield return SceneFader.FadeOut(fadeOutDuration);

            // Chỉ HOST ra lệnh load scene; client follow qua Fusion. Client gọi sẽ no-op.
            var launcher = Attrition.Networking.NetworkLauncher.Instance;
            if (launcher != null && launcher.Runner != null && launcher.Runner.IsServer)
            {
                launcher.BeginGameplay(nextSceneName);
            }
            // Không FadeIn ở đây: scene mới load sẽ thay thế; SceneFader DontDestroyOnLoad nên
            // scene kế có thể gọi FadeIn lúc sẵn sàng (hoặc để màn đen tự nhiên trong lúc load).
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f); // cam: phân biệt với room transition (xanh)
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = new Vector3(
                col.size.x * transform.lossyScale.x,
                col.size.y * transform.lossyScale.y,
                transform.lossyScale.z);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
