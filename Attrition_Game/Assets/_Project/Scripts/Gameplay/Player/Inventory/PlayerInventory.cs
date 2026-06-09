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

        public override void Spawned()
        {
            _stats = GetComponent<PlayerStats>();
            _db = ItemDatabaseSO.Instance;

            if (_db == null)
                Debug.LogError("[PlayerInventory] ItemDatabaseSO.Instance chưa được set!");
        }

        // ═══════════════════════════════════════════
        //  ADD ITEM (BR-40, BR-41, BR-42)
        // ═══════════════════════════════════════════

        /// <summary>Thêm vật phẩm vào túi đồ. Trả false nếu đầy (BR-40). Chỉ host.</summary>
        public bool TryAddItem(int itemIndex, int amount = 1)
        {
            if (!HasStateAuthority || _db == null || amount <= 0) return false;
            var item = _db.GetItem(itemIndex);
            if (item == null) return false;

            var arr = GetArrayForCategory(item.Category);
            if (arr.Length == 0) return false;

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

            if (remaining >= amount) return false; // không thêm được gì cả

            NotifyChanged();
            return true;
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

            // Spawn DroppedItem ở vị trí player (DroppedItem tự raycast xuống sàn — BR-43)
            if (droppedItemPrefab.IsValid)
            {
                Runner.Spawn(droppedItemPrefab, transform.position, Quaternion.identity, null, (runner, obj) =>
                {
                    var dropped = obj.GetComponent<Attrition.Gameplay.World.DroppedItem>();
                    if (dropped != null)
                    {
                        dropped.ItemIndex = slot.ItemIndex;
                        dropped.Amount = slot.Amount;
                    }
                });
            }

            arr.Set(slotIndex, InventorySlot.Empty);
            NotifyChanged();
            return true;
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

            string json = JsonUtility.ToJson(data, true);
            string path = GetSavePath();
            System.IO.File.WriteAllText(path, json);
        }

        /// <summary>Load inventory từ local JSON. Gọi khi game start hoặc respawn.</summary>
        public void LoadFromLocal()
        {
            if (!HasStateAuthority || _db == null) return;

            string path = GetSavePath();
            if (!System.IO.File.Exists(path)) return;

            string json = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<InventorySaveData>(json);
            if (data == null) return;

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

        private string GetSavePath()
        {
            // Mỗi player có file riêng theo Object.InputAuthority
            string playerId = Object != null ? Object.InputAuthority.PlayerId.ToString() : "local";
            return System.IO.Path.Combine(Application.persistentDataPath, $"inventory_{playerId}.json");
        }

        // ═══════════════════════════════════════════
        //  RPC — Client gửi yêu cầu lên Host
        // ═══════════════════════════════════════════

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestEquip(int slotIndex) => TryEquipFromSlot(slotIndex);

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestEquipSkill(int slotIndex) => TryEquipSkillFromSlot(slotIndex);

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
