using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Attrition.Data;

namespace Attrition.Gameplay.Player.Inventory
{
    /// <summary>
    /// Hệ thống túi đồ chính — gắn vào Player Prefab cùng cấp PlayerStats/PlayerCombat.
    /// Host-authoritative: mọi thay đổi chỉ host thực thi, client gửi RPC request.
    /// 
    /// Inventory chia 3 nhóm (tổng 64 ô):
    ///   EquipmentSlots[40] — trang bị (Head/Chest/Legs/Boots) + skill SO
    ///   AccessorySlots[10] — DamageEffect &amp; AbilityGrant
    ///   MaterialSlots[14]  — nguyên liệu, key item
    /// 
    /// 6 equip slot riêng: 4 armor + 1 skill + 1 damage-accessory.
    /// Mỗi thay đổi → CommitLocalSave() (BR-46).
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerInventory : NetworkBehaviour
    {
        [Networked, Capacity(40)] public NetworkArray<InventorySlot> EquipmentSlots { get; }
        [Networked, Capacity(10)] public NetworkArray<InventorySlot> AccessorySlots { get; }
        [Networked, Capacity(14)] public NetworkArray<InventorySlot> MaterialSlots { get; }

        // Định danh chủ nhân của nhân vật này (host ghi/đọc để LOAD + LƯU đồ đúng người trong coop).
        // Host's own player: host ghi thẳng. Client's player: client gửi qua RpcSetOwnerIdentity.
        [Networked] public NetworkString<_64> OwnerUserId { get; set; }
        [Networked] public NetworkString<_64> OwnerCharacterId { get; set; }

        [Networked] public InventorySlot EquippedHead { get; set; }
        [Networked] public InventorySlot EquippedChest { get; set; }
        [Networked] public InventorySlot EquippedLegs { get; set; }
        [Networked] public InventorySlot EquippedBoots { get; set; }
        [Networked] public InventorySlot EquippedSkill { get; set; }
        [Networked] public InventorySlot EquippedAccessory { get; set; }

        private PlayerStats _stats;
        private ItemDatabaseSO _db;

        /// <summary>Flag set bởi PlayerController khi đang trong vùng Boss (BR-17).</summary>
        [System.NonSerialized] public bool IsInBossZone;

        /// <summary>
        /// Được phép ĐỔI ACCESSORY chưa? Yêu cầu user: CHỈ đổi khi đang đứng tại điểm checkpoint.
        ///
        /// Đọc `PlayerController.AtCheckpointNet` ([Networked]) chứ KHÔNG dùng `IsAtCheckpoint` — cái sau
        /// chỉ có giá trị trên máy giữ InputAuthority, còn gate này chạy ở HOST (StateAuthority) nên trong
        /// coop host sẽ luôn thấy false cho client và client không bao giờ đổi được.
        ///
        /// Không tìm thấy PlayerController (prefab lạ/test scene) → cho phép, để không khoá cứng đồ chơi thử.
        /// </summary>
        private bool CanSwapAccessory
        {
            get
            {
                var pc = GetComponent<PlayerController>();
                if (pc == null) return true;
                return pc.AtCheckpointNet;
            }
        }

        /// <summary>Event UI lắng nghe để refresh grid.</summary>
        public event System.Action OnInventoryChanged;

        [Header("---- STARTING ITEMS (host seed) ----")]
        [Tooltip("String id của item phát cho nhân vật mới (vd: iron_helm, skill_fire). Chỉ host seed, chỉ khi túi đang trống. Để TEST: mặc định phát sẵn vài món.")]
        [SerializeField] private string[] startingItemIds = new string[0];

        // Checksum inventory (client) để phát hiện networked slot đổi trong Render → rebuild gear + refresh UI.
        // CLIENT không chạy NotifyChanged/RebuildAndApplyGear (host-only) nên nếu không tự phát hiện, icon
        // grid + chỉ số gear (MaxHP/DEF...) của client KHÔNG BAO GIỜ cập nhật khi host sync đồ xuống.
        private int _lastInvChecksum = int.MinValue;

        public override void Spawned()
        {
            _stats = GetComponent<PlayerStats>();
            _db = ItemDatabaseSO.Instance;

            if (_db == null)
            {
                Debug.LogError("[PlayerInventory] ItemDatabaseSO.Instance chưa được set!");
                return;
            }

            // SOLO (không online): host=single, load local ngay.
            if (!Attrition.Persistence.GameLaunch.IsOnline)
            {
                if (HasStateAuthority)
                {
                    LoadFromLocal();
                    SeedStartingItems();
                }
                return;
            }

            // ONLINE COOP: nạp đồ phải theo ĐÚNG chủ nhân của nhân vật này, không dùng GameLaunch
            // chung (trên host, GameLaunch.CharacterId là của host → sẽ nạp nhầm đồ host cho client).
            // Peer sở hữu (InputAuthority) biết danh tính của mình → đẩy lên host:
            //   - Host's own player: host vừa StateAuthority vừa InputAuthority → ghi thẳng rồi load.
            //   - Client's player:   chỉ client có InputAuthority → gửi RPC; host nhận → load.
            if (HasInputAuthority)
            {
                string ownerId = Attrition.Persistence.GameLaunch.OwnerId ?? "";
                string charId = Attrition.Persistence.GameLaunch.CharacterId ?? "";

                if (HasStateAuthority)
                {
                    OwnerUserId = ownerId;
                    OwnerCharacterId = charId;
                    StartCoroutine(LoadOnlineInventory(charId, isOwningPeerHere: true));
                }
                else
                {
                    RpcSetOwnerIdentity(PackGuid(ownerId) + PackGuid(charId));
                }
            }
        }

        // CLIENT: phát hiện networked inventory đổi (host sync xuống) rồi rebuild gear + refresh UI. Host
        // đã tự làm qua NotifyChanged khi thao tác; client KHÔNG có trigger đó nên tự dò checksum mỗi frame.
        private float _nextInvPoll;

        public override void Render()
        {
            if (HasStateAuthority || _db == null) return; // host tự xử lý; chỉ client cần dò.

            // Dò 5 lần/giây thay vì mỗi frame: quét 64 ô NetworkArray ở 144fps là chi phí thuần vô ích
            // (đồ chỉ đổi khi có thao tác), và nó nằm trong Render nên tính vào mọi frame của client.
            if (Time.unscaledTime < _nextInvPoll) return;
            _nextInvPoll = Time.unscaledTime + 0.2f;

            int sum = ComputeInventoryChecksum();
            if (sum == _lastInvChecksum) return;
            _lastInvChecksum = sum;

            // Slot đã đổi → dựng lại chỉ số theo đồ đang mặc (MaxHP/DEF...) + báo UI vẽ lại icon grid.
            RebuildAndApplyGear();
            OnInventoryChanged?.Invoke();
        }

        // Tổng hợp trạng thái mọi ô + 6 slot mặc thành 1 số để so đổi (rẻ, ~70 ô).
        private int ComputeInventoryChecksum()
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < EquipmentSlots.Length; i++) { var s = EquipmentSlots.Get(i); h = h * 31 + s.ItemIndex; h = h * 31 + s.Amount; }
                for (int i = 0; i < AccessorySlots.Length; i++) { var s = AccessorySlots.Get(i); h = h * 31 + s.ItemIndex; h = h * 31 + s.Amount; }
                for (int i = 0; i < MaterialSlots.Length; i++) { var s = MaterialSlots.Get(i); h = h * 31 + s.ItemIndex; h = h * 31 + s.Amount; }
                h = h * 31 + EquippedHead.ItemIndex;
                h = h * 31 + EquippedChest.ItemIndex;
                h = h * 31 + EquippedLegs.ItemIndex;
                h = h * 31 + EquippedBoots.ItemIndex;
                h = h * 31 + EquippedSkill.ItemIndex;
                h = h * 31 + EquippedAccessory.ItemIndex;
                return h;
            }
        }

        /// <summary>
        /// Client báo danh tính chủ nhân lên host; host ghi state + fetch đồ đúng character.
        /// GỘP owner+char vào 1 NetworkString để tránh vượt giới hạn payload RPC 512 byte
        /// (2× NetworkString&lt;_64&gt; = 528 byte > 512 → RPC bị huỷ, host không nhận được danh tính).
        /// GUID bỏ dấu '-' = 32 hex; ghép owner(32)+char(32) = 64 ký tự vừa khít _64.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcSetOwnerIdentity(NetworkString<_64> packed)
        {
            string s = packed.ToString();
            string ownerId = UnpackGuid(s.Length >= 32 ? s.Substring(0, 32) : s);
            string charId = UnpackGuid(s.Length >= 64 ? s.Substring(32, 32) : "");
            OwnerUserId = ownerId;
            OwnerCharacterId = charId;
            StartCoroutine(LoadOnlineInventory(charId, isOwningPeerHere: false));
        }

        /// <summary>Bỏ dấu '-' của GUID → 32 hex (PadRight nếu thiếu) để gộp 2 GUID vào 1 NetworkString&lt;_64&gt;.</summary>
        private static string PackGuid(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new string('0', 32);
            string hex = raw.Replace("-", "");
            if (hex.Length > 32) hex = hex.Substring(0, 32);
            else if (hex.Length < 32) hex = hex.PadRight(32, '0');
            return hex;
        }

        /// <summary>32 hex → GUID chuẩn 8-4-4-4-12 (khôi phục dạng có dấu '-' để khớp host-own-player + API).</summary>
        private static string UnpackGuid(string hex)
        {
            return System.Guid.TryParseExact(hex, "N", out var g) ? g.ToString() : "";
        }

        /// <summary>
        /// Host nạp đồ cho nhân vật này theo ĐÚNG cặp (charId, session hiện tại) — KHÔNG phải đồ toàn cục
        /// của character. Fetch session detail 1 lần (cache vào GameLaunch), rồi đọc inventoryJson của
        /// charId trong session đó. Không có row → túi trống → seed tân thủ (đây chính là hành vi
        /// "session mới / char mới vào session = đồ trống" mong muốn). Quest world-state theo session,
        /// chỉ nạp 1 lần (gắn vào nhân vật host).
        /// </summary>
        private System.Collections.IEnumerator LoadOnlineInventory(string charId, bool isOwningPeerHere)
        {
            yield return EnsureSessionLoaded(isOwningPeerHere);

            // Hydrate STAT coop (level/exp/điểm cộng/HP/Mana/số bình) TRƯỚC khi nạp đồ — đồ đắp lên có
            // thể đổi MaxHP nên set CurrentHP xong ở đây rồi đồ điều chỉnh max sau là đúng thứ tự. Chỉ
            // host (StateAuthority trên player này). Không có cache = char mới → giữ mặc định.
            if (HasStateAuthority && !string.IsNullOrEmpty(charId)
                && Attrition.Persistence.GameLaunch.SessionStatsByChar.TryGetValue(charId, out var statDto))
            {
                var st = GetComponent<PlayerStats>();
                if (st != null) st.HydrateFromCoopSession(statDto);
            }
            else if (HasStateAuthority)
            {
                Debug.LogWarning($"[Hydrate] KHÔNG hydrate stat: charId='{charId}' "
                                 + $"cóCache={(!string.IsNullOrEmpty(charId) && Attrition.Persistence.GameLaunch.SessionStatsByChar.ContainsKey(charId))}. "
                                 + "charId rỗng hoặc cache trống = stat/bình về mặc định.");
            }

            if (!string.IsNullOrEmpty(charId)
                && Attrition.Persistence.GameLaunch.SessionInventoryByChar.TryGetValue(charId, out var invJson)
                && !string.IsNullOrEmpty(invJson))
            {
                ImportJson(invJson);
            }

            SeedStartingItems(); // chỉ seed nếu túi vẫn trống sau khi nạp từ session

            // Spawn ĐÚNG checkpoint đã lưu của session: host (StateAuthority trên player này) teleport
            // tới vị trí rest đã lưu nếu thuộc scene hiện tại. Làm SAU EnsureSessionLoaded để chắc
            // chắn cache vị trí đã có. Chưa rest / scene khác → giữ nguyên spawnPoint mặc định.
            if (HasStateAuthority && !string.IsNullOrEmpty(charId))
            {
                // COOP: KHÔNG dùng SceneManager.GetActiveScene() (trả 'Main_Menu_UI' vì load additive)
                // và KHÔNG dùng gameObject.scene.name (trả scene runner riêng của Fusion, vd
                // 'NetworkLauncher_[Player:1]' trong multi-peer). Cả hai đều KHÔNG phải tên map.
                // Nguồn duy nhất đáng tin cho "map mà room này đang chơi" là GameLaunch.GameplayScene —
                // save cũng ghi cùng giá trị này nên so khớp luôn nhất quán.
                string activeScene = Attrition.Persistence.GameLaunch.GameplayScene;
                if (Attrition.Persistence.GameLaunch.SessionRestPosByChar.TryGetValue(charId, out var rest)
                    && (rest.x != 0f || rest.y != 0f))
                {
                    // ĐÒI KHỚP DƯƠNG: chỉ teleport khi biết CHẮC toạ độ thuộc scene hiện tại. Nhãn rỗng =
                    // "không rõ map nào" (xem chỗ ghi cache) và trước đây nhánh đó được coi là hợp lệ →
                    // toạ độ map cũ áp vào map mới, lọt vào lòng địa hình vì bounds các map chồng lấn.
                    // Không rõ thì dùng spawnPoint của map: sai chỗ đứng còn sửa được, kẹt trong đất thì không.
                    if (rest.scene != activeScene)
                    {
                        Debug.LogWarning($"[Spawn] char {charId}: vị trí rest ({rest.x:F1},{rest.y:F1}) thuộc "
                                         + $"scene '{rest.scene ?? "?"}' ≠ '{activeScene}' → spawnPoint mặc định.");
                    }
                    else
                    {
                        var pc = GetComponent<PlayerController>();
                        if (pc != null)
                            pc.TeleportTo(new Vector3(rest.x, rest.y, 0f));
                    }
                }
            }
        }

        /// <summary>
        /// Host fetch session detail (GET /sessions/{id}, JWT host) MỘT LẦN, đổ inventoryJson từng
        /// character vào GameLaunch.SessionInventoryByChar và quest world-state vào CoopQuestsJson.
        /// Các PlayerInventory sau đọc thẳng từ cache (không gọi API lại). Nếu chưa có SessionId
        /// (chưa tạo room server) → bỏ qua, mọi người sẽ seed tân thủ.
        /// </summary>
        private System.Collections.IEnumerator EnsureSessionLoaded(bool isOwningPeerHere)
        {
            // Player KHÁC (không phải người fetch) → chờ tới khi fetch xong rồi mới đọc cache.
            if (Attrition.Persistence.GameLaunch.SessionInventoryFetchStarted)
            {
                while (!Attrition.Persistence.GameLaunch.SessionInventoryLoaded) yield return null;
                yield break;
            }
            Attrition.Persistence.GameLaunch.SessionInventoryFetchStarted = true; // claim fetch (idempotent)

            string sessionId = Attrition.Persistence.GameLaunch.SessionId;
            if (APIManager.Instance != null && !string.IsNullOrEmpty(sessionId))
            {
                yield return APIManager.Instance.GetSession(sessionId, detail =>
                {
                    if (detail == null) { Debug.LogWarning("[SessionLoad] GetSession trả null → không nạp được stat/đồ."); return; }

                    if (detail.characters != null)
                    {
                        foreach (var cs in detail.characters)
                        {
                            if (cs == null || string.IsNullOrEmpty(cs.characterId)) continue;
                            Attrition.Persistence.GameLaunch.SessionInventoryByChar[cs.characterId] = cs.inventoryJson;
                            // Cache vị trí rest đã lưu để spawn đúng checkpoint. Scene của vị trí phải suy
                            // từ CHECKPOINT RIÊNG của nhân vật, KHÔNG phải detail.currentScene: cái sau là
                            // map PHÒNG đang chơi, còn posX/posY là của RIÊNG nhân vật này. Hai thứ lệch
                            // nhau khi một người rời phòng ở map cũ rồi người còn lại đi tiếp sang map mới
                            // — bulk save chỉ ghi row của player CÒN TRONG PHÒNG, nên row người kia giữ
                            // toạ độ map CŨ mà lại được dán nhãn map MỚI → guard scene bên dưới cho qua và
                            // teleport thẳng vào lòng địa hình (bounds 5 map chồng lấn nhau nên toạ độ map
                            // A gần như luôn "trông hợp lệ" ở map B) → đúng lỗi "camera nằm dưới đất".
                            // Không tra được id (chưa rest lần nào, hoặc row lưu trước thay đổi này) → để
                            // nhãn RỖNG, tức "không biết toạ độ này thuộc map nào". Tuyệt đối KHÔNG rơi về
                            // detail.currentScene: đoán sai chính là nguồn của bug trên.
                            var restMap = Attrition.Gameplay.Environment.MapRegistrySO.Load()
                                ?.GetByCheckpoint(cs.lastRestPointId);
                            Attrition.Persistence.GameLaunch.SessionRestPosByChar[cs.characterId] =
                                (cs.posX, cs.posY, restMap != null ? restMap.sceneName : null);
                            // Cache DTO stat đầy đủ (level/exp/điểm cộng/HP/Mana/số bình) để PlayerStats
                            // hydrate stat coop khi spawn — đối xứng với solo đọc save slot.
                            Attrition.Persistence.GameLaunch.SessionStatsByChar[cs.characterId] = cs;
                        }
                    }

                    // Quest world-state của session (host-authoritative). Khôi phục NPC theo tiến trình session.
                    string questsJson = BuildQuestsJson(detail.worldStates);
                    Attrition.Persistence.GameLaunch.CoopQuestsJson = questsJson;
                    Attrition.Gameplay.NPC.NetworkNPC.ApplyAllJson(questsJson);

                    ApplyCoopWorldProgress(detail);
                });
            }

            Attrition.Persistence.GameLaunch.SessionInventoryLoaded = true; // mở khoá cho player đang chờ
        }

        /// <summary>Map worldStates (per session: eventId/stateValue/progress) → QuestProgressList JSON
        /// mà NetworkNPC.ApplyAllJson hiểu (questId/state/progress).
        /// CHỈ lấy row có prefix "q:" — cùng bảng world-state còn chứa boss đã hạ và checkpoint đã mở,
        /// nhét chúng vào đây thì NetworkNPC nhận quest rác.</summary>
        private static string BuildQuestsJson(System.Collections.Generic.List<APIManager.WorldStateDto> states)
        {
            if (states == null || states.Count == 0) return "";
            var list = new Attrition.Persistence.QuestProgressList();
            var entries = new System.Collections.Generic.List<Attrition.Persistence.QuestProgressEntry>();
            foreach (var ws in states)
            {
                if (ws == null) continue;
                string questId = Attrition.Gameplay.Persistence.GameSaveService.ParseQuestEventId(ws.eventId);
                if (string.IsNullOrEmpty(questId)) continue;
                entries.Add(new Attrition.Persistence.QuestProgressEntry
                {
                    questId = questId,
                    state = (byte)ws.stateValue,
                    progress = ws.progress
                });
            }
            if (entries.Count == 0) return "";
            list.quests = entries.ToArray();
            return UnityEngine.JsonUtility.ToJson(list);
        }

        /// <summary>
        /// COOP: khôi phục tiến trình cấp PHÒNG từ session detail — boss đã hạ, checkpoint đã khám phá,
        /// fog đã mở. Trước đây 3 thứ này chỉ nằm trong RAM host nên reopen phòng là boss sống lại,
        /// bản đồ tối lại và fast-travel mất điểm đến.
        /// </summary>
        private static void ApplyCoopWorldProgress(APIManager.SessionDetailDto detail)
        {
            var bosses = new System.Collections.Generic.List<string>();
            var checkpoints = new System.Collections.Generic.List<string>();

            if (detail.worldStates != null)
            {
                foreach (var ws in detail.worldStates)
                {
                    if (ws == null || ws.stateValue <= 0) continue;

                    string cp = Attrition.Gameplay.Persistence.GameSaveService.ParseCheckpointEventId(ws.eventId);
                    if (!string.IsNullOrEmpty(cp)) { checkpoints.Add(cp); continue; }

                    if (Attrition.Gameplay.Persistence.GameSaveService.IsBossEventId(ws.eventId))
                        bosses.Add(ws.eventId);
                }
            }

            // Hợp nhất theo session: fetch lại CÙNG phòng (đổi map) KHÔNG được xoá boss/fog vừa mở mà
            // server chưa kịp lưu; sang phòng khác thì thay thế sạch.
            string sessionId = Attrition.Persistence.GameLaunch.SessionId;
            Attrition.Gameplay.Environment.BossDefeatState.LoadFromIds(bosses, sessionId);

            // fogJson hỏng → giữ fog đang có (null) thay vì xoá sạch bản đồ của người chơi.
            System.Collections.Generic.List<string> fog = null;
            if (!string.IsNullOrEmpty(detail.fogJson))
            {
                try
                {
                    fog = Newtonsoft.Json.JsonConvert
                        .DeserializeObject<System.Collections.Generic.List<string>>(detail.fogJson);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SessionLoad] fogJson lỗi, giữ fog hiện tại: {e.Message}");
                }
            }
            Attrition.Gameplay.Environment.WorldMapState.LoadFromCoop(fog, checkpoints, sessionId);

            // Beacon checkpoint: Spawned() của chúng có thể đã chạy trước khi fetch về → quét lại.
            Attrition.Gameplay.World.Checkpoint.ApplyCoopDiscovered();
        }

        /// <summary>Phát item khởi đầu cho nhân vật mới (host-only, chỉ khi túi trống).</summary>
        private void SeedStartingItems()
        {
            if (startingItemIds == null || startingItemIds.Length == 0) return;
            // Chỉ seed khi cả 3 nhóm đang trống (tránh ghi đè save đã load).
            if (!EquipmentSlots.Get(0).IsEmpty || !AccessorySlots.Get(0).IsEmpty) return;

            foreach (var id in startingItemIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                int idx = _db.GetIndex(id);
                if (idx >= 0)
                    TryAddItem(idx, 1);
                else Debug.LogWarning($"[Seed] '{id}' KHÔNG có trong ItemDatabase.");
            }
        }


        /// <summary>Thêm vật phẩm vào túi đồ. Trả false nếu đầy (BR-40). Chỉ host.</summary>
        public bool TryAddItem(int itemIndex, int amount = 1)
        {
            if (!HasStateAuthority || _db == null || amount <= 0) return false;
            int remaining = AddItemInternal(itemIndex, amount);
            if (remaining >= amount) return false; // không thêm được gì cả
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// Thêm item vào túi; phần KHÔNG vừa (túi đầy) sẽ VĂNG ra thế giới gần player.
        /// Dùng cho phần thưởng quest (BR-40): item luôn tới tay player, đầy thì rơi ra đất.
        /// Chỉ host.
        /// </summary>
        public void AddItemOrDrop(int itemIndex, int amount = 1)
        {
            if (!HasStateAuthority || _db == null || amount <= 0) return;
            int remaining = AddItemInternal(itemIndex, amount);
            if (remaining > 0) SpawnDroppedItem(itemIndex, remaining, Object.InputAuthority);
            if (remaining < amount) NotifyChanged();
        }

        /// <summary>Lõi thêm item: stack + lấp ô trống. Trả về số lượng KHÔNG thêm được (dư vì đầy).</summary>
        private int AddItemInternal(int itemIndex, int amount)
        {
            var item = _db.GetItem(itemIndex);
            if (item == null) return amount;

            var arr = GetArrayForCategory(item.Category);
            if (arr.Length == 0) return amount;

            int remaining = amount;

            // 1) Stack vào ô đã có (nếu stackable)
            if (Attrition.Persistence.ItemRuntimeConfig.MaxStack(item) > 1)
            {
                for (int i = 0; i < arr.Length && remaining > 0; i++)
                {
                    var slot = arr.Get(i);
                    if (slot.ItemIndex == itemIndex && slot.Amount < Attrition.Persistence.ItemRuntimeConfig.MaxStack(item))
                    {
                        int canAdd = Mathf.Min(remaining, Attrition.Persistence.ItemRuntimeConfig.MaxStack(item) - slot.Amount);
                        slot.Amount += canAdd;
                        arr.Set(i, slot);
                        remaining -= canAdd;
                    }
                }
            }

            // 2) Tìm ô trống
            for (int i = 0; i < arr.Length && remaining > 0; i++)
            {
                var slot = arr.Get(i);
                if (slot.IsEmpty)
                {
                    int canAdd = Mathf.Min(remaining, Attrition.Persistence.ItemRuntimeConfig.MaxStack(item));
                    arr.Set(i, new InventorySlot { ItemIndex = itemIndex, Amount = canAdd });
                    remaining -= canAdd;
                }
            }

            return remaining;
        }


        /// <summary>Xóa vật phẩm khỏi túi đồ. Trả false nếu không đủ số lượng. Chỉ host.</summary>
        public bool TryRemoveItem(int itemIndex, int amount = 1)
        {
            if (!HasStateAuthority || _db == null || amount <= 0) return false;
            var item = _db.GetItem(itemIndex);
            if (item == null) return false;

            var arr = GetArrayForCategory(item.Category);
            if (arr.Length == 0) return false;

            // Đếm tổng có bao nhiêu
            int total = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                var s = arr.Get(i);
                if (s.ItemIndex == itemIndex) total += s.Amount;
            }
            if (total < amount) return false;

            int remaining = amount;
            for (int i = arr.Length - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = arr.Get(i);
                if (slot.ItemIndex != itemIndex) continue;
                int remove = Mathf.Min(remaining, slot.Amount);
                slot.Amount -= remove;
                remaining -= remove;
                if (slot.Amount <= 0) slot = InventorySlot.Empty;
                arr.Set(i, slot);
            }

            NotifyChanged();
            return true;
        }


        /// <summary>Hoán đổi 2 ô trong cùng category. Dùng cho drag-and-drop UI.</summary>
        public void SwapSlots(ItemCategory cat, int from, int to)
        {
            if (!HasStateAuthority) return;
            var arr = GetArrayForCategory(cat);
            if (arr.Length == 0 || from < 0 || to < 0 || from >= arr.Length || to >= arr.Length || from == to) return;

            var a = arr.Get(from);
            var b = arr.Get(to);
            arr.Set(from, b);
            arr.Set(to, a);
            NotifyChanged();
        }


        /// <summary>Trang bị Equipment từ ô inventory. Trả false nếu block (BR-17) hoặc sai loại.</summary>
        public bool TryEquipFromSlot(int slotIndex)
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (slotIndex < 0 || slotIndex >= EquipmentSlots.Length) return false;

            var slot = EquipmentSlots.Get(slotIndex);
            if (slot.IsEmpty || _db == null) return false;

            var item = _db.GetItem(slot.ItemIndex);
            if (item is EquipmentSO eq)
            {
                // Gỡ trang bị cũ (nếu có) → trả về inventory
                var currentEquipped = GetEquipSlotValue(eq.slot);
                if (!currentEquipped.IsEmpty)
                {
                    int emptyIdx = FindEmptySlot(EquipmentSlots);
                    if (emptyIdx < 0) return false; // không có chỗ trả
                    EquipmentSlots.Set(emptyIdx, currentEquipped);
                }

                SetEquipSlot(eq.slot, slot);
                EquipmentSlots.Set(slotIndex, InventorySlot.Empty);
                RebuildAndApplyGear();
                NotifyChanged();
                return true;
            }
            return false;
        }

        /// <summary>Trang bị Skill từ inventory. Tối đa 1 skill (BR-17).</summary>
        public bool TryEquipSkillFromSlot(int slotIndex)
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (slotIndex < 0 || slotIndex >= EquipmentSlots.Length) return false;

            var slot = EquipmentSlots.Get(slotIndex);
            if (slot.IsEmpty || _db == null) return false;
            if (_db.GetItem(slot.ItemIndex) is not SkillSO) return false;

            // Gỡ skill cũ
            if (!EquippedSkill.IsEmpty)
            {
                int emptyIdx = FindEmptySlot(EquipmentSlots);
                if (emptyIdx < 0) return false;
                EquipmentSlots.Set(emptyIdx, EquippedSkill);
            }

            EquippedSkill = slot;
            EquipmentSlots.Set(slotIndex, InventorySlot.Empty);
            NotifyChanged();
            return true;
        }

        /// <summary>Trang bị DamageEffect Accessory. Tối đa 1 (BR-17).</summary>
        public bool TryEquipAccessoryFromSlot(int slotIndex)
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (!CanSwapAccessory) return false;   // chỉ đổi accessory tại checkpoint
            if (slotIndex < 0 || slotIndex >= AccessorySlots.Length) return false;

            var slot = AccessorySlots.Get(slotIndex);
            if (slot.IsEmpty || _db == null) return false;

            var item = _db.GetItem(slot.ItemIndex);
            if (item is not AccessorySO acc || acc.kind != AccessoryKind.DamageEffect) return false;

            if (!EquippedAccessory.IsEmpty)
            {
                int emptyIdx = FindEmptySlot(AccessorySlots);
                if (emptyIdx < 0) return false;
                AccessorySlots.Set(emptyIdx, EquippedAccessory);
            }

            EquippedAccessory = slot;
            AccessorySlots.Set(slotIndex, InventorySlot.Empty);
            RebuildAndApplyGear();
            NotifyChanged();

            // acc_potion (AttackBuff) / acc_postskill (SkillBuff): buff chạy 60s tính TỪ LÚC TRANG BỊ.
            // Gọi SAU khi `EquippedAccessory` đã đổi vì AccessoryEffects đọc ô đang trang bị để biết loại buff.
            var fx = GetComponent<AccessoryEffects>();
            if (fx != null) fx.ArmBuffsOnEquip();

            return true;
        }

        /// <summary>Gỡ trang bị armor slot → trả về inventory. Trả false nếu không có chỗ.</summary>
        public bool TryUnequipArmor(EquipmentSlot armorSlot)
        {
            if (!HasStateAuthority || IsInBossZone) return false;

            var equipped = GetEquipSlotValue(armorSlot);
            if (equipped.IsEmpty) return false;

            int emptyIdx = FindEmptySlot(EquipmentSlots);
            if (emptyIdx < 0) return false;

            EquipmentSlots.Set(emptyIdx, equipped);
            SetEquipSlot(armorSlot, InventorySlot.Empty);
            RebuildAndApplyGear();
            NotifyChanged();
            return true;
        }

        public bool TryUnequipArmorToSlot(EquipmentSlot armorSlot, int destSlot)
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            var equipped = GetEquipSlotValue(armorSlot);
            if (equipped.IsEmpty) return false;

            if (destSlot >= 0 && destSlot < EquipmentSlots.Length)
            {
                var targetSlot = EquipmentSlots.Get(destSlot);
                if (targetSlot.IsEmpty)
                {
                    EquipmentSlots.Set(destSlot, equipped);
                    SetEquipSlot(armorSlot, InventorySlot.Empty);
                    RebuildAndApplyGear();
                    NotifyChanged();
                    return true;
                }
                else
                {
                    // Nếu ô đích có đồ, xem nó có cùng slot không thì equip luôn (tự swap)
                    var item = _db.GetItem(targetSlot.ItemIndex);
                    if (item is EquipmentSO eq && eq.slot == armorSlot)
                        return TryEquipFromSlot(destSlot);
                }
            }
            return TryUnequipArmor(armorSlot);
        }

        /// <summary>Gỡ skill đang trang bị → trả về inventory.</summary>
        public bool TryUnequipSkill()
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (EquippedSkill.IsEmpty) return false;

            int emptyIdx = FindEmptySlot(EquipmentSlots);
            if (emptyIdx < 0) return false;

            EquipmentSlots.Set(emptyIdx, EquippedSkill);
            EquippedSkill = InventorySlot.Empty;
            NotifyChanged();
            return true;
        }

        public bool TryUnequipSkillToSlot(int destSlot)
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (EquippedSkill.IsEmpty) return false;

            if (destSlot >= 0 && destSlot < EquipmentSlots.Length)
            {
                var targetSlot = EquipmentSlots.Get(destSlot);
                if (targetSlot.IsEmpty)
                {
                    EquipmentSlots.Set(destSlot, EquippedSkill);
                    EquippedSkill = InventorySlot.Empty;
                    NotifyChanged();
                    return true;
                }
                else
                {
                    var item = _db.GetItem(targetSlot.ItemIndex);
                    if (item is SkillSO)
                        return TryEquipSkillFromSlot(destSlot);
                }
            }
            return TryUnequipSkill();
        }

        /// <summary>Gỡ accessory đang trang bị → trả về inventory.</summary>
        public bool TryUnequipAccessory()
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (!CanSwapAccessory) return false;   // chỉ đổi accessory tại checkpoint
            if (EquippedAccessory.IsEmpty) return false;

            int emptyIdx = FindEmptySlot(AccessorySlots);
            if (emptyIdx < 0) return false;

            AccessorySlots.Set(emptyIdx, EquippedAccessory);
            EquippedAccessory = InventorySlot.Empty;
            RebuildAndApplyGear();
            NotifyChanged();
            return true;
        }

        public bool TryUnequipAccessoryToSlot(int destSlot)
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (!CanSwapAccessory) return false;   // chỉ đổi accessory tại checkpoint
            if (EquippedAccessory.IsEmpty) return false;

            if (destSlot >= 0 && destSlot < AccessorySlots.Length)
            {
                var targetSlot = AccessorySlots.Get(destSlot);
                if (targetSlot.IsEmpty)
                {
                    AccessorySlots.Set(destSlot, EquippedAccessory);
                    EquippedAccessory = InventorySlot.Empty;
                    RebuildAndApplyGear();
                    NotifyChanged();
                    return true;
                }
                else
                {
                    var item = _db.GetItem(targetSlot.ItemIndex);
                    if (item is AccessorySO)
                        return TryEquipAccessoryFromSlot(destSlot);
                }
            }
            return TryUnequipAccessory();
        }

        /// <summary>
        /// Vứt ra sàn được không? KHÔNG cho: key item (BR-45), skill và accessory — 3 loại này chỉ
        /// mặc/gỡ, mất là không lấy lại được. Nguồn duy nhất cho cả 4 đường drop + UI (ẩn nút DROP).
        /// </summary>
        public static bool CanDrop(ItemSO item) =>
            item != null
            && !Attrition.Persistence.ItemRuntimeConfig.IsKeyItem(item)
            && !(item is SkillSO) && !(item is AccessorySO);

        public bool TryDropEquippedArmor(EquipmentSlot armorSlot)
        {
            if (!HasStateAuthority || _db == null) return false;
            var equipped = GetEquipSlotValue(armorSlot);
            if (equipped.IsEmpty) return false;

            var item = _db.GetItem(equipped.ItemIndex);
            if (!CanDrop(item)) return false;

            SpawnDroppedItem(equipped.ItemIndex, equipped.Amount, Object.InputAuthority);
            SetEquipSlot(armorSlot, InventorySlot.Empty);
            RebuildAndApplyGear();
            NotifyChanged();
            return true;
        }

        public bool TryDropEquippedSkill()
        {
            if (!HasStateAuthority || _db == null) return false;
            if (EquippedSkill.IsEmpty) return false;

            var item = _db.GetItem(EquippedSkill.ItemIndex);
            if (!CanDrop(item)) return false;

            SpawnDroppedItem(EquippedSkill.ItemIndex, EquippedSkill.Amount, Object.InputAuthority);
            EquippedSkill = InventorySlot.Empty;
            NotifyChanged();
            return true;
        }

        public bool TryDropEquippedAccessory()
        {
            if (!HasStateAuthority || _db == null) return false;
            if (EquippedAccessory.IsEmpty) return false;

            var item = _db.GetItem(EquippedAccessory.ItemIndex);
            if (!CanDrop(item)) return false;

            SpawnDroppedItem(EquippedAccessory.ItemIndex, EquippedAccessory.Amount, Object.InputAuthority);
            EquippedAccessory = InventorySlot.Empty;
            RebuildAndApplyGear();
            NotifyChanged();
            return true;
        }

        [Header("---- PREFABS ----")]
        [SerializeField] private NetworkPrefabRef droppedItemPrefab;

        /// <summary>Vứt vật phẩm ra thế giới. Block Key Item (BR-45). Chỉ host.</summary>
        public bool TryDropItem(ItemCategory cat, int slotIndex)
        {
            if (!HasStateAuthority || _db == null) return false;
            var arr = GetArrayForCategory(cat);
            if (arr.Length == 0 || slotIndex < 0 || slotIndex >= arr.Length) return false;

            var slot = arr.Get(slotIndex);
            if (slot.IsEmpty) return false;

            var item = _db.GetItem(slot.ItemIndex);
            if (!CanDrop(item)) return false; // BR-45 + skill/accessory không vứt

            SpawnDroppedItem(slot.ItemIndex, slot.Amount, Object.InputAuthority);

            arr.Set(slotIndex, InventorySlot.Empty);
            NotifyChanged();
            return true;
        }

        /// <summary>Spawn 1 DroppedItem ra sàn gần player (dùng cho vứt thủ công + reward tràn túi). Chỉ host.</summary>
        private void SpawnDroppedItem(int itemIndex, int amount, PlayerRef dropper)
        {
            if (!HasStateAuthority || !droppedItemPrefab.IsValid || amount <= 0) return;

            // Lệch ngang nhỏ để raycast xuống vẫn trúng sàn dưới chân.
            float face = transform.localScale.x >= 0 ? 1f : -1f;
            Vector3 spawnPos = transform.position + new Vector3(0.25f * face, 0.2f, 0f);
            Runner.Spawn(droppedItemPrefab, spawnPos, Quaternion.identity, null, (runner, obj) =>
            {
                var dropped = obj.GetComponent<Attrition.Gameplay.World.DroppedItem>();
                if (dropped != null)
                {
                    dropped.ItemIndex = itemIndex;
                    dropped.Amount = amount;
                    dropped.InitDrop(dropper, runner.Tick);
                }
            });
        }



        /// <summary>Rebuild trang bị → cập nhật PlayerStats. Gọi sau mỗi equip/unequip.</summary>
        public void RebuildAndApplyGear()
        {
            if (_stats == null || _db == null) return;

            var equipped = new List<EquipmentSO>(4);
            TryAddEquipSO(EquippedHead, equipped);
            TryAddEquipSO(EquippedChest, equipped);
            TryAddEquipSO(EquippedLegs, equipped);
            TryAddEquipSO(EquippedBoots, equipped);

            var accessories = new List<AccessorySO>(1);
            if (!EquippedAccessory.IsEmpty)
            {
                var acc = _db.GetItem(EquippedAccessory.ItemIndex) as AccessorySO;
                if (acc != null) accessories.Add(acc);
            }

            _stats.ApplyLoadout(_stats.Level, equipped.ToArray(), accessories.ToArray());
        }

        private void TryAddEquipSO(InventorySlot slot, List<EquipmentSO> list)
        {
            if (slot.IsEmpty || _db == null) return;
            var eq = _db.GetItem(slot.ItemIndex) as EquipmentSO;
            if (eq != null) list.Add(eq);
        }


        /// <summary>
        /// Có SỞ HỮU 1 accessory dạng AbilityGrant cấp kỹ năng `ability` không?
        /// Theo concept (AccessorySO): chỉ cần sở hữu trong túi là tự áp dụng — KHÔNG cần trang bị.
        /// Quét cả 3 lưới túi + ô accessory đang đeo (phòng khi item nằm trong slot đeo).
        /// </summary>
        public bool HasAbility(GrantedAbility ability)
        {
            if (ability == GrantedAbility.None || _db == null) return false;

            if (GridHasAbility(EquipmentSlots, ability)) return true;
            if (GridHasAbility(AccessorySlots, ability)) return true;
            if (GridHasAbility(MaterialSlots, ability)) return true;
            if (SlotHasAbility(EquippedAccessory, ability)) return true;
            return false;
        }

        private bool GridHasAbility(NetworkArray<InventorySlot> grid, GrantedAbility ability)
        {
            for (int i = 0; i < grid.Length; i++)
                if (SlotHasAbility(grid.Get(i), ability)) return true;
            return false;
        }

        private bool SlotHasAbility(InventorySlot slot, GrantedAbility ability)
        {
            if (slot.IsEmpty) return false;
            var acc = _db.GetItem(slot.ItemIndex) as AccessorySO;
            return acc != null
                && acc.kind == AccessoryKind.AbilityGrant
                && acc.grantedAbility == ability;
        }


        /// <summary>Lưu inventory vào local JSON. Gọi sau mỗi thay đổi (BR-46).</summary>
        public void CommitLocalSave()
        {
            if (_db == null) return;
            string json = JsonUtility.ToJson(BuildSaveData(), true);
            System.IO.File.WriteAllText(GetSavePath(), json);
        }

        /// <summary>Xuất toàn bộ inventory + trang bị ra JSON (cho lưu server coop).</summary>
        public string ExportJson()
        {
            if (_db == null) return null;
            return JsonUtility.ToJson(BuildSaveData(), false);
        }

        /// <summary>Nạp inventory + trang bị từ JSON (server coop trả về). Chỉ host.</summary>
        public void ImportJson(string json)
        {
            if (!HasStateAuthority || _db == null || string.IsNullOrEmpty(json)) return;
            var data = JsonUtility.FromJson<InventorySaveData>(json);
            if (data != null) ApplySaveData(data);
        }

        private InventorySaveData BuildSaveData()
        {
            var data = new InventorySaveData();
            SerializeArray(EquipmentSlots, data.equipmentSlots);
            SerializeArray(AccessorySlots, data.accessorySlots);
            SerializeArray(MaterialSlots, data.materialSlots);
            data.equippedHead = SlotToSave(EquippedHead);
            data.equippedChest = SlotToSave(EquippedChest);
            data.equippedLegs = SlotToSave(EquippedLegs);
            data.equippedBoots = SlotToSave(EquippedBoots);
            data.equippedSkill = SlotToSave(EquippedSkill);
            data.equippedAccessory = SlotToSave(EquippedAccessory);
            return data;
        }

        private void ApplySaveData(InventorySaveData data)
        {
            DeserializeArray(data.equipmentSlots, EquipmentSlots);
            DeserializeArray(data.accessorySlots, AccessorySlots);
            DeserializeArray(data.materialSlots, MaterialSlots);
            EquippedHead = SaveToSlot(data.equippedHead);
            EquippedChest = SaveToSlot(data.equippedChest);
            EquippedLegs = SaveToSlot(data.equippedLegs);
            EquippedBoots = SaveToSlot(data.equippedBoots);
            EquippedSkill = SaveToSlot(data.equippedSkill);
            EquippedAccessory = SaveToSlot(data.equippedAccessory);
            RebuildAndApplyGear();
            NotifyChanged();
        }

        /// <summary>Load inventory từ local JSON. Gọi khi game start hoặc respawn.</summary>
        public void LoadFromLocal()
        {
            if (!HasStateAuthority || _db == null) return;

            string path = GetSavePath();
            if (!System.IO.File.Exists(path)) return;

            string json = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<InventorySaveData>(json);
            if (data != null) ApplySaveData(data);
        }

        private string GetSavePath()
        {
            // Solo: theo save slot đang chơi (mỗi nhân vật 1 file).
            // Coop: theo Owner + tên nhân vật (mỗi tài khoản/nhân vật riêng), tránh đè save solo.
            // Solo: dùng CHUNG helper với SaveManager. Trước đây mỗi bên tự ghép chuỗi đường dẫn,
            // nên SaveManager.DeleteSlot không biết file này tồn tại → xoá nhân vật xong tạo lại ở
            // cùng slot vẫn nạp nguyên túi đồ cũ.
            if (Attrition.Persistence.GameLaunch.Mode != Attrition.Persistence.LaunchMode.Coop)
                return Attrition.Persistence.SaveManager.SoloInventoryPath(Attrition.Persistence.GameLaunch.SelectedSlot);

            string owner = string.IsNullOrEmpty(Attrition.Persistence.GameLaunch.OwnerId) ? "guest" : Attrition.Persistence.GameLaunch.OwnerId;
            string chr = string.IsNullOrEmpty(Attrition.Persistence.GameLaunch.CharacterName) ? "char" : Attrition.Persistence.GameLaunch.CharacterName;
            return System.IO.Path.Combine(Application.persistentDataPath, $"inventory_coop_{owner}_{chr}.json");
        }


        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestEquip(int slotIndex) => TryEquipFromSlot(slotIndex);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestEquipSkill(int slotIndex) => TryEquipSkillFromSlot(slotIndex);

        /// <summary>SkillSO đang trang bị (ô EquippedSkill), hoặc null nếu trống. Dùng cho PlayerSkillCaster.</summary>
        public Attrition.Data.SkillSO GetEquippedSkillSO()
        {
            if (EquippedSkill.IsEmpty || _db == null) return null;
            return _db.GetItem(EquippedSkill.ItemIndex) as Attrition.Data.SkillSO;
        }

        /// <summary>AccessorySO đang trang bị (ô EquippedAccessory), hoặc null. Dùng cho AccessoryEffects đọc hiệu ứng.</summary>
        public Attrition.Data.AccessorySO GetEquippedAccessorySO()
        {
            if (EquippedAccessory.IsEmpty || _db == null) return null;
            return _db.GetItem(EquippedAccessory.ItemIndex) as Attrition.Data.AccessorySO;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestEquipAccessory(int slotIndex) => TryEquipAccessoryFromSlot(slotIndex);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipArmor(int armorSlotInt) => TryUnequipArmor((EquipmentSlot)armorSlotInt);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipArmorToSlot(int armorSlotInt, int destSlot) => TryUnequipArmorToSlot((EquipmentSlot)armorSlotInt, destSlot);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipSkill() => TryUnequipSkill();

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipSkillToSlot(int destSlot) => TryUnequipSkillToSlot(destSlot);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipAccessory() => TryUnequipAccessory();

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipAccessoryToSlot(int destSlot) => TryUnequipAccessoryToSlot(destSlot);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestSwap(int category, int from, int to) => SwapSlots((ItemCategory)category, from, to);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestDrop(int category, int slotIndex) => TryDropItem((ItemCategory)category, slotIndex);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestDropEquippedArmor(int equipmentSlot) => TryDropEquippedArmor((EquipmentSlot)equipmentSlot);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestDropEquippedSkill() => TryDropEquippedSkill();

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestDropEquippedAccessory() => TryDropEquippedAccessory();


        private NetworkArray<InventorySlot> GetArrayForCategory(ItemCategory cat)
        {
            switch (cat)
            {
                case ItemCategory.Equipment:
                case ItemCategory.Skill:
                    return EquipmentSlots;
                case ItemCategory.Accessory:
                    return AccessorySlots;
                case ItemCategory.Material:
                    return MaterialSlots;
                default:
                    return default;
            }
        }

        private InventorySlot GetEquipSlotValue(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return EquippedHead;
                case EquipmentSlot.Chest: return EquippedChest;
                case EquipmentSlot.Legs: return EquippedLegs;
                case EquipmentSlot.Boots: return EquippedBoots;
                default: return InventorySlot.Empty;
            }
        }

        private void SetEquipSlot(EquipmentSlot slot, InventorySlot value)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: EquippedHead = value; break;
                case EquipmentSlot.Chest: EquippedChest = value; break;
                case EquipmentSlot.Legs: EquippedLegs = value; break;
                case EquipmentSlot.Boots: EquippedBoots = value; break;
            }
        }




        private int FindEmptySlot(NetworkArray<InventorySlot> arr)
        {
            for (int i = 0; i < arr.Length; i++)
                if (arr.Get(i).IsEmpty) return i;
            return -1;
        }

        private void NotifyChanged()
        {
            CommitLocalSave();
            OnInventoryChanged?.Invoke();
        }


        // Vị trí ô được mã hoá bằng CHỈ SỐ trong list: ghi ra ĐỦ mọi ô kể cả ô trống để index i trong
        // JSON = ô i trong túi. DeserializeArray đọc lại theo đúng chỉ số đó.
        // ponytail: mã hoá theo vị trí rất dễ vỡ — đổi Capacity của túi, hay bất cứ ai lọc bỏ ô trống
        // trước khi lưu, là lệch toàn bộ lưới. Nâng cấp: thêm `slotIndex` vào SlotSaveData và đọc theo
        // field đó, fallback về vị trí trong list khi slotIndex khuyết (save cũ).
        private void SerializeArray(NetworkArray<InventorySlot> src, List<SlotSaveData> dest)
        {
            dest.Clear();
            for (int i = 0; i < src.Length; i++)
            {
                var s = src.Get(i);
                dest.Add(SlotToSave(s));
            }
        }

        private void DeserializeArray(List<SlotSaveData> src, NetworkArray<InventorySlot> dest)
        {
            for (int i = 0; i < dest.Length; i++)
            {
                dest.Set(i, i < src.Count ? SaveToSlot(src[i]) : InventorySlot.Empty);
            }
        }

        private SlotSaveData SlotToSave(InventorySlot slot)
        {
            if (slot.IsEmpty) return new SlotSaveData { itemId = "", amount = 0 };
            var item = _db.GetItem(slot.ItemIndex);
            return new SlotSaveData
            {
                itemId = item != null ? item.itemId : "",
                amount = slot.Amount
            };
        }

        private InventorySlot SaveToSlot(SlotSaveData save)
        {
            if (string.IsNullOrEmpty(save.itemId) || save.amount <= 0) return InventorySlot.Empty;
            int idx = _db.GetIndex(save.itemId);
            if (idx < 0) return InventorySlot.Empty;
            return new InventorySlot { ItemIndex = idx, Amount = save.amount };
        }
    }


    [System.Serializable]
    public class SlotSaveData
    {
        public string itemId = "";
        public int amount;
    }

    [System.Serializable]
    public class InventorySaveData
    {
        public List<SlotSaveData> equipmentSlots = new();
        public List<SlotSaveData> accessorySlots = new();
        public List<SlotSaveData> materialSlots = new();

        public SlotSaveData equippedHead = new();
        public SlotSaveData equippedChest = new();
        public SlotSaveData equippedLegs = new();
        public SlotSaveData equippedBoots = new();
        public SlotSaveData equippedSkill = new();
        public SlotSaveData equippedAccessory = new();
    }
}
