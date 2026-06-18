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

        // ─────────────────────────────── LOCAL (solo) ───────────────────────────────
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
                data.checkpointX = checkpointPos.Value.x;
                data.checkpointY = checkpointPos.Value.y;
                data.checkpointZ = checkpointPos.Value.z;
            }

            if (potions != null) data.potionMaxFlasks = potions.MaxHealthCharges;
            data.allocatedPoints = CaptureAllocated(stats);
            data.quests = Attrition.Gameplay.NPC.NetworkNPC.CaptureAll();
            data.originMode = GameLaunch.Mode.ToString();
            data.lastSavedUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            SaveManager.SaveSlot(slot, data);
            Debug.Log($"[Save:LOCAL] slot {slot} ({evt}) Lv{data.level} HP{data.currentHP} @{data.checkpointId}");
        }

        // ─────────────────────────────── ONLINE (coop) ──────────────────────────────
        /// <summary>
        /// Host-authoritative: HOST lưu hộ MỌI player (host + client) lên server, mỗi người theo
        /// character của CHÍNH HỌ (OwnerUserId/OwnerCharacterId networked trên PlayerInventory).
        /// Quest world-state chỉ đính kèm 1 lần vào snapshot của nhân vật HOST.
        /// </summary>
        private IEnumerator SaveAllOnline(SaveEvent evt, string checkpointId = null, Vector3? checkpointPos = null)
        {
            if (APIManager.Instance == null)
            {
                Debug.LogWarning("[Save:ONLINE] APIManager chưa sẵn sàng.");
                yield break;
            }

            // Quest world-state là của host → gom 1 lần, gắn vào nhân vật host.
            string questsJson = Attrition.Gameplay.NPC.NetworkNPC.CaptureAllJson();

            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (player == null) continue;
                var stats = player.GetComponent<PlayerStats>();
                if (stats == null) continue;
                var prog = player.GetComponent<PlayerProgression>();
                var inv = player.GetComponent<Attrition.Gameplay.Player.Inventory.PlayerInventory>();

                // Danh tính chủ nhân nhân vật này (host ghi/đọc qua networked field).
                string ownerId = inv != null ? inv.OwnerUserId.ToString() : "";
                string charId = inv != null ? inv.OwnerCharacterId.ToString() : "";

                // Không có danh tính (chưa kịp sync / player lạ) → bỏ qua, tránh ghi nhầm.
                if (string.IsNullOrEmpty(ownerId)) continue;

                // Nhân vật host = peer vừa có InputAuthority vừa StateAuthority (host điều khiển).
                bool isHostOwnPlayer = player.Object != null && player.Object.HasInputAuthority;

                string invJson = inv != null ? inv.ExportJson() : null;

                var req = new APIManager.SnapshotIngestRequest
                {
                    ownerId = ownerId,
                    characterId = string.IsNullOrEmpty(charId) ? null : charId,
                    name = string.IsNullOrEmpty(player.DisplayName.Value) ? "Wanderer" : player.DisplayName.Value,
                    archetype = "default",
                    level = prog != null ? prog.Level : stats.Level,
                    hp = stats.CurrentHP,
                    maxHp = stats.MaxHP,
                    gold = 0,
                    isAlive = !player.IsDead,
                    roomCode = GameLaunch.RoomCode,
                    eventType = evt.ToString().ToLowerInvariant(),
                    playtimeSeconds = TotalPlaytimeSeconds,
                    inventoryJson = invJson,
                    // Quest world-state chỉ gắn vào nhân vật host (1 lần), client gửi null.
                    questsJson = isHostOwnPlayer ? questsJson : null
                };

                yield return APIManager.Instance.PostSnapshot(req, ok =>
                {
                    Debug.Log($"[Save:ONLINE] {ownerId} ({evt}) → {(ok ? "OK" : "FAILED")}");
                });

                // PER-ROOM: ngoài snapshot (lịch sử theo character), đẩy tiến trình đầy đủ vào
                // character_session theo (charId, SessionId) — stat/điểm cộng/bình/vị trí/đồ của
                // ĐÚNG người trong ĐÚNG room. Chỉ khi host đã có SessionId (tạo room server OK)
                // và character này có id (đã resolve trên server). Thiếu 1 trong 2 → bỏ qua phần này,
                // snapshot ở trên vẫn lưu nên không mất tiến trình cốt lõi.
                if (!string.IsNullOrEmpty(GameLaunch.SessionId) && !string.IsNullOrEmpty(charId))
                {
                    var potions = player.GetComponent<PotionSystem>();
                    Vector3 pos = checkpointPos ?? player.transform.position;

                    var csReq = new APIManager.SaveCharacterSessionRequest
                    {
                        characterId = charId,
                        sessionId = GameLaunch.SessionId,
                        playerRole = (short)(isHostOwnPlayer ? 0 : 1),
                        currentLevel = prog != null ? prog.Level : stats.Level,
                        currentExp = prog != null ? prog.CurrentExp : 0,
                        allocatedPointsJson = JsonConvert.SerializeObject(CaptureAllocated(stats)),
                        maxHp = stats.MaxHP,
                        currentHp = stats.CurrentHP,
                        maxMana = stats.MaxMana,
                        currentMana = stats.CurrentMana,
                        maxStamina = stats.MaxStamina,
                        potionMaxFlasks = potions != null ? potions.MaxHealthCharges : 0,
                        attackSpeed = stats.AttackSpeed,
                        posX = pos.x,
                        posY = pos.y,
                        lastRestPointId = checkpointId,
                        // Đồ đẩy lại cùng giá trị với snapshot (đã export ở trên); null = giữ nguyên.
                        inventoryJson = invJson,
                        equipmentJson = null
                    };

                    yield return APIManager.Instance.SaveCharacterSession(csReq, ok =>
                    {
                        Debug.Log($"[Save:ROOM] {charId}@{GameLaunch.SessionId} ({evt}) → {(ok ? "OK" : "FAILED")}");
                    });
                }
            }

            // PER-ROOM quest world-state: host đẩy meta phòng (playtime/scene) khi có SessionId.
            if (!string.IsNullOrEmpty(GameLaunch.SessionId))
            {
                var metaReq = new APIManager.UpdateSessionRequest
                {
                    sessionId = GameLaunch.SessionId,
                    playTimeSeconds = TotalPlaytimeSeconds,
                    currentScene = GameLaunch.GameplayScene
                };
                yield return APIManager.Instance.UpdateSessionMeta(metaReq, _ => { });
            }
        }

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
