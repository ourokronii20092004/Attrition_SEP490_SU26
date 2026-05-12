using Fusion;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private PlayerAnimation animationComp;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask targetLayers;

    public int attackDamage = 1;
    [Networked] public NetworkBool IsAttacking { get; set; }
    [Networked] private NetworkButtons _combatButtonsPrev { get; set; }

    private TickTimer attackCooldown;

    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<PlayerAnimation>();
    }

    public void HandleCombat(NetworkInputData data, bool isFacingRight)
    {
        if (attackPoint != null)
        {
            float sign = isFacingRight ? 1f : -1f;
            attackPoint.localPosition = new Vector3(Mathf.Abs(attackPoint.localPosition.x) * sign, attackPoint.localPosition.y, attackPoint.localPosition.z);
        }

        var pressed = data.buttons.GetPressed(_combatButtonsPrev);
        if (pressed.IsSet(MyButtons.Attack) && attackCooldown.ExpiredOrNotRunning(Runner))
        {
            IsAttacking = true;
            attackCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);

            if (Runner.IsForward)
            {
                RPC_PlayAttackAnimation();
            }
        }
        _combatButtonsPrev = data.buttons;

        if (attackCooldown.Expired(Runner)) IsAttacking = false;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnimation()
    {
        if (animationComp != null) animationComp.PlayAttack();
    }

    public void TriggerAttackDamage()
    {
        // if (!HasInputAuthority) return; 
        if (attackPoint == null) return;

        Debug.Log("==================================");

        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = targetLayers;
        filter.useTriggers = false;

        int count = Runner.GetPhysicsScene2D().OverlapCircle(attackPoint.position, attackRange, filter, results);

        Debug.Log($"[PLAYER] Fusion quét được {count} mục tiêu.");

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = results[i];
            if (hit.gameObject == this.gameObject) continue;

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
            {
                Vector2 pushDir = new Vector2((hit.transform.position - transform.position).normalized.x, 0.5f).normalized;
                dmg.TakeDamage(attackDamage, pushDir, 5f);
                Debug.Log($"[PLAYER] ---> CHÉM TRÚNG: {hit.name}");
            }
        }
    }

    public void FinishAttack()
    {
        IsAttacking = false;
    }
}