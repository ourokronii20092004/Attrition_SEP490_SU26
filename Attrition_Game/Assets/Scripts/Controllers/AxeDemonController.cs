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
    private bool _localDeathHandled = false;

    public bool IsDead => isDeadNetworked;

    public override void Spawned()
    {
        if (HasStateAuthority) Health = maxHealth;

        rb = GetComponent<Rigidbody2D>();
        if (aiComp == null) aiComp = GetComponent<EnemyAI>();
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
        if (combatComp == null) combatComp = GetComponent<EnemyCombat>();

        _localDeathHandled = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isDeadNetworked) return;

        if (IsKnockbackActive && knockbackTimer.Expired(Runner)) IsKnockbackActive = false;

        aiComp.RunAILogic();
    }

    /// <summary>
    /// Render() chạy mỗi frame ở CẢ host và client.
    /// Khi isDeadNetworked (Networked state) chuyển thành true,
    /// cả 2 phía đều sẽ chạy HandleDeathVisuals() để thấy animation chết.
    /// </summary>
    public override void Render()
    {
        if (isDeadNetworked && !_localDeathHandled)
        {
            HandleDeathVisuals();
            _localDeathHandled = true;
        }
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

    /// <summary>
    /// Die() chỉ chạy ở Host (StateAuthority).
    /// Set các Networked state → tự động sync sang Client.
    /// Client sẽ phản ứng trong Render().
    /// </summary>
    private void Die()
    {
        isDeadNetworked = true;
        combatComp.IsAttacking = false;
        IsKnockbackActive = false;

        // Despawn sau 1.5s (chỉ Host mới despawn được)
        Invoke(nameof(DespawnEnemy), 1.5f);
    }

    /// <summary>
    /// Xử lý visual khi quái chết - chạy ở CẢ host và client.
    /// Được gọi từ Render() khi phát hiện isDeadNetworked == true.
    /// </summary>
    private void HandleDeathVisuals()
    {
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        transform.position = new Vector2(transform.position.x, transform.position.y - 0.7f);
        animationComp.PlayDeath();
    }

    private void DespawnEnemy() { if (HasStateAuthority) Runner.Despawn(Object); }
}