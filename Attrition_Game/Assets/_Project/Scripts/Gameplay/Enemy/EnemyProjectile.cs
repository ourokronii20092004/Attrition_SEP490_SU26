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

    [Tooltip("TÙY CHỌN: prefab hiệu ứng nổ khi đạn trúng đích hoặc hết tầm (vd WaterBall - Impact của " +
             "ArchDemon). Bỏ trống = không có hiệu ứng. Prefab nên có EnemyAoEDamage với damage 0 nếu chỉ " +
             "muốn làm hình, hoặc >0 nếu muốn nổ gây thêm sát thương diện rộng.")]
    public Fusion.NetworkPrefabRef impactPrefab;
    [Tooltip("Sát thương của vụ nổ impactPrefab (0 = chỉ là hiệu ứng hình).")]
    public int impactDamage = 0;

    [Networked] private TickTimer lifeTimer { get; set; }
    [Networked] private Vector2 moveDirection { get; set; }
    [Networked] private int damage { get; set; }
    [Networked] private float knockbackForce { get; set; }
    [Networked] private int damageTypeRaw { get; set; }

    public void Init(Vector2 direction, int dmg, float knockback, Attrition.Core.DamageType type = Attrition.Core.DamageType.Physical)
    {
        moveDirection = direction.normalized;
        damage = dmg;
        knockbackForce = knockback;
        damageTypeRaw = (int)type;
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
        if (Attrition.Persistence.GamePause.IsPaused) return; // SOLO pause

        // SỬA: Chỉ Host mới được chạy FixedUpdateNetwork để tính va chạm và vị trí gốc
        if (!HasStateAuthority) return;

        if (lifeTimer.Expired(Runner))
        {
            SpawnImpact();   // hết tầm (vd tới rìa map) cũng nổ — yêu cầu của ArchDemon skill 2
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
                dmg.TakeDamage(damage, pushDir, knockbackForce, (Attrition.Core.DamageType)damageTypeRaw);
            }

            SpawnImpact();
            Runner.Despawn(Object);
            return;
        }

        transform.Translate(movement, Space.World);
    }

    /// <summary>
    /// Spawn hiệu ứng nổ tại chỗ đạn đang đứng (nếu prefab có gán impactPrefab). Chỉ host — hàm này
    /// chỉ được gọi từ FixedUpdateNetwork đã gate HasStateAuthority, nhưng vẫn guard cho chắc vì Despawn
    /// là thao tác host-only.
    /// </summary>
    private void SpawnImpact()
    {
        if (!HasStateAuthority || !impactPrefab.IsValid) return;

        Runner.Spawn(impactPrefab, transform.position, Quaternion.identity, null, (runner, obj) =>
        {
            Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                obj, Vector2.zero, impactDamage,
                Attrition.Gameplay.Combat.ProjectileInitializer.DefaultSpeed,
                (Attrition.Core.DamageType)damageTypeRaw);
        });
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitboxRadius);
    }
}
