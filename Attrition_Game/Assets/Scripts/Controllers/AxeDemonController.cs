using Fusion;
using UnityEngine;

public class AxeDemonController : NetworkBehaviour, IDamageable
{
    [Header("---- INJECT COMPONENTS ----")]
    [SerializeField] private EnemyAI aiComp;
    [SerializeField] private EnemyAnimation animationComp;
    [SerializeField] private EnemyCombat combatComp;

    [Header("---- STATE ----")]
    [Networked] public int Health { get; set; }
    [Networked] public NetworkBool isDeadNetworked { get; set; }
    [Networked] public NetworkBool IsKnockbackActive { get; set; }
    [Networked] private TickTimer knockbackTimer { get; set; }

    public int maxHealth = 3;
    private Rigidbody2D rb;

    public bool IsDead => isDeadNetworked;

    public override void Spawned()
    {
        if (HasStateAuthority) Health = maxHealth;

        rb = GetComponent<Rigidbody2D>();
        if (aiComp == null) aiComp = GetComponent<EnemyAI>();
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
        if (combatComp == null) combatComp = GetComponent<EnemyCombat>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isDeadNetworked) return;

        if (IsKnockbackActive && knockbackTimer.Expired(Runner)) IsKnockbackActive = false;

        aiComp.RunAILogic();
    }

    public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
    {
        if (isDeadNetworked) return;
        RPC_TakeDamage(damage, knockbackDir, knockbackForce);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
    {
        if (isDeadNetworked) return;

        Health -= damage;
        aiComp.ForceFacePlayer();

        if (Health <= 0)
        {
            Die();
        }
        else
        {
            IsKnockbackActive = true;
            knockbackTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);
            RPC_ApplyKnockback(knockbackDir, knockbackForce);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyKnockback(Vector2 dir, float force)
    {
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);
        if (!combatComp.IsAttacking) animationComp.PlayHit();
    }

    private void Die()
    {
        isDeadNetworked = true;
        combatComp.IsAttacking = false;
        IsKnockbackActive = false;

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Collider2D>().enabled = false;

        transform.position = new Vector2(transform.position.x, transform.position.y - 0.7f);

        animationComp.PlayDeath();

        Invoke(nameof(DespawnEnemy), 1.2f);
    }

    private void DespawnEnemy() { if (HasStateAuthority) Runner.Despawn(Object); }
}