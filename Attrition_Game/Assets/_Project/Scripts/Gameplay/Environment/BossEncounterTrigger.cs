using UnityEngine;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Vùng kích hoạt trận boss: đủ người sống bước vào → gọi StartIntroSequence.
    ///
    /// KHÔNG dùng OnTriggerEnter2D/Exit2D + HashSet nữa. Cách đó giữ TRẠNG THÁI TÍCH LUỸ nên sai bền:
    /// player bị teleport ra (checkpoint, wipe, chuyển phòng) hoặc chết/hồi sinh thì Exit không chắc nổ
    /// → danh sách còn tên người không còn trong vùng, hoặc thiếu người đang đứng trong vùng. Trigger
    /// kẹt vĩnh viễn, boss đứng im tới khi bị đánh (đường knockback trong RunAILogic đánh thức
    /// state machine) — đúng lỗi "2 người vào phòng mà Druid không đánh, chỉ đánh khi mình đánh trước".
    ///
    /// Thay bằng ĐỌC THẲNG vị trí player mỗi frame và so với bounds của vùng: không trạng thái tích luỹ,
    /// không phụ thuộc callback, tự đúng sau mọi lần teleport/hồi sinh.
    /// </summary>
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
        private Collider2D _zone;

        private void Awake() => _zone = GetComponent<Collider2D>();

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
            if (_isTriggered) return;
            CheckTrigger();
        }

        /// <summary>
        /// Cho phép kích hoạt lại trigger (dùng khi cả team chết mà boss còn sống → player quay lại đánh).
        /// </summary>
        public void ResetTrigger()
        {
            _isTriggered = false;
        }

        private void CheckTrigger()
        {
            if (_isTriggered) return;
            if (!IsHost) return;   // xem IsHost: client không thấy trigger của đồng đội
            if (_zone == null) return;

            // CHẨN ĐOÁN: log 1 lần xem ref boss có bị null ở runtime hay không — điểm nghi vấn chính
            // của "vào phòng boss Map 4 không kích hoạt". Boss là prefab instance trong scene; nếu
            // `boss` null hoặc cast IBossEncounter fail thì StartIntroSequence không bao giờ được gọi.
            if (!_loggedRefState)
            {
                _loggedRefState = true;
                var enc = BossEncounter;
                Debug.Log($"[BossTrigger:{name}] bossRefNull={boss == null} "
                          + $"bossEncounterNull={enc == null} zoneNull={_zone == null} "
                          + $"bossType={(boss != null ? boss.GetType().Name : "NULL")} "
                          + $"Mode={Attrition.Persistence.GameLaunch.Mode}");
            }

            var bounds = _zone.bounds;
            int alive = 0, inZone = 0;
            foreach (var p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (p == null || p.Object == null || !p.Object.IsValid || p.isDeadNetworked) continue;
                alive++;
                // Test 2D: bounds của collider là 3D, player có thể lệch Z so với vùng trigger.
                Vector3 pos = p.transform.position;
                if (pos.x >= bounds.min.x && pos.x <= bounds.max.x
                    && pos.y >= bounds.min.y && pos.y <= bounds.max.y) inZone++;
            }

            // Solo (hoặc chỉ còn 1 người sống trong coop) → 1 người là đủ.
            int required = Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop
                ? Mathf.Max(1, alive) : 1;

            if (inZone >= required)
            {
                _isTriggered = true;
                BossEncounter?.StartIntroSequence();
            }
        }

        private bool _loggedRefState;
    }
}
