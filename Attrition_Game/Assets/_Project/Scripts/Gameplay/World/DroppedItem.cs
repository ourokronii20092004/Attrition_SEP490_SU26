using Fusion;
using UnityEngine;
using Attrition.Data;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Vật phẩm đã bị player vứt ra sàn (BR-43).
    /// Spawn bởi PlayerInventory.TryDropItem → Runner.Spawn().
    /// Tự raycast xuống tìm walkable surface (BR-43).
    /// Tự despawn khi player rời zone hoặc disconnect (BR-44).
    /// Player khác chạm vào có thể nhặt lại.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class DroppedItem : NetworkBehaviour
    {
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float maxRayDistance = 50f;

        [Networked] public int ItemIndex { get; set; }
        [Networked] public int Amount { get; set; }
        [Networked] private NetworkBool Consumed { get; set; }

        private SpriteRenderer _sr;

        public override void Spawned()
        {
            _sr = GetComponent<SpriteRenderer>();

            // Hiện icon item
            var db = ItemDatabaseSO.Instance;
            if (db != null)
            {
                var item = db.GetItem(ItemIndex);
                if (item != null && _sr != null)
                    _sr.sprite = item.icon;
            }

            // Raycast xuống tìm sàn (BR-43)
            if (HasStateAuthority)
            {
                var hit = Physics2D.Raycast(transform.position, Vector2.down, maxRayDistance, groundLayer);
                if (hit.collider != null)
                {
                    transform.position = hit.point + Vector2.up * 0.3f;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!HasStateAuthority || Consumed) return;

            var inv = other.GetComponentInParent<Attrition.Gameplay.Player.Inventory.PlayerInventory>();
            if (inv == null) return;

            if (inv.TryAddItem(ItemIndex, Amount))
            {
                Consumed = true;
                Runner.Despawn(Object);
            }
        }

        /// <summary>
        /// Gọi bởi hệ thống khi player rời zone hoặc quit (BR-44).
        /// Có thể gọi từ scene change callback hoặc NetworkRunner shutdown.
        /// </summary>
        public void ForceCleanup()
        {
            if (HasStateAuthority && !Consumed)
            {
                Consumed = true;
                Runner.Despawn(Object);
            }
        }
    }
}
