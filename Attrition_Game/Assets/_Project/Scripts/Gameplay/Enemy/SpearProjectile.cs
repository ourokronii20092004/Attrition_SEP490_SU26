using Fusion;
using UnityEngine;

public class SpearProjectile : NetworkBehaviour
{
    [Header("Spear Settings")]
    public float speed = 15f;
    public float gravity = -30f; // Trọng lực tùy chỉnh, thường cao hơn thực tế để rơi nhanh hơn (chuẩn game 2D)
    public float lifetimeAfterStuck = 3f;
    public float maxLifetime = 5f; // Đề phòng lỗi bay mãi không trúng gì
    
    [Header("Collision & Hitbox")]
    [Tooltip("Layer chứa cả Player và Môi trường (Ground/Wall)")]
    public LayerMask hitLayer; 
    [Tooltip("Layer dành riêng cho Môi trường để biết khi nào thì cắm vào đất")]
    public LayerMask groundLayer; 
    
    [Tooltip("Kích thước hitbox dạng hộp chữ nhật của cây lao")]
    public Vector2 hitboxSize = new Vector2(1.5f, 0.2f); 
    [Tooltip("Góc xoay bù trừ nếu Sprite bị lệch")]
    public float rotationOffset = 0f;

    [Networked] private TickTimer lifeTimer { get; set; }
    [Networked] private TickTimer stuckTimer { get; set; }
    [Networked] private Vector2 currentVelocity { get; set; }
    [Networked] private NetworkBool isStuck { get; set; }
    
    [Networked] private int damage { get; set; }
    [Networked] private float knockbackForce { get; set; }
    [Networked] private int damageTypeRaw { get; set; }

    public void Init(Vector2 initialDirection, int dmg, float knockback, Attrition.Core.DamageType type = Attrition.Core.DamageType.Physical)
    {
        // Vận tốc ban đầu (ví dụ: quái ném góc xéo lên)
        currentVelocity = initialDirection.normalized * speed;
        damage = dmg;
        knockbackForce = knockback;
        isStuck = false;
        damageTypeRaw = (int)type;
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            lifeTimer = TickTimer.CreateFromSeconds(Runner, maxLifetime);
        }
    }

    public override void Render()
    {
        // Cập nhật góc xoay của hình ảnh theo hướng bay (nếu chưa cắm vào tường)
        if (!isStuck && currentVelocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Nếu lao đã cắm vào tường, đếm ngược để tự hủy
        if (isStuck)
        {
            if (stuckTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
            return; // Dừng tại đây, không di chuyển hay gây sát thương nữa
        }

        // Hết thời gian bay tối đa (rớt ra ngoài map)
        if (lifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        // 1. TÍNH TOÁN VẬT LÝ VÒNG CUNG (Trọng lực)
        Vector2 velocity = currentVelocity;
        velocity.y += gravity * Runner.DeltaTime;
        currentVelocity = velocity;

        Vector2 movement = currentVelocity * Runner.DeltaTime;
        float distance = movement.magnitude;
        Vector2 direction = movement.normalized;

        float angle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;
        
        // 2. XỬ LÝ VA CHẠM (BoxCast - Phát hiện va chạm cực chuẩn không lo bị xuyên tường khi bay nhanh)
        RaycastHit2D hit = Runner.GetPhysicsScene2D().BoxCast(
            transform.position, 
            hitboxSize, 
            angle + rotationOffset, 
            direction, 
            distance, 
            hitLayer
        );

        if (hit.collider != null)
        {
            // Kiểm tra xem chạm vào Ground/Wall hay Player
            if (((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
            {
                // CẮM VÀO TƯỜNG / ĐẤT
                StickIntoGround(hit.point, direction, angle);
            }
            else
            {
                // TRÚNG PLAYER HOẶC VẬT THỂ NHẬN SÁT THƯƠNG KHÁC
                IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead)
                {
                    Vector2 pushDir = new Vector2(Mathf.Sign(currentVelocity.x), 0.5f).normalized;
                    dmg.TakeDamage(damage, pushDir, knockbackForce, (Attrition.Core.DamageType)damageTypeRaw);
                }
                
                // Trúng người thì biến mất luôn (hoặc có thể xuyên qua tùy game design)
                Runner.Despawn(Object);
            }
            return;
        }

        // 3. CẬP NHẬT VỊ TRÍ
        transform.position += (Vector3)movement;
    }

    private void StickIntoGround(Vector2 hitPoint, Vector2 impactDirection, float impactAngle)
    {
        isStuck = true;
        currentVelocity = Vector2.zero; // Dừng lại
        
        // Cập nhật rotation lần cuối cho khớp
        transform.rotation = Quaternion.Euler(0, 0, impactAngle + rotationOffset);
        
        // Dịch chuyển mũi lao tới điểm chạm cộng thêm 1 chút xíu để tạo hiệu ứng cắm sâu vào đất
        transform.position = hitPoint + (impactDirection * 0.15f);
        
        // Bắt đầu đếm ngược thời gian tồn tại sau khi cắm
        stuckTimer = TickTimer.CreateFromSeconds(Runner, lifetimeAfterStuck);
    }

    void OnDrawGizmos()
    {
        // Tránh lỗi truy cập biến [Networked] khi chưa Spawn (Edit Mode)
        bool drawAsStuck = false;
        if (Application.isPlaying)
        {
            try { drawAsStuck = isStuck; } catch { }
        }

        // Vẽ hiển thị hitbox trong Scene view (luôn hiển thị để dễ chỉnh sửa)
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.matrix = rotationMatrix;
        
        // Vẽ viền ngoài
        Gizmos.color = drawAsStuck ? Color.gray : Color.red;
        Gizmos.DrawWireCube(Vector3.zero, hitboxSize);
        
        // Vẽ nền mờ bên trong để dễ nhìn hơn
        Gizmos.color = drawAsStuck ? new Color(0.5f, 0.5f, 0.5f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawCube(Vector3.zero, hitboxSize);
    }
}
