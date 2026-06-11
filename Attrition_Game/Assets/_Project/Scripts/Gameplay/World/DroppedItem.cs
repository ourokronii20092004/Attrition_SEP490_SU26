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
        [Tooltip("Độ cao item so với mặt đất khi đáp (units). Càng nhỏ càng sát đất.")]
        [SerializeField] private float groundOffset = 0.12f;
        [Tooltip("Giây mà NGƯỜI VỪA VỨT chưa nhặt lại được. Người khác nhặt liền.")]
        [SerializeField] private float selfPickupCooldown = 3f;

        [Networked] public int ItemIndex { get; set; }
        [Networked] public int Amount { get; set; }
        [Networked] private NetworkBool Consumed { get; set; }
        [Networked] private PlayerRef DropperRef { get; set; }
        [Networked] private int DropTick { get; set; }

        private SpriteRenderer _sr;

        /// <summary>Gán người vứt + tick lúc vứt (host gọi trong OnBeforeSpawned).</summary>
        public void InitDrop(PlayerRef dropper, int dropTick)
        {
            DropperRef = dropper;
            DropTick = dropTick;
        }

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

            // Raycast xuống tìm sàn (BR-43). PHẢI dùng physics scene của Fusion (Runner),
            // KHÔNG dùng Physics2D static (query nhầm scene mặc định → luôn trượt).
            if (HasStateAuthority)
            {
                Vector2 origin = (Vector2)transform.position + Vector2.up * 1.5f;
                var hit = Runner.GetPhysicsScene2D().Raycast(origin, Vector2.down, maxRayDistance, groundLayer);
                if (hit.collider != null)
                {
                    Vector3 landed = new Vector3(transform.position.x, hit.point.y + groundOffset, transform.position.z);
                    // NetworkTransform ghi đè transform.position ở Render → phải Teleport để state networked đổi theo.
                    var nt = GetComponent<NetworkTransform>();
                    if (nt != null) nt.Teleport(landed);
                    else transform.position = landed;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
        private void OnTriggerStay2D(Collider2D other) => TryPickup(other);

        private void TryPickup(Collider2D other)
        {
            if (!HasStateAuthority || Consumed) return;

            var inv = other.GetComponentInParent<Attrition.Gameplay.Player.Inventory.PlayerInventory>();
            if (inv == null) return;

            // Người VỪA VỨT phải chờ hết cooldown; người khác nhặt ngay.
            bool isDropper = inv.Object != null && inv.Object.InputAuthority == DropperRef;
            if (isDropper && (Runner.Tick - DropTick) * Runner.DeltaTime < selfPickupCooldown) return;

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
