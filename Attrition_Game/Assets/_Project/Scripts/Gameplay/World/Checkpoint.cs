using System.Linq;
using Fusion;
using UnityEngine;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Checkpoint / Save point (cơ chế Rest kiểu Hollow Knight/Sekiro).
    /// Gắn lên 1 GameObject có Collider2D (Is Trigger = ON) làm vùng nghỉ.
    ///
    /// Luật coop (chốt 2026-06-02):
    /// - Cần CẢ HAI player out-of-combat (không quái nào đang aggro toàn scene).
    /// - CHỈ MỘT người đứng trong vùng nhấn R là đủ → hồi đầy HP/Mana/Stamina + refill bình CHO CẢ HAI.
    /// - Lưu respawn point host-side (RespawnPosition) để dùng khi hồi sinh sau này.
    ///
    /// Chỉ host (StateAuthority) thực thi; client gọi TryRest qua RPC.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : NetworkBehaviour
    {
        [Header("---- IDENTITY ----")]
        [Tooltip("Tên hiển thị trên UI Fast Travel. Bỏ trống = dùng tên GameObject.")]
        [SerializeField] private string displayName = "";
        [Tooltip("Khu vực (region) hiển thị phụ trên UI.")]
        [SerializeField] private string region = "";

        public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
        public string Region => region;

        [Header("---- REST ----")]
        [Tooltip("Điểm hồi sinh khi rest tại đây. Bỏ trống = dùng vị trí của chính checkpoint.")]
        [SerializeField] private Transform respawnPoint;

        [Header("---- FEEDBACK (tùy chọn) ----")]
        [Tooltip("Bật/tắt object này khi rest thành công (vd hiệu ứng lửa, ánh sáng).")]
        [SerializeField] private GameObject activeVisual;

        /// <summary>Vị trí respawn đã lưu (host set khi rest). Player đọc khi cần hồi sinh.</summary>
        [Networked] public Vector3 RespawnPosition { get; set; }
        [Networked] public NetworkBool HasBeenActivated { get; set; }

        /// <summary>
        /// Checkpoint được rest/activate GẦN NHẤT (host-side). Respawn sau Game Over dùng cái này thay vì
        /// "checkpoint activated đầu tiên trong scene" — nếu không, đã activate nhiều điểm sẽ hồi sinh
        /// nhầm về điểm đầu danh sách thay vì điểm save gần nhất.
        /// </summary>
        public static Checkpoint MostRecentlyActivated { get; internal set; }

        private Vector3 RestPoint => respawnPoint != null ? respawnPoint.position : transform.position;

        public override void Spawned()
        {
            // Khôi phục trạng thái "đã kích hoạt" từ save (host/solo). HasBeenActivated là [Networked]
            // nên KHÔNG tự bền qua lần chơi — phải đọc lại từ slot đã lưu rồi bật cờ.
            if (HasStateAuthority) RestoreActivatedFromSave();
            if (activeVisual != null) activeVisual.SetActive(HasBeenActivated);
        }

        private void RestoreActivatedFromSave()
        {
            // Online (coop) khôi phục từ server — bỏ qua local.
            if (Attrition.Persistence.GameLaunch.IsOnline) return;

            var data = Attrition.Persistence.SaveManager.LoadSlot(Attrition.Persistence.GameLaunch.SelectedSlot);
            if (data == null) return;

            // Nạp toàn bộ trạng thái bản đồ (fog + mọi checkpoint đã khám phá) vào WorldMapState 1 lần.
            Attrition.Gameplay.Environment.WorldMapState.LoadFrom(data);

            // Checkpoint này đã khám phá (đã rest trước đây)? → bật beacon để fast-travel + world map thấy.
            // Hỗ trợ CẢ list mới (discoveredCheckpoints) lẫn field cũ (checkpointId) cho save cũ.
            bool discovered = Attrition.Gameplay.Environment.WorldMapState.IsCheckpointDiscovered(DisplayName)
                              || data.checkpointId == DisplayName;
            if (discovered)
            {
                HasBeenActivated = true;
                RespawnPosition = RestPoint;
                Attrition.Gameplay.Environment.WorldMapState.MarkCheckpointDiscovered(DisplayName);

                // Checkpoint khớp đúng cái save gần nhất → đặt làm điểm hồi sinh hiện hành.
                if (data.checkpointId == DisplayName) MostRecentlyActivated = this;
            }
        }

        /// <summary>
        /// Gọi bởi player đang đứng trong vùng khi nhấn R (bất kỳ peer nào).
        /// Tự định tuyến: client → RPC lên host; host xử lý trực tiếp.
        /// </summary>
        public void RequestRest()
        {
            RpcRequestRest();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestRest()
        {
            DoRest();
        }

        /// <summary>
        /// Gọi khi player nhấn F mở bảng checkpoint: kích hoạt beacon (fast-travel) + LƯU tiến trình.
        /// Tách khỏi Rest — mở bảng luôn lưu, kể cả khi còn quái (save không cần out-of-combat).
        /// </summary>
        public void RequestActivateAndSave()
        {
            RpcRequestActivateAndSave();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestActivateAndSave()
        {
            if (!HasStateAuthority) return;

            RespawnPosition = RestPoint;
            HasBeenActivated = true;
            MostRecentlyActivated = this;

            // Đánh dấu đã khám phá (cho World Map + fast-travel cross-map nhớ qua các phiên).
            Attrition.Gameplay.Environment.WorldMapState.MarkCheckpointDiscovered(DisplayName);

            // Lưu tiến trình tại mốc tới checkpoint (host/single). Solo → local JSON; online → snapshot.
            var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
            saver.Save(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Rest,
                       DisplayName, RestPoint);

            RpcOnRested();
        }

        // Host thực thi rest. Trả về true nếu thành công.
        private bool DoRest()
        {
            if (!HasStateAuthority) return false;
            if (AnyEnemyAggressive()) return false;

            // 1) Hồi đầy + refill bình + DỊCH CHUYỂN mọi player về điểm rest.
            // Loading "Resting..." bắn về CẢ HAI máy ngay khi rest hợp lệ (trước teleport) để không giật.
            RpcRestTeleportLoading();
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc == null) continue;
                var stats = pc.GetComponent<PlayerStats>();
                if (stats != null)
                {
                    stats.RestoreFull();
                    var potions = pc.GetComponent<PotionSystem>();
                    if (potions != null) potions.RefillAll();
                }
                pc.TeleportTo(RestPoint);
            }

            // 2) Reset/hồi sinh quái thường + elite (boss đã đánh chết KHÔNG hồi sinh).
            var spawner = FindFirstObjectByType<NetworkSpawner>();
            if (spawner != null)
            {
                // Despawn mọi quái còn sống TRỪ boss (boss giữ nguyên), rồi spawn lại theo config.
                foreach (var enemy in FindObjectsByType<Attrition.Controllers.EnemyController>(FindObjectsSortMode.None))
                {
                    if (enemy == null) continue;
                    var es = enemy.GetComponent<Attrition.Gameplay.Enemy.EnemyStats>();
                    if (es != null && es.Tier == Attrition.Data.EnemyTier.Boss) continue; // boss: bỏ qua
                    spawner.DespawnObject(enemy.Object);
                }
                spawner.RespawnConfiguredEnemies();
            }

            RespawnPosition = RestPoint;
            HasBeenActivated = true;

            // Rest gần nhất → điểm hồi sinh hiện hành cho Game Over respawn.
            MostRecentlyActivated = this;

            // Đánh dấu đã khám phá (World Map + fast-travel nhớ qua phiên).
            Attrition.Gameplay.Environment.WorldMapState.MarkCheckpointDiscovered(DisplayName);

            // LƯU game SAU khi đã hồi đầy máu và bình (quan trọng: đè lên file save cũ để nhớ 100% HP)
            var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
            saver.Save(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Rest,
                       DisplayName, RestPoint);

            RpcOnRested();
            return true;
        }

        private bool AnyEnemyAggressive()
        {
            var enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            return enemies.Any(e => e != null && e.IsAggressive);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcOnRested()
        {
            // Dùng chung cho CẢ F (activate+save) lẫn nút Rest → chỉ bật beacon, KHÔNG hiện loading.
            // Loading "Resting..." chỉ bắn riêng từ DoRest (nút Rest thật) qua RpcRestTeleportLoading.
            if (activeVisual != null) activeVisual.SetActive(true);
        }

        /// <summary>Chỉ nút REST (DoRest) bắn: mọi máy hiện thanh load "Resting..." + sắp bị teleport.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcRestTeleportLoading()
        {
            Attrition.Controllers.CoopFeedbackEvents.RaiseTravelLoading("Resting...");
        }
    }
}
