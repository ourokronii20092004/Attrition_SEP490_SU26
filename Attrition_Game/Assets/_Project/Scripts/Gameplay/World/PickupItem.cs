using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.World
{
    public enum PickupKind
    {
        RestoreHP,        // Hồi HP ngay
        RestoreMana,      // Hồi Mana ngay
        MaxHealthCharge,  // Tăng cap bình máu (+amount)
        MaxManaCharge     // Tăng cap bình mana (+amount)
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
        [Tooltip("Lượng hồi (HP/Mana) hoặc số charge cộng thêm.")]
        [SerializeField] private int amount = 50;

        [Header("---- FEEDBACK (tùy chọn) ----")]
        [Tooltip("Prefab hiệu ứng spawn khi nhặt (vd particle). Bỏ trống = không có.")]
        [SerializeField] private GameObject collectVfxPrefab;

        [Networked] private NetworkBool Consumed { get; set; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Chỉ host quyết định ai nhặt được (tránh double-pickup giữa host/client).
            if (!HasStateAuthority || Consumed) return;

            var stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null) return;

            ApplyTo(stats);
            Consumed = true;
            RpcOnCollected(transform.position);

            Runner.Despawn(Object);
        }

        private void ApplyTo(PlayerStats stats)
        {
            switch (kind)
            {
                case PickupKind.RestoreHP:
                    stats.RestoreHP(amount);
                    break;
                case PickupKind.RestoreMana:
                    stats.RestoreMana(amount);
                    break;
                case PickupKind.MaxHealthCharge:
                {
                    var potions = stats.GetComponent<PotionSystem>();
                    if (potions != null) potions.IncreaseMaxHealthCharges(amount);
                    break;
                }
                case PickupKind.MaxManaCharge:
                {
                    var potions = stats.GetComponent<PotionSystem>();
                    if (potions != null) potions.IncreaseMaxManaCharges(amount);
                    break;
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcOnCollected(Vector3 pos)
        {
            if (collectVfxPrefab != null) Instantiate(collectVfxPrefab, pos, Quaternion.identity);
        }
    }
}
