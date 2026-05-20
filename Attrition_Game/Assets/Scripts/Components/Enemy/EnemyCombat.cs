using Fusion;
using UnityEngine;

public class EnemyCombat : NetworkBehaviour
{
    [SerializeField] private EnemyAnimation animationComp;
    public Transform attackPoint;
    public float attackRange = 1.5f;
    [Range(0, 360)] public float attackAngle = 90f;
    public LayerMask playerLayer;

    [Header("---- RANGED ATTACK ----")]
    public bool isRanged;
    public NetworkPrefabRef projectilePrefab;
    public Transform projectileSpawnPoint;

    [Header("---- DAMAGE & SPEED ----")]
    public int attackDamage = 1;
    [Tooltip("Số kiểu đòn đánh (1 = chỉ có Attack1, 2 = có Attack1 và Attack2)")]
    public int attackVariants = 1;
    [Tooltip("Thời gian đứng yên khi đánh - nên bằng độ dài animation attack (giây)")]
    public float attackDuration = 0.6f;
    [Tooltip("Thời gian nghỉ giữa 2 đòn đánh (Cooldown - Tính bằng giây)")]
    public float attackCooldown = 1.0f;
    [Tooltip("Tốc độ phát Animation đánh (1 là mặc định, 2 là nhanh gấp đôi)")]
    public float currentAttackSpeed = 1f;

    [HideInInspector][Networked] public NetworkBool IsAttacking { get; set; }
    [Networked] private TickTimer attackTimer { get; set; }
    [Networked] private TickTimer cooldownTimer { get; set; }

    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
    }

    public override void FixedUpdateNetwork()
    {
        // ExpiredOrNotRunning thay vì Expired: tránh miss tick duy nhất gây kẹt IsAttacking
        if (IsAttacking && attackTimer.ExpiredOrNotRunning(Runner))
        {
            IsAttacking = false;
            cooldownTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
            attackTimer = TickTimer.None;
        }
    }

    public bool CanAttack()
    {
        return !IsAttacking && cooldownTimer.ExpiredOrNotRunning(Runner);
    }

    public void AttemptAttack()
    {
        if (!CanAttack()) return;

        IsAttacking = true;
        int randomAttackIndex = Random.Range(0, attackVariants);

        RPC_PlayAttackAnim(randomAttackIndex, currentAttackSpeed);

        // Quái đứng yên trong attackDuration (chia cho tốc độ đánh)
        attackTimer = TickTimer.CreateFromSeconds(Runner, attackDuration / currentAttackSpeed);
    }

    // Vẫn giữ lại cho ai muốn dùng Animation Event (không bắt buộc)
    public void FinishAttack()
    {
        if (!IsAttacking) return;
        IsAttacking = false;
        if (HasStateAuthority)
        {
            cooldownTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
        }
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnim(int attackIndex, float speed)
    {
        if (animationComp != null) animationComp.PlayAttack(attackIndex, speed);
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);

        Vector3 facingDirection = transform.localScale.x > 0 ? Vector3.right : Vector3.left;

        Vector3 upperLimit = Quaternion.Euler(0, 0, attackAngle / 2f) * facingDirection;
        Vector3 lowerLimit = Quaternion.Euler(0, 0, -attackAngle / 2f) * facingDirection;

        Gizmos.color = new Color(1, 0.92f, 0.016f, 0.7f);
        Gizmos.DrawRay(transform.position, upperLimit * attackRange);
        Gizmos.DrawRay(transform.position, lowerLimit * attackRange);
    }

    public void TriggerAttackDamage()
    {
        if (attackPoint == null) return;

        if (isRanged)
        {
            if (projectileSpawnPoint == null || !projectilePrefab.IsValid)
            {
                Debug.LogWarning("Chưa gắn Projectile Prefab hoặc Spawn Point cho " + gameObject.name);
                return;
            }
            
            if (!HasStateAuthority) return; // Chỉ có Host mới được tạo đạn để đồng bộ cho các máy khác

            // Tìm người chơi gần nhất trong tầm để bắn chính xác (đặc biệt khi quái bay cần bắn chéo xuống)
            Vector2 shootDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            Collider2D[] rangeResults = new Collider2D[5];
            ContactFilter2D rangeFilter = new ContactFilter2D();
            rangeFilter.useLayerMask = true;
            rangeFilter.layerMask = playerLayer;
            rangeFilter.useTriggers = false;
            
            int pCount = Runner.GetPhysicsScene2D().OverlapCircle(transform.position, attackRange * 1.5f, rangeFilter, rangeResults);
            float closestDist = float.MaxValue;
            Transform targetPlayer = null;
            
            for (int i = 0; i < pCount; i++)
            {
                float dist = Vector2.Distance(transform.position, rangeResults[i].transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetPlayer = rangeResults[i].transform;
                }
            }

            if (targetPlayer != null)
            {
                shootDir = (targetPlayer.position - projectileSpawnPoint.position).normalized;
            }

            Runner.Spawn(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity, null, (runner, obj) =>
            {
                EnemyProjectile proj = obj.GetComponent<EnemyProjectile>();
                if (proj != null) proj.Init(shootDir, attackDamage, 8f);
            });
            return;
        }

        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = playerLayer;
        filter.useTriggers = false;

        int count = Runner.GetPhysicsScene2D().OverlapCircle(attackPoint.position, attackRange, filter, results);

        for (int i = 0; i < count; i++)
        {
            Collider2D player = results[i];

            Vector2 directionToPlayer = (player.transform.position - transform.position).normalized;
            Vector2 facingDirection = transform.localScale.x > 0 ? Vector2.right : Vector2.left;

            if (Vector2.Angle(facingDirection, directionToPlayer) < attackAngle / 2f)
            {
                IDamageable dmg = player.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead)
                {
                    Vector2 pushDir = new Vector2(directionToPlayer.x, 0.5f).normalized;
                    dmg.TakeDamage(attackDamage, pushDir, 0f); // force=0 → Player sẽ dùng knockbackForceOverride
                }
            }
        }
    }
}