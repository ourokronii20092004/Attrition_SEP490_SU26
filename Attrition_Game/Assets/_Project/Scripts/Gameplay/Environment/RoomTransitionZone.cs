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

        [Tooltip("ID của SceneEntryPoint ở scene ĐÍCH để player xuất hiện đúng cửa nối. " +
                 "Bỏ trống = dùng Player_SpawnPoint (đầu map). Cửa ĐI NGƯỢC nên điền, " +
                 "nếu không player về map cũ sẽ bị ném về đầu map.")]
        [SerializeField] private string entryPointId = "";

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

        /// <summary>
        /// HOST xét điều kiện MỖI TICK thay vì chỉ lúc player bước vào. Lý do:
        ///  1. Đứng sẵn trong vùng rồi boss mới chết (IsActive bật sau) → nếu chỉ xét ở OnTriggerEnter2D
        ///     thì cửa "chết cứng", phải bước ra bước vào lại mới đi được.
        ///  2. Coop: người thứ hai vào vùng ở máy khác — host mới là nơi thấy đủ cả hai (host có
        ///     StateAuthority trên MỌI player nên simulate hết; client KHÔNG simulate đồng đội).
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _transitionStarted) return;
            if (_playersInZone.Count == 0) return;
            CheckTransition();
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

        /// <summary>
        /// CHỈ HOST xét (host là peer duy nhất simulate MỌI player nên thấy đủ cả hai người).
        /// Đủ điều kiện → host bắn RPC cho MỌI máy cùng fade + ghi điểm vào, rồi host load scene.
        /// </summary>
        private void CheckTransition()
        {
            if (!HasStateAuthority || _transitionStarted) return;
            if (!IsActive) return;
            if (string.IsNullOrEmpty(nextSceneName)) return;

            int requiredPlayers = 1;

            if (Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop)
            {
                var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                int alive = 0;
                foreach (var p in allPlayers)
                    if (p != null && p.Object != null && p.Object.IsValid && !p.isDeadNetworked) alive++;
                requiredPlayers = Mathf.Max(1, alive);
            }

            // Chỉ tính player CÒN SỐNG & hợp lệ đang trong vùng (xác chết nằm trong vùng không tính).
            int inZone = 0;
            foreach (var p in _playersInZone)
                if (p != null && p.Object != null && p.Object.IsValid && !p.isDeadNetworked) inZone++;

            if (inZone < requiredPlayers) return;

            _transitionStarted = true;   // chặn ngay, tránh RPC bắn nhiều lần trong các tick kế
            RpcBeginTransition(string.IsNullOrEmpty(entryPointId) ? "" : entryPointId);
        }

        /// <summary>
        /// Host → MỌI máy: cùng fade đen và cùng ghi PendingEntryId, sau đó host load scene.
        /// Trước đây mỗi máy tự chạy TransitionRoutine cục bộ, nhưng trigger chỉ đáng tin ở host
        /// (client KHÔNG simulate đồng đội) → client thường không fade và KHÔNG ghi PendingEntryId
        /// → sang scene mới client bị đặt về Player_SpawnPoint thay vì cửa nối.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcBeginTransition(string entryId)
        {
            _transitionStarted = true;
            StartCoroutine(TransitionRoutine(entryId));
        }

        private IEnumerator TransitionRoutine(string entryId)
        {
            // Ghi điểm vào cho scene ĐÍCH trên MỌI máy (static local, không networked).
            // NetworkSpawner (host) đọc để đặt player đúng cửa nối; client dùng cho camera/entry sau load.
            SceneEntryRegistry.PendingEntryId = string.IsNullOrEmpty(entryId) ? null : entryId;

            // Màn đen dần trên MỌI máy.
            yield return SceneFader.FadeOut(fadeOutDuration);

            // Chỉ HOST ra lệnh load scene; client tự follow scene của host qua Fusion.
            var launcher = Attrition.Networking.NetworkLauncher.Instance;
            if (launcher != null && launcher.Runner != null && launcher.Runner.IsServer)
            {
                launcher.BeginGameplay(nextSceneName);
            }
            // Không FadeIn ở đây: SceneFader tự fade-in khi scene mới load xong (hook sceneLoaded).
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
