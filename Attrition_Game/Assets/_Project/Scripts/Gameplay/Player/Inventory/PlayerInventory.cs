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
        // ═══════════════════════════════════════════
        //  NETWORKED INVENTORY GRID
        // ═══════════════════════════════════════════
        [Networked, Capacity(40)] public NetworkArray<InventorySlot> EquipmentSlots { get; }
        [Networked, Capacity(10)] public NetworkArray<InventorySlot> AccessorySlots { get; }
        [Networked, Capacity(14)] public NetworkArray<InventorySlot> MaterialSlots { get; }

        // Định danh chủ nhân của nhân vật này (host ghi/đọc để LOAD + LƯU đồ đúng người trong coop).
        // Host's own player: host ghi thẳng. Client's player: client gửi qua RpcSetOwnerIdentity.
        [Networked] public NetworkString<_64> OwnerUserId { get; set; }
        [Networked] public NetworkString<_64> OwnerCharacterId { get; set; }

        // ═══════════════════════════════════════════
        //  NETWORKED EQUIP SLOTS (đang mặc)
        // ═══════════════════════════════════════════
        [Networked] public InventorySlot EquippedHead { get; set; }
        [Networked] public InventorySlot EquippedChest { get; set; }
        [Networked] public InventorySlot EquippedLegs { get; set; }
        [Networked] public InventorySlot EquippedBoots { get; set; }
        [Networked] public InventorySlot EquippedSkill { get; set; }
        [Networked] public InventorySlot EquippedAccessory { get; set; }

        // ═══════════════════════════════════════════
        //  REFERENCES
        // ═══════════════════════════════════════════
        private PlayerStats _stats;
        private ItemDatabaseSO _db;

        /// <summary>Flag set bởi PlayerController khi đang trong vùng Boss (BR-17).</summary>
        [System.NonSerialized] public bool IsInBossZone;

        /// <summary>Event UI lắng nghe để refresh grid.</summary>
        public event System.Action OnInventoryChanged;

        [Header("---- STARTING ITEMS (host seed) ----")]
        [Tooltip("String id của item phát cho nhân vật mới (vd: iron_helm, skill_fire). Chỉ host seed, chỉ khi túi đang trống. Để TEST: mặc định phát sẵn vài món.")]
        [SerializeField] private string[] startingItemIds =
        {
            "leather_helm", "iron_chest", "gold_boots",
            "acc_double_jump", "acc_stamina_charm",
            "skill_fire", "skill_thunder"
        };

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

            if (!string.IsNullOrEmpty(charId)
                && Attrition.Persistence.GameLaunch.SessionInventoryByChar.TryGetValue(charId, out var invJson)
                && !string.IsNullOrEmpty(invJson))
            {
                ImportJson(invJson);
            }

            SeedStartingItems(); // chỉ seed nếu túi vẫn trống sau khi nạp từ session
        }

        /// <summary>
        /// Host fetch session detail (GET /internal/sessions/{id}) MỘT LẦN, đổ inventoryJson từng
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
                    if (detail == null) return;

                    if (detail.characters != null)
                    {
                        foreach (var cs in detail.characters)
                        {
                            if (cs == null || string.IsNullOrEmpty(cs.characterId)) continue;
                            Attrition.Persistence.GameLaunch.SessionInventoryByChar[cs.characterId] = cs.inventoryJson;
                        }
                    }

                    // Quest world-state của session (host-authoritative). Khôi phục NPC theo tiến trình session.
                    string questsJson = BuildQuestsJson(detail.worldStates);
                    Attrition.Persistence.GameLaunch.CoopQuestsJson = questsJson;
                    Attrition.Gameplay.NPC.NetworkNPC.ApplyAllJson(questsJson);
                });
            }

            Attrition.Persistence.GameLaunch.SessionInventoryLoaded = true; // mở khoá cho player đang chờ
        }

        /// <summary>Map worldStates (per session: eventId/stateValue/progress) → QuestProgressList JSON
        /// mà NetworkNPC.ApplyAllJson hiểu (questId/state/progress). eventId chính là questId.</summary>
        private static string BuildQuestsJson(System.Collections.Generic.List<APIManager.WorldStateDto> states)
        {
            if (states == null || states.Count == 0) return "";
            var list = new Attrition.Persistence.QuestProgressList();
            var entries = new System.Collections.Generic.List<Attrition.Persistence.QuestProgressEntry>();
            foreach (var ws in states)
            {
                if (ws == null || string.IsNullOrEmpty(ws.eventId)) continue;
                entries.Add(new Attrition.Persistence.QuestProgressEntry
                {
                    questId = ws.eventId,
                    state = (byte)ws.stateValue,
                    progress = ws.progress
                });
            }
            if (entries.Count == 0) return "";
            list.quests = entries.ToArray();
            return UnityEngine.JsonUtility.ToJson(list);
        }

        /// <summary>Phát item khởi đầu cho nhân vật mới (host-only, chỉ khi túi trống).</summary>
        private void SeedStartingItems()
        {
            Debug.Log($"[Seed] start. db.Count={_db.Count}, ids={(startingItemIds==null?0:startingItemIds.Length)}, eq0empty={EquipmentSlots.Get(0).IsEmpty}, acc0empty={AccessorySlots.Get(0).IsEmpty}");
            if (startingItemIds == null || startingItemIds.Length == 0) return;
            // Chỉ seed khi cả 3 nhóm đang trống (tránh ghi đè save đã load).
            if (!EquipmentSlots.Get(0).IsEmpty || !AccessorySlots.Get(0).IsEmpty) return;

            foreach (var id in startingItemIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                int idx = _db.GetIndex(id);
                if (idx >= 0)
                {
                    bool ok = TryAddItem(idx, 1);
                    Debug.Log($"[Seed] '{id}' idx={idx} added={ok}");
                }
                else Debug.LogWarning($"[Seed] '{id}' KHÔNG có trong ItemDatabase.");
            }
        }

        // ═══════════════════════════════════════════
        //  ADD ITEM (BR-40, BR-41, BR-42)
        // ═══════════════════════════════════════════

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
            if (item.maxStack > 1)
            {
                for (int i = 0; i < arr.Length && remaining > 0; i++)
                {
                    var slot = arr.Get(i);
                    if (slot.ItemIndex == itemIndex && slot.Amount < item.maxStack)
                    {
                        int canAdd = Mathf.Min(remaining, item.maxStack - slot.Amount);
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
                    int canAdd = Mathf.Min(remaining, item.maxStack);
                    arr.Set(i, new InventorySlot { ItemIndex = itemIndex, Amount = canAdd });
                    remaining -= canAdd;
                }
            }

            return remaining;
        }

        // ═══════════════════════════════════════════
        //  REMOVE ITEM
        // ═══════════════════════════════════════════

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

        // ═══════════════════════════════════════════
        //  SWAP SLOTS
        // ═══════════════════════════════════════════

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

        // ═══════════════════════════════════════════
        //  EQUIP / UNEQUIP (BR-17)
        // ═══════════════════════════════════════════

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

        /// <summary>Gỡ accessory đang trang bị → trả về inventory.</summary>
        public bool TryUnequipAccessory()
        {
            if (!HasStateAuthority || IsInBossZone) return false;
            if (EquippedAccessory.IsEmpty) return false;

            int emptyIdx = FindEmptySlot(AccessorySlots);
            if (emptyIdx < 0) return false;

            AccessorySlots.Set(emptyIdx, EquippedAccessory);
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
            if (item == null) return false;
            if (item.isKeyItem) return false; // BR-45

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


        // ═══════════════════════════════════════════
        //  GEAR → STATS INTEGRATION
        // ═══════════════════════════════════════════

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

        // ═══════════════════════════════════════════
        //  LOCAL SAVE / LOAD (BR-46)
        // ═══════════════════════════════════════════

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
            var gl = Attrition.Persistence.GameLaunch.Mode;
            string key;
            if (gl == Attrition.Persistence.LaunchMode.Coop)
            {
                string owner = string.IsNullOrEmpty(Attrition.Persistence.GameLaunch.OwnerId) ? "guest" : Attrition.Persistence.GameLaunch.OwnerId;
                string chr = string.IsNullOrEmpty(Attrition.Persistence.GameLaunch.CharacterName) ? "char" : Attrition.Persistence.GameLaunch.CharacterName;
                key = $"coop_{owner}_{chr}";
            }
            else
            {
                key = $"solo_{Attrition.Persistence.GameLaunch.SelectedSlot}";
            }
            return System.IO.Path.Combine(Application.persistentDataPath, $"inventory_{key}.json");
        }

        // ═══════════════════════════════════════════
        //  RPC — Client gửi yêu cầu lên Host
        // ═══════════════════════════════════════════

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

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestEquipAccessory(int slotIndex) => TryEquipAccessoryFromSlot(slotIndex);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipArmor(int armorSlotInt) => TryUnequipArmor((EquipmentSlot)armorSlotInt);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipSkill() => TryUnequipSkill();

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestUnequipAccessory() => TryUnequipAccessory();

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestSwap(int category, int from, int to) => SwapSlots((ItemCategory)category, from, to);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestDrop(int category, int slotIndex) => TryDropItem((ItemCategory)category, slotIndex);

        // ═══════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════

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

        // ── Save/Load serialization helpers ──

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

    // ═══════════════════════════════════════════
    //  JSON SAVE STRUCTURES
    // ═══════════════════════════════════════════

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
