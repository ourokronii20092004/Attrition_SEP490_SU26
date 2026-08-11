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

        public static void ClearRuntimeState() => MostRecentlyActivated = null;

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
            // COOP: nguồn sự thật là WorldMapState (đã nạp từ server trong PlayerInventory). Có thể
            // fetch CHƯA xong lúc này → khi xong, ApplyCoopDiscovered() quét lại toàn bộ checkpoint.
            if (Attrition.Persistence.GameLaunch.IsOnline)
            {
                if (Attrition.Gameplay.Environment.WorldMapState.IsCheckpointDiscovered(DisplayName))
                    ApplyDiscovered();
                return;
            }

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
                ApplyDiscovered();

                // Checkpoint khớp đúng cái save gần nhất → đặt làm điểm hồi sinh hiện hành.
                if (data.checkpointId == DisplayName) MostRecentlyActivated = this;
            }
        }

        /// <summary>Bật beacon + điểm hồi sinh cho checkpoint đã khám phá (host-side).</summary>
        private void ApplyDiscovered()
        {
            HasBeenActivated = true;
            RespawnPosition = RestPoint;
            Attrition.Gameplay.Environment.WorldMapState.MarkCheckpointDiscovered(DisplayName);
            if (activeVisual != null) activeVisual.SetActive(true);
        }

        /// <summary>
        /// COOP: gọi SAU khi nạp xong world-state từ server. `Spawned()` của checkpoint có thể đã chạy
        /// TRƯỚC khi request về (thứ tự không đảm bảo) — lúc đó WorldMapState còn rỗng nên beacon không
        /// bật, fast-travel mất điểm đến và hồi sinh về sai chỗ. Quét lại một lượt là đủ, rẻ hơn cho
        /// mỗi checkpoint tự kiểm mỗi tick.
        /// </summary>
        public static void ApplyCoopDiscovered()
        {
            foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
            {
                if (cp == null || !cp.HasStateAuthority || cp.HasBeenActivated) continue;
                if (Attrition.Gameplay.Environment.WorldMapState.IsCheckpointDiscovered(cp.DisplayName))
                    cp.ApplyDiscovered();
            }
        }

        /// <summary>
        /// Host: đặt checkpoint này làm ĐIỂM HỒI SINH hiện hành (lastCheckpoint) — dùng bởi fast-travel.
        ///
        /// VÌ SAO CẦN: fast-travel trước đây chỉ set <see cref="MostRecentlyActivated"/> mà KHÔNG bật
        /// <see cref="HasBeenActivated"/>/<see cref="RespawnPosition"/>. Respawn sau Game Over đòi CẢ BA
        /// (xem PlayerController.RpcRequestRespawnAll), nên teleport tới checkpoint chưa từng rest rồi
        /// chết sẽ rơi vào nhánh fallback "checkpoint activated ĐẦU TIÊN trong scene" → hồi sinh sai chỗ.
        /// Đây đúng là lỗi user báo: chết sau khi teleport không về điểm vừa teleport.
        /// </summary>
        public void MarkAsLastCheckpoint()
        {
            if (!HasStateAuthority) return;

            RespawnPosition = RestPoint;
            HasBeenActivated = true;
            MostRecentlyActivated = this;
            Attrition.Gameplay.Environment.WorldMapState.MarkCheckpointDiscovered(DisplayName);
            RpcOnRested();   // bật beacon trên mọi máy + đánh dấu đã khám phá (WorldMapState là local)
        }

        /// <summary>
        /// Host: hồi đầy HP/Mana/Stamina + refill bình cho MỌI player, và hồi sinh người đang gục, tại
        /// <paramref name="destination"/>. Fast-travel dùng chung với rest theo yêu cầu "fast travel
        /// giống như rest". Tách riêng khỏi <see cref="DoRest"/> vì rest còn reset quái + lưu game.
        /// </summary>
        public static void RestoreAllPlayersAt(Vector3 destination)
        {
            PotionSystem.ShareFlaskCapacityOnRest();
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc == null) continue;
                if (pc.IsDead) { pc.ReviveAndRestore(destination); continue; }

                var stats = pc.GetComponent<PlayerStats>();
                if (stats != null)
                {
                    stats.RestoreFull();
                    var potions = pc.GetComponent<PotionSystem>();
                    if (potions != null) potions.RefillAll();
                }
                pc.TeleportTo(destination);
            }
        }

        /// <summary>
        /// Host: despawn mọi quái thường + elite CÒN SỐNG rồi spawn lại theo config. Boss KHÔNG hồi sinh
        /// (boss đặt sẵn trong scene, không nằm trong enemySpawnConfigs nên RespawnConfiguredEnemies
        /// không chạm tới).
        ///
        /// Dùng chung bởi rest, fast-travel và respawn sau Game Over — cả ba đều là "về checkpoint, thế
        /// giới nạp lại". Trước đây logic này bị copy ở 2 nơi (DoRest + RpcRequestRespawnAll) và
        /// fast-travel thì THIẾU HẲN → teleport về checkpoint xong quái vẫn nằm chết / vẫn đang aggro.
        /// </summary>
        public static void ResetEnemiesExceptBoss()
        {
            var spawner = FindFirstObjectByType<NetworkSpawner>();
            if (spawner == null) return;

            // Despawn TRƯỚC khi spawn lại → tránh nhân đôi quái.
            foreach (var enemy in FindObjectsByType<Attrition.Controllers.EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy == null) continue;
                var es = enemy.GetComponent<Attrition.Gameplay.Enemy.EnemyStats>();
                if (es != null && es.Tier == Attrition.Data.EnemyTier.Boss) continue; // boss: bỏ qua
                spawner.DespawnObject(enemy.Object);
            }
            spawner.RespawnConfiguredEnemies();
        }

        /// <summary>
        /// Host: xoá mọi item CÒN NẰM TRÊN SÀN chưa ai nhặt (rơi từ quái hoặc do player vứt ra).
        /// Rest = "thế giới nạp lại" nên loot cũ không được giữ lại — nhặt kịp trước khi Rest thì được,
        /// không thì mất. ForceCleanup tự kiểm HasStateAuthority + Consumed nên gọi thẳng là an toàn.
        /// </summary>
        public static void ClearDroppedItems()
        {
            foreach (var item in FindObjectsByType<DroppedItem>(FindObjectsSortMode.None))
                if (item != null) item.ForceCleanup();
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

        // Host thực thi rest. Trả về true nếu thành công.
        private bool DoRest()
        {
            if (!HasStateAuthority) return false;

            // 1) Hồi đầy + refill bình + DỊCH CHUYỂN mọi player về điểm rest.
            // Loading "Resting..." bắn về CẢ HAI máy ngay khi rest hợp lệ (trước teleport) để không giật.
            RpcRestTeleportLoading();

            // Hồi đầy + refill bình + teleport mọi player (kể cả người đang gục) — dùng chung với
            // fast-travel qua RestoreAllPlayersAt.
            RestoreAllPlayersAt(RestPoint);

            // 2) Reset/hồi sinh quái thường + elite (boss đã đánh chết KHÔNG hồi sinh).
            ResetEnemiesExceptBoss();

            // 2b) Xoá item còn nằm trên sàn chưa ai nhặt (rơi từ quái hoặc player vứt) — Rest làm mất.
            ClearDroppedItems();

            // Rest gần nhất → điểm hồi sinh hiện hành + bật beacon trên mọi máy (đã gồm RpcOnRested).
            MarkAsLastCheckpoint();

            // LƯU game SAU khi đã hồi đầy máu và bình (quan trọng: đè lên file save cũ để nhớ 100% HP)
            var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
            saver.Save(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Rest,
                       DisplayName, RestPoint);

            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcOnRested()
        {
            // RPC chạy trên mọi peer: static WorldMapState là local nên mỗi máy phải tự đánh dấu.
            Attrition.Gameplay.Environment.WorldMapState.MarkCheckpointDiscovered(DisplayName);
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
