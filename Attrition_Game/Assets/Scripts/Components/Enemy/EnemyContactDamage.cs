using UnityEngine;
using Attrition.Controllers;

/// <summary>
/// Gắn vào Child Object của Enemy. Child cần có Collider2D với Is Trigger = true.
/// LƯU Ý: Layer của Child này KHÔNG được đặt là "Enemy" (để tránh bị IgnoreLayerCollision chặn).
/// Hãy để layer mặc định (Default) hoặc tạo layer riêng (ví dụ "EnemyHitbox").
/// </summary>
public class EnemyContactDamage : MonoBehaviour
{
    [Header("---- CONTACT DAMAGE ----")]
    [Tooltip("Sát thương khi Player chạm vào quái")]
    public int contactDamage = 1;
    [Tooltip("Lực đẩy lùi khi chạm quái (0 = dùng knockbackForceOverride của Player)")]
    public float contactKnockbackForce = 5f;
    [Tooltip("Thời gian hồi (giây) giữa 2 lần gây sát thương chạm")]
    public float contactCooldown = 0.5f;

    private EnemyController enemyController;
    private float lastContactTime = -999f;

    private void Awake()
    {
        enemyController = GetComponentInParent<EnemyController>();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time - lastContactTime < contactCooldown) return;
        if (enemyController != null && enemyController.IsDead) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        // Hướng đẩy lùi: từ quái ra phía Player, hơi hất lên
        Vector2 knockbackDir = (other.transform.position - transform.parent.position).normalized;
        knockbackDir = new Vector2(knockbackDir.x, 0.4f).normalized;

        damageable.TakeDamage(contactDamage, knockbackDir, contactKnockbackForce);
        lastContactTime = Time.time;
    }
}
