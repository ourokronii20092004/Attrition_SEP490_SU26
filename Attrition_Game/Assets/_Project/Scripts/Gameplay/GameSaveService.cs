using System.Collections;
using UnityEngine;
using Newtonsoft.Json;
using Attrition.Gameplay.Player;
using Attrition.Persistence;

namespace Attrition.Gameplay.Persistence
{
    /// <summary>
    /// Điểm vào DUY NHẤT để lưu/đọc tiến trình. Định tuyến theo chế độ chơi:
    ///   - SOLO  (GameLaunch.Mode == Solo): lưu file JSON cục bộ qua SaveManager (slot 0..2).
    ///   - ONLINE (Coop + đã login):        POST snapshot lên web → web ghi Postgres (bền) + Redis (live).
    ///
    /// Quy ước dữ liệu (chốt với user):
    ///   - Base stat quái thường/elite/boss → ScriptableObject (không lưu ở đây).
    ///   - Tiến trình người chơi solo      → local JSON; online → Postgres (Docker).
    ///   - Trạng thái realtime trong trận   → web giữ ở Redis; client chỉ bắn snapshot tại mốc (rest/quit/death/levelup).
    ///
    /// Gắn component này lên 1 GameObject bền trong scene gameplay (hoặc tự tạo qua EnsureExists).
    /// Chỉ HOST/Single nên gọi Save (state-authoritative). Client coop không tự lưu hộ host.
    /// </summary>
    public class GameSaveService : MonoBehaviour
    {
        public static GameSaveService Instance { get; private set; }

        public enum SaveEvent { Rest, Quit, Death, LevelUp }

        private float _sessionStartTime;
        private int _basePlaytimeSeconds; // playtime tích lũy từ slot đã load

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // PERSIST qua scene: đổi map (fast-travel) không destroy + tạo lại → _basePlaytimeSeconds/
            // _sessionStartTime giữ nguyên → playtime cộng dồn ĐÚNG. Trước đây per-scene: mỗi lần đổi map
            // instance bị hủy + rebuild → baseline về 0 → playtime tụt sai. SetBasePlaytime vẫn nạp lại
            // đúng khi load slot mới, nên persist an toàn.
            DontDestroyOnLoad(gameObject);
            _sessionStartTime = Time.time;
        }

        public static GameSaveService EnsureExists()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameSaveService");
                Instance = go.AddComponent<GameSaveService>();
            }
            return Instance;
        }

        /// <summary>Nạp playtime nền từ slot vừa load (để cộng dồn với session hiện tại).</summary>
        public void SetBasePlaytime(int seconds) => _basePlaytimeSeconds = seconds;

        private int TotalPlaytimeSeconds =>
            _basePlaytimeSeconds + Mathf.FloorToInt(Time.time - _sessionStartTime);

        /// <summary>
        /// Lưu tiến trình của local player. Gọi tại mốc: rest checkpoint, quit, chết, lên cấp.
        /// An toàn gọi từ host/single; no-op nếu không tìm thấy local player.
        /// </summary>
        public void Save(SaveEvent evt, string checkpointId = null, Vector3? checkpointPos = null)
        {
            // ONLINE COOP: host-authoritative — chỉ HOST (server) lưu, và lưu hộ MỌI player
            // (host + client) theo character của từng người. Client KHÔNG tự lưu (tránh ghi đè).
            if (GameLaunch.IsOnline)
            {
                var anyPlayer = FindLocalPlayer();
                bool isServer = anyPlayer != null && anyPlayer.Object != null
                                && anyPlayer.Object.Runner != null && anyPlayer.Object.Runner.IsServer;
                if (!isServer) return; // client coop: host lo việc lưu
                StartCoroutine(SaveAllOnline(evt, checkpointId, checkpointPos));
                return;
            }

            // SOLO: lưu local player vào slot JSON.
            var player = FindLocalPlayer();
            if (player == null)
            {
                Debug.LogWarning("[Save] Không tìm thấy local player để lưu.");
                return;
            }

            var stats = player.GetComponent<PlayerStats>();
            var prog = player.GetComponent<PlayerProgression>();
            var potions = player.GetComponent<PotionSystem>();
            if (stats == null) return;

            SaveLocal(evt, stats, prog, potions, player, checkpointId, checkpointPos);
        }

        private void SaveLocal(SaveEvent evt, PlayerStats stats, PlayerProgression prog,
                               PotionSystem potions, PlayerController player,
                               string checkpointId, Vector3? checkpointPos)
        {
            int slot = GameLaunch.SelectedSlot;
            var data = SaveManager.LoadSlot(slot) ?? new SaveSlotData
            {
                characterName = string.IsNullOrEmpty(GameLaunch.CharacterName) ? "Wanderer" : GameLaunch.CharacterName,
                avatarColorIndex = 0
            };

            data.level = prog != null ? prog.Level : stats.Level;
            data.currentExp = prog != null ? prog.CurrentExp : 0;
            data.currentHP = stats.CurrentHP;
            data.currentMana = stats.CurrentMana;
            data.playtimeSeconds = TotalPlaytimeSeconds;
            data.playtime = data.ToDisplayPlaytime();
            if (evt == SaveEvent.Death) data.deaths += 1;

            if (checkpointPos.HasValue)
            {
                data.checkpointId = checkpointId ?? data.checkpointId;
                data.checkpointScene = GameLaunch.GameplayScene;
                data.checkpointX = checkpointPos.Value.x;
                data.checkpointY = checkpointPos.Value.y;
                data.checkpointZ = checkpointPos.Value.z;
            }

            if (potions != null)
            {
                data.potionMaxFlasks = potions.MaxHealthCharges;
                data.potionMaxManaFlasks = potions.MaxManaCharges;
                data.healthCharges = potions.HealthCharges;
                data.manaCharges = potions.ManaCharges;
            }
            data.allocatedPoints = CaptureAllocated(stats);
            data.quests = Attrition.Gameplay.NPC.NetworkNPC.CaptureAll();
            data.originMode = GameLaunch.Mode.ToString();
            data.lastSavedUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Bản đồ tổng: ghi fog đã xua + checkpoint đã khám phá vào save (lưu vĩnh viễn).
            Attrition.Gameplay.Environment.WorldMapState.WriteTo(data);
            // Boss đã hạ — không hồi sinh khi quay lại map / vào lại game.
            Attrition.Gameplay.Environment.BossDefeatState.WriteTo(data);
            // Vật phá được đã vỡ — không spawn lại (giữ đường tắt đã mở).
            Attrition.Gameplay.Environment.BreakableState.WriteTo(data);
            // Elite/Boss đã rơi đồ — quay lại đánh chỉ được EXP, không rơi vật phẩm lần hai.
            Attrition.Controllers.EnemyLootTracker.WriteTo(data);

            SaveManager.SaveSlot(slot, data);
        }

        /// <summary>
        /// Lưu NGAY world-state vào slot (solo), không cần chờ mốc rest/quit: boss đã hạ + vật phá được
        /// đã vỡ. Đây là các mốc quan trọng — chờ tới rest mà người chơi tắt game thì mất, boss hồi sinh
        /// và tường chắn lại đường đã mở.
        /// Chỉ ghi các field world-state, KHÔNG đụng stat/vị trí player (tránh đè tiến trình bằng
        /// dữ liệu giữa trận, vd HP đang thấp sau khi đánh boss).
        /// </summary>
        public void SaveWorldState()
        {
            if (GameLaunch.IsOnline) return;   // coop: chỉ giữ trong phiên host

            int slot = GameLaunch.SelectedSlot;
            var data = SaveManager.LoadSlot(slot);
            if (data == null) return;          // chưa có slot (chưa từng lưu) → bỏ qua, mốc sau sẽ ghi

            Attrition.Gameplay.Environment.BossDefeatState.WriteTo(data);
            Attrition.Gameplay.Environment.BreakableState.WriteTo(data);
            data.quests = Attrition.Gameplay.NPC.NetworkNPC.CaptureAll();
            data.lastSavedUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveManager.SaveSlot(slot, data);
        }

        /// <summary>
        /// Host-authoritative: HOST gom TOÀN BỘ dữ liệu của MỌI player (host + client) thành 1 payload
        /// và đẩy lên server bằng ĐÚNG 1 request (`sessions/bulk`). Server ghi tất cả trong 1
        /// transaction nên không còn cảnh player A lưu xong, player B lỗi mạng.
        ///
        /// Trước đây: N snapshot + N character-session + 1 meta = 3~4 request mỗi lần save, và snapshot
        /// CHỈ ghi cho nhân vật host (server lấy OwnerId từ JWT) nên tiến trình client không lên web.
        /// Nay mỗi entry mang ownerId riêng và server tự đối chiếu bảng characters trước khi ghi.
        /// </summary>
        private IEnumerator SaveAllOnline(SaveEvent evt, string checkpointId = null, Vector3? checkpointPos = null)
        {
            if (APIManager.Instance == null)
            {
                Debug.LogWarning("[Save:ONLINE] APIManager chưa sẵn sàng.");
                yield break;
            }

            // Access token sống ngắn (15 phút). Nếu CÒN refresh token, thử refresh CHỦ ĐỘNG để token
            // tươi trước khi lưu (phiên dài). NHƯNG refresh fail KHÔNG được chặn save — access token
            // hiện tại có thể vẫn sống → vẫn thử lưu bình thường. Chỉ khi chính các call lưu trả lỗi
            // mới báo người chơi. (Tránh regression: refresh trục trặc làm chết toàn bộ save.)
            if (!string.IsNullOrEmpty(APIManager.Instance.RefreshToken))
            {
                yield return APIManager.Instance.RefreshAccessToken(_ => { });
            }

            // Không có SessionId (tạo room trên server thất bại) thì không biết ghi vào phòng nào.
            if (string.IsNullOrEmpty(GameLaunch.SessionId))
            {
                Debug.LogWarning("[Save:ONLINE] BỎ QUA: chưa có SessionId (host chưa tạo được room trên server).");
                Attrition.Controllers.SaveNotifyEvents.RaiseFailed(
                    "Failed to save progress: this room isn't registered on the server.");
                yield break;
            }

            var characters = new System.Collections.Generic.List<APIManager.BulkCharacterDto>();

            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (player == null) continue;
                var stats = player.GetComponent<PlayerStats>();
                if (stats == null) continue;
                var prog = player.GetComponent<PlayerProgression>();
                var inv = player.GetComponent<Attrition.Gameplay.Player.Inventory.PlayerInventory>();
                var potions = player.GetComponent<PotionSystem>();

                // Danh tính chủ nhân nhân vật này. PHẢI đọc từ networked field trên PlayerInventory —
                // GameLaunch.* là static LOCAL của host, dùng nó sẽ gán cả 2 player về host.
                string ownerId = inv != null ? inv.OwnerUserId.ToString() : "";
                string charId = inv != null ? inv.OwnerCharacterId.ToString() : "";

                // Thiếu danh tính (chưa kịp sync / player lạ) → bỏ qua, tránh ghi nhầm người.
                if (string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(charId))
                {
                    Debug.LogWarning($"[Save:BULK] BỎ QUA 1 player: ownerId='{ownerId}' charId='{charId}' "
                                     + "(client chưa resolve characterId trên server).");
                    continue;
                }

                // Nhân vật host = peer có InputAuthority (trên máy host chỉ player của host có).
                bool isHostOwnPlayer = player.Object != null && player.Object.HasInputAuthority;

                // Vị trí: checkpoint (khi rest) hoặc vị trí hiện tại. checkpointPos là của HOST nên chỉ
                // áp cho player host; client vẫn lưu đúng chỗ nó đang đứng.
                Vector3 pos = (isHostOwnPlayer && checkpointPos.HasValue)
                    ? checkpointPos.Value
                    : player.transform.position;

                characters.Add(new APIManager.BulkCharacterDto
                {
                    characterId = charId,
                    ownerId = ownerId,
                    playerRole = (short)(isHostOwnPlayer ? 0 : 1),
                    name = string.IsNullOrEmpty(player.DisplayName.Value) ? "Wanderer" : player.DisplayName.Value,
                    archetype = "default",
                    currentLevel = prog != null ? prog.Level : stats.Level,
                    currentExp = prog != null ? prog.CurrentExp : 0,
                    allocatedPointsJson = JsonConvert.SerializeObject(CaptureAllocated(stats)),
                    maxHp = stats.MaxHP,
                    currentHp = stats.CurrentHP,
                    maxMana = stats.MaxMana,
                    currentMana = stats.CurrentMana,
                    maxStamina = stats.MaxStamina,
                    potionMaxFlasks = potions != null ? potions.MaxHealthCharges : 0,
                    potionMaxManaFlasks = potions != null ? potions.MaxManaCharges : 0,
                    healthCharges = potions != null ? potions.HealthCharges : 0,
                    manaCharges = potions != null ? potions.ManaCharges : 0,
                    attackSpeed = stats.AttackSpeed,
                    // Chỉ số cuối đã gộp base + điểm cộng + đồ. Web không tính lại được nên gửi sẵn.
                    ad = stats.AD,
                    ap = stats.AP,
                    def = stats.DEF,
                    res = stats.RES,
                    posX = pos.x,
                    posY = pos.y,
                    posZ = pos.z,
                    // checkpointId là của host (điểm host vừa rest) → chỉ gán cho host, tránh ghi
                    // sai điểm hồi sinh của client. null = server giữ giá trị cũ.
                    lastRestPointId = isHostOwnPlayer ? checkpointId : null,
                    inventoryJson = inv != null ? inv.ExportJson() : null,
                    equipmentJson = null,   // đồ đang trang bị nằm trong inventoryJson (equipped*)
                    deathCount = stats.DeathCount,
                    isAlive = !player.IsDead
                });
            }

            // World flags của PHÒNG (host-authoritative, không theo từng player):
            //  - quest  : stateValue/progress thật từ NetworkNPC.
            //  - boss   : đã hạ (stateValue 1).
            //  - cp:*   : checkpoint đã khám phá (prefix để không đụng namespace của quest/boss).
            var worldStates = new System.Collections.Generic.List<APIManager.BulkWorldStateDto>();
            AppendQuestStates(worldStates);

            foreach (var bossId in Attrition.Gameplay.Environment.BossDefeatState.AllDefeated)
                if (!string.IsNullOrEmpty(bossId))
                    worldStates.Add(new APIManager.BulkWorldStateDto { eventId = bossId, stateValue = 1, progress = 0 });

            foreach (var cp in Attrition.Gameplay.Environment.WorldMapState.AllDiscoveredCheckpoints)
                if (!string.IsNullOrEmpty(cp))
                    worldStates.Add(new APIManager.BulkWorldStateDto { eventId = CheckpointEventId(cp), stateValue = 1, progress = 0 });

            var req = new APIManager.BulkSaveRequest
            {
                sessionId = GameLaunch.SessionId,
                playTimeSeconds = TotalPlaytimeSeconds,
                // Ghi GameLaunch.GameplayScene — nguồn DUY NHẤT đáng tin cho tên map đang chơi. KHÔNG
                // dùng SceneManager.GetActiveScene() (coop load additive nên có thể trả menu) hay
                // gameObject.scene (trả scene runner riêng của Fusion). Load-back so khớp CÙNG giá trị.
                currentScene = GameLaunch.GameplayScene,
                fogJson = JsonConvert.SerializeObject(
                    new System.Collections.Generic.List<string>(Attrition.Gameplay.Environment.WorldMapState.AllFog)),
                eventType = evt.ToString().ToLowerInvariant(),
                roomCode = GameLaunch.RoomCode,
                characters = characters,
                worldStates = worldStates
            };

            APIManager.BulkSaveResultDto result = null;
            yield return APIManager.Instance.SaveBulk(req, r => result = r);

            if (result == null)
            {
                Attrition.Controllers.SaveNotifyEvents.RaiseFailed(
                    "Failed to save progress. Check your connection — your latest progress may not be saved.");
                yield break;
            }

            // Server trả 200 nhưng có thể TỪ CHỐI một số nhân vật (ownerId không khớp bảng characters).
            // Không log thì lỗi này vô hình: người chơi thấy "saved" mà tiến trình 1 người không lên.
            if (result.skipped != null && result.skipped.Count > 0)
            {
                Debug.LogError($"[Save:BULK] Server TỪ CHỐI {result.skipped.Count} nhân vật (ownerId không khớp): "
                               + string.Join(", ", result.skipped));
                Attrition.Controllers.SaveNotifyEvents.RaiseFailed(
                    "Some progress could not be saved — a character wasn't recognised by the server.");
                yield break;
            }

            // Gửi player nhưng server không ghi được ai → coi là thất bại, đừng báo xanh.
            if (characters.Count > 0 && result.charactersSaved == 0)
            {
                Debug.LogError("[Save:BULK] Không nhân vật nào được ghi dù payload có dữ liệu.");
                Attrition.Controllers.SaveNotifyEvents.RaiseFailed(
                    "Failed to save progress. Please try again.");
                yield break;
            }

            Attrition.Controllers.SaveNotifyEvents.RaiseOk("Progress saved.");
        }

        /// <summary>
        /// Quest world-state: NetworkNPC lưu dạng JSON (questId/state/progress). Parse ra từng row để
        /// server giữ được state + progress thật, thay vì nhét cả khối JSON vào snapshot như trước.
        /// JSON hỏng → bỏ qua quest, các phần còn lại của save vẫn đi.
        /// </summary>
        private void AppendQuestStates(System.Collections.Generic.List<APIManager.BulkWorldStateDto> dest)
        {
            string questsJson = Attrition.Gameplay.NPC.NetworkNPC.CaptureAllJson();
            if (string.IsNullOrEmpty(questsJson)) return;
            try
            {
                var list = JsonConvert.DeserializeObject<QuestProgressList>(questsJson);
                if (list?.quests == null) return;
                foreach (var q in list.quests)
                {
                    if (q == null || string.IsNullOrEmpty(q.questId)) continue;
                    dest.Add(new APIManager.BulkWorldStateDto
                    {
                        eventId = QuestEventId(q.questId),
                        stateValue = q.state,
                        progress = q.progress
                    });
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Save:BULK] Parse quest JSON lỗi, bỏ qua quest: {e.Message}");
            }
        }

        // Prefix để 3 loại world-state (quest / boss / checkpoint) không đụng eventId của nhau.
        // Cột EventId chỉ 50 ký tự nên prefix phải ngắn.
        private const string QuestPrefix = "q:";
        private const string CheckpointPrefix = "cp:";
        private static string QuestEventId(string questId) => QuestPrefix + questId;
        private static string CheckpointEventId(string checkpointId) => CheckpointPrefix + checkpointId;

        /// <summary>Bóc prefix "cp:" → id checkpoint. Trả null nếu không phải row checkpoint.</summary>
        public static string ParseCheckpointEventId(string eventId)
            => !string.IsNullOrEmpty(eventId) && eventId.StartsWith(CheckpointPrefix, System.StringComparison.Ordinal)
                ? eventId.Substring(CheckpointPrefix.Length)
                : null;

        /// <summary>
        /// Bóc prefix "q:" → questId. Trả null nếu KHÔNG phải row quest.
        /// Row cũ (lưu trước khi có prefix) không có "q:" nhưng cũng không có "cp:" và không phải boss
        /// nào — không phân biệt được với boss, nên coi row-không-prefix là boss (xem IsBossEventId) và
        /// quest cũ sẽ mất 1 lần; lần save kế tiếp ghi lại đúng prefix.
        /// </summary>
        public static string ParseQuestEventId(string eventId)
            => !string.IsNullOrEmpty(eventId) && eventId.StartsWith(QuestPrefix, System.StringComparison.Ordinal)
                ? eventId.Substring(QuestPrefix.Length)
                : null;

        /// <summary>True nếu row world-state là boss đã hạ (không prefix, stateValue = 1).</summary>
        public static bool IsBossEventId(string eventId)
            => !string.IsNullOrEmpty(eventId)
               && !eventId.StartsWith(QuestPrefix, System.StringComparison.Ordinal)
               && !eventId.StartsWith(CheckpointPrefix, System.StringComparison.Ordinal);

        private int[] CaptureAllocated(PlayerStats stats)
        {
            var arr = new int[7];
            for (int i = 0; i < 7 && i < stats.AllocatedPoints.Length; i++)
                arr[i] = stats.AllocatedPoints.Get(i);
            return arr;
        }

        private PlayerController FindLocalPlayer()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                // Single: chỉ 1 player. Coop: lưu cho player local (InputAuthority).
                if (pc.Object == null) return pc;            // single (không network object)
                if (pc.Object.HasInputAuthority) return pc;  // coop local
            }
            // fallback: player đầu tiên
            var all = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }
    }
}
