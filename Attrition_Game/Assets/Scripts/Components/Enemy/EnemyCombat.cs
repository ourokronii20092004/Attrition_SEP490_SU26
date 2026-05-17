using Fusion;
using UnityEngine;

public class EnemyCombat : NetworkBehaviour
{
    [SerializeField] private EnemyAnimation animationComp;
    public Transform attackPoint;
    public float attackRange = 1.5f;
    [Range(0, 360)] public float attackAngle = 90f;
    public LayerMask playerLayer;

    [Header("---- DAMAGE & SPEED ----")]
    public int attackDamage = 1;
    [Tooltip("Tốc độ đánh (1 là mặc định, 2 là nhanh gấp đôi)")]
    public float currentAttackSpeed = 1f;

    [Networked] public NetworkBool IsAttacking { get; set; }
    [Networked] private TickTimer attackTimer { get; set; }

    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
    }

    public override void FixedUpdateNetwork()
    {
        if (IsAttacking && attackTimer.Expired(Runner)) IsAttacking = false;
    }

    public void AttemptAttack()
    {
        if (IsAttacking || !attackTimer.ExpiredOrNotRunning(Runner)) return;

        IsAttacking = true;
        int randomAttackIndex = Random.Range(0, 2);

        // Truyền tốc độ đánh sang cho mọi Client cập nhật hình ảnh
        RPC_PlayAttackAnim(randomAttackIndex, currentAttackSpeed);

        // Chia thời gian chờ (1.5s mặc định) cho Tốc độ đánh
        attackTimer = TickTimer.CreateFromSeconds(Runner, 1.5f / currentAttackSpeed);
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
                    dmg.TakeDamage(attackDamage, pushDir, 8f);
                }
            }
        }
    }

    public void FinishAttack()
    {
        IsAttacking = false;
    }
}