using Fusion;
using UnityEngine;

public class EnemyProjectile : NetworkBehaviour
{
    public float speed = 10f;
    public float lifetime = 3f;
    public LayerMask hitLayer; // Nên chọn layer Player và Obstacle/Ground
    public float hitboxRadius = 0.2f;
    [Tooltip("Điều chỉnh góc xoay nếu ảnh đạn bị lệch (ví dụ: ảnh gốc chĩa lên trên thì nhập -90)")]
    public float rotationOffset = 0f;

    [Networked] private TickTimer lifeTimer { get; set; }
    [Networked] private Vector2 moveDirection { get; set; }
    [Networked] private int damage { get; set; }
    [Networked] private float knockbackForce { get; set; }

    public void Init(Vector2 direction, int dmg, float knockback)
    {
        moveDirection = direction.normalized;
        damage = dmg;
        knockbackForce = knockback;
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            lifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
        }
    }

    public override void Render()
    {
        if (moveDirection != Vector2.zero)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // SỬA: Chỉ Host mới được chạy FixedUpdateNetwork để tính va chạm và vị trí gốc
        if (!HasStateAuthority) return;

        if (lifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        Vector2 movement = moveDirection * speed * Runner.DeltaTime;
        
        RaycastHit2D hit = Runner.GetPhysicsScene2D().CircleCast(transform.position, hitboxRadius, moveDirection, movement.magnitude, hitLayer);
        
        if (hit.collider != null)
        {
            IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
            {
                Vector2 pushDir = new Vector2(moveDirection.x, 0.5f).normalized;
                dmg.TakeDamage(damage, pushDir, knockbackForce);
            }
            
            Runner.Despawn(Object);
            return;
        }

        transform.Translate(movement, Space.World);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitboxRadius);
    }
}
