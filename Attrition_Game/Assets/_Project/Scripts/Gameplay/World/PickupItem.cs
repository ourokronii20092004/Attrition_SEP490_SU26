using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Player;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.Gameplay.World
{
    public enum PickupKind
    {
        RestoreHP,        // Hồi HP ngay
        RestoreMana,      // Hồi Mana ngay
        MaxHealthCharge,  // Tăng cap bình máu (+amount)
        MaxManaCharge,    // Tăng cap bình mana (+amount)
        InventoryItem     // Nhặt vật phẩm vào inventory
    }

    /// <summary>
    /// Vật phẩm nhặt trong thế giới (prefab mẫu — thay Sprite vào là xài).
    /// Gắn lên GameObject có Collider2D (Is Trigger = ON). Player chạm vào → nhận hiệu ứng.
    ///
    /// Coop: theo concept, vật phẩm rơi ngẫu nhiên là RIÊNG mỗi player → pickup này
    /// chỉ tác dụng lên ĐÚNG player chạm vào (không chia sẻ). Host xử lý rồi despawn.
    ///
    /// Loại hiệu ứng chọn trong Inspector (PickupKind). Lượng = amount.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PickupItem : NetworkBehaviour
    {
        [Header("---- PICKUP ----")]
        [Tooltip("Loại hiệu ứng khi nhặt.")]
        [SerializeField] private PickupKind kind = PickupKind.RestoreHP;
        [Tooltip("Lượng hồi (HP/Mana) hoặc số charge cộng thêm, hoặc số lượng item nhặt.")]
        [SerializeField] private int amount = 50;

        [Header("---- INVENTORY ITEM (kind = InventoryItem) ----")]
        [Tooltip("SO vật phẩm sẽ được thêm vào inventory khi nhặt. Chỉ dùng khi kind = InventoryItem.")]
        [SerializeField] private ItemSO itemData;

        [Header("---- FEEDBACK (tùy chọn) ----")]
        [Tooltip("Prefab hiệu ứng spawn khi nhặt (vd particle). Bỏ trống = không có.")]
        [SerializeField] private GameObject collectVfxPrefab;

        [Networked] private NetworkBool Consumed { get; set; }

        /// <summary>
        /// Vị trí lúc SPAWN — dùng làm khoá ghi nhớ "đã nhặt". Chốt ở Spawned thay vì đọc
        /// transform.position lúc nhặt: pickup có FloatBobEffect (nhấp nhô lên xuống) nên toạ độ y
        /// lúc chạm khác lúc spawn, làm khoá lệch và lần sau vào map lại nhặt được nữa.
        /// </summary>
        private Vector3 _spawnPos;

        public override void Spawned()
        {
            _spawnPos = transform.position;

            // ĐÃ NHẶT từ lần chơi/lần load trước → despawn ngay, đừng cho nhặt lại (farm max HP charge).
            // Chỉ host quyết (Despawn là host-authoritative); client thấy object biến mất qua sync.
            if (!HasStateAuthority) return;

            // Thứ tự Spawned không đảm bảo → nạp lazy tại đây (giống BreakableObject).
            Attrition.Gameplay.Environment.PickupState.EnsureLoadedForSolo();
            if (Attrition.Gameplay.Environment.PickupState.IsCollected(SceneKey, _spawnPos))
            {
                Consumed = true;
                Runner.Despawn(Object);
            }
        }

        /// <summary>
        /// Tên map dùng làm khoá. KHÔNG dùng gameObject.scene.name: coop load additive nên nó có thể
        /// trả scene runner riêng của Fusion, không phải tên map. GameLaunch.GameplayScene là nguồn
        /// duy nhất đáng tin và cũng là giá trị phần save đang dùng.
        /// </summary>
        private static string SceneKey => Attrition.Persistence.GameLaunch.GameplayScene;

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Chỉ host quyết định ai nhặt được (tránh double-pickup giữa host/client).
            if (!HasStateAuthority || Consumed) return;

            var stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null) return;

            if (!ApplyTo(stats)) return;

            Consumed = true;

            // GHI NHỚ BỀN: chỉ cho pickup ĐẶT SẴN trong scene (bình máu ẩn...). Đồ quái rơi ra dùng
            // DroppedItem nên không đi qua đây — ghi nhớ theo vị trí sẽ làm món rơi trúng chỗ cũ biến mất.
            if (Attrition.Gameplay.Environment.PickupState.MarkCollected(SceneKey, _spawnPos))
            {
                // Lưu NGAY: chờ tới mốc rest mà người chơi thoát game thì bình lại hiện ra để nhặt lần nữa.
                Attrition.Gameplay.Persistence.GameSaveService.EnsureExists().SaveWorldState();
            }

            RpcOnCollected(transform.position);
            Runner.Despawn(Object);
        }

        /// <summary>Áp hiệu ứng. Trả false nếu không áp được (vd inventory đầy).</summary>
        private bool ApplyTo(PlayerStats stats)
        {
            switch (kind)
            {
                case PickupKind.RestoreHP:
                    stats.RestoreHP(amount);
                    return true;
                case PickupKind.RestoreMana:
                    stats.RestoreMana(amount);
                    return true;
                case PickupKind.MaxHealthCharge:
                {
                    var potions = stats.GetComponent<PotionSystem>();
                    if (potions != null) potions.IncreaseMaxHealthCharges(amount);
                    return true;
                }
                case PickupKind.MaxManaCharge:
                {
                    var potions = stats.GetComponent<PotionSystem>();
                    if (potions != null) potions.IncreaseMaxManaCharges(amount);
                    return true;
                }
                case PickupKind.InventoryItem:
                {
                    if (itemData == null) return false;
                    var db = ItemDatabaseSO.Instance;
                    if (db == null) return false;
                    int idx = db.GetIndex(itemData);
                    if (idx < 0) return false;

                    // Accessory (kể cả AbilityGrant double jump / shadow dash) DÙNG CHUNG: 1 người nhặt →
                    // thêm vào túi CẢ HAI player. Item thường chỉ vào người chạm vào.
                    if (itemData is AccessorySO)
                    {
                        bool anyAdded = false;
                        foreach (var otherInv in FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None))
                            if (otherInv != null && otherInv.TryAddItem(idx, amount)) anyAdded = true;
                        return anyAdded;
                    }

                    var inv = stats.GetComponent<PlayerInventory>();
                    if (inv == null) return false;
                    return inv.TryAddItem(idx, amount); // BR-40: trả false nếu đầy
                }
                default:
                    return false;
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcOnCollected(Vector3 pos)
        {
            if (collectVfxPrefab != null) Instantiate(collectVfxPrefab, pos, Quaternion.identity);
        }
    }
}
