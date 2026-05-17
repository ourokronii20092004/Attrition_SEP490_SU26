using Fusion;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private PlayerAnimation animationComp;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask targetLayers;

    [Header("---- DAMAGE & SPEED ----")]
    public int attackDamage = 1;
    public int chargeAttackDamage = 2;
    [SerializeField] private float chargeThreshold = 0.25f;
    public float currentAttackSpeed = 1f;

    [Networked] public NetworkBool IsAttacking { get; set; }
    [Networked] public NetworkBool IsChargingAttack { get; set; }

    // SỬA: Chuyển biến này thành Public để PlayerController có thể đọc được trạng thái giữ nút
    [Networked] public NetworkBool IsHoldingAttack { get; set; }

    [Networked] private NetworkButtons _combatButtonsPrev { get; set; }
    [Networked] private float _holdTime { get; set; }
    [Networked] private NetworkBool _chargeTriggered { get; set; }

    private TickTimer attackCooldown;

    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<PlayerAnimation>();
    }

    public void HandleCombat(NetworkInputData data, bool isFacingRight, bool isCrouching)
    {
        if (attackPoint != null)
        {
            float sign = isFacingRight ? 1f : -1f;
            attackPoint.localPosition = new Vector3(
                Mathf.Abs(attackPoint.localPosition.x) * sign,
                attackPoint.localPosition.y,
                attackPoint.localPosition.z
            );
        }

        var pressed = data.buttons.GetPressed(_combatButtonsPrev);
        bool attackHeld = data.buttons.IsSet(MyButtons.AttackHold);
        bool attackJustPressed = pressed.IsSet(MyButtons.Attack);

        // --- J VỪA NHẤN ---
        if (attackJustPressed && attackCooldown.ExpiredOrNotRunning(Runner))
        {
            IsHoldingAttack = true;
            _holdTime = 0;
            _chargeTriggered = false;
        }

        // --- ĐANG GIỮ J ---
        if (IsHoldingAttack && attackHeld)
        {
            _holdTime += Runner.DeltaTime;

            if (_holdTime >= chargeThreshold && !_chargeTriggered)
            {
                _chargeTriggered = true;
                IsAttacking = true;
                IsChargingAttack = true;

                attackCooldown = TickTimer.CreateFromSeconds(Runner, 1.5f / currentAttackSpeed);

                if (Runner.IsForward)
                {
                    if (isCrouching)
                        RPC_PlayCrouchAttack(currentAttackSpeed);
                    else
                        RPC_PlayChargeAttack(currentAttackSpeed);
                }
            }
        }

        // --- BUÔNG J ---
        if (IsHoldingAttack && !attackHeld)
        {
            if (_chargeTriggered)
            {
                IsChargingAttack = false;
                if (Runner.IsForward) RPC_ReleaseChargeAttack();
            }
            else
            {
                if (attackCooldown.ExpiredOrNotRunning(Runner))
                {
                    IsAttacking = true;

                    attackCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f / currentAttackSpeed);

                    if (Runner.IsForward)
                    {
                        if (isCrouching)
                            RPC_PlayCrouchAttack(currentAttackSpeed);
                        else
                            RPC_PlayAttackAnimation(currentAttackSpeed);
                    }
                }
            }

            IsHoldingAttack = false;
            _holdTime = 0;
            _chargeTriggered = false;
        }

        _combatButtonsPrev = data.buttons;

        if (attackCooldown.Expired(Runner))
        {
            IsAttacking = false;
            IsChargingAttack = false;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnimation(float spd)
    {
        if (animationComp != null) animationComp.PlayAttack(spd);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayChargeAttack(float spd)
    {
        if (animationComp != null) animationComp.PlayChargeAttack(spd);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_ReleaseChargeAttack()
    {
        if (animationComp != null) animationComp.ReleaseChargeAttack();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayCrouchAttack(float spd)
    {
        if (animationComp != null) animationComp.PlayCrouchAttack(spd);
    }

    public void TriggerAttackDamage()
    {
        DealDamage(attackDamage);
    }

    public void TriggerChargeAttackDamage()
    {
        DealDamage(chargeAttackDamage);
    }

    private void DealDamage(int damage)
    {
        if (attackPoint == null) return;

        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = targetLayers;
        filter.useTriggers = false;

        int count = Runner.GetPhysicsScene2D().OverlapCircle(attackPoint.position, attackRange, filter, results);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = results[i];
            if (hit.gameObject == this.gameObject) continue;

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
            {
                Vector2 pushDir = new Vector2((hit.transform.position - transform.position).normalized.x, 0.5f).normalized;
                float force = damage > attackDamage ? 8f : 5f;
                dmg.TakeDamage(damage, pushDir, force);
            }
        }
    }

    public void FinishAttack()
    {
        IsAttacking = false;
        IsChargingAttack = false;
    }
}