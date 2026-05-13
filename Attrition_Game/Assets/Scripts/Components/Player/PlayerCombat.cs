using Fusion;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private PlayerAnimation animationComp;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask targetLayers;

    [Header("---- DAMAGE ----")]
    public int attackDamage = 1;
    public int chargeAttackDamage = 2;
    [SerializeField] private float chargeThreshold = 0.25f; // Giữ J bao lâu để thành charge attack

    [Networked] public NetworkBool IsAttacking { get; set; }
    [Networked] public NetworkBool IsChargingAttack { get; set; }
    [Networked] private NetworkButtons _combatButtonsPrev { get; set; }
    [Networked] private float _holdTime { get; set; }
    [Networked] private NetworkBool _isHolding { get; set; }
    [Networked] private NetworkBool _chargeTriggered { get; set; }

    private TickTimer attackCooldown;

    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<PlayerAnimation>();
    }

    public void HandleCombat(NetworkInputData data, bool isFacingRight, bool isCrouching)
    {
        // Cập nhật hướng attack point
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
            _isHolding = true;
            _holdTime = 0;
            _chargeTriggered = false;
        }

        // --- ĐANG GIỮ J ---
        if (_isHolding && attackHeld)
        {
            _holdTime += Runner.DeltaTime;

            // Đã giữ đủ lâu → bắt đầu charge attack
            if (_holdTime >= chargeThreshold && !_chargeTriggered)
            {
                _chargeTriggered = true;
                IsAttacking = true;
                IsChargingAttack = true;
                attackCooldown = TickTimer.CreateFromSeconds(Runner, 1.5f);

                if (Runner.IsForward)
                {
                    if (isCrouching)
                        RPC_PlayCrouchAttack();
                    else
                        RPC_PlayChargeAttack();
                }
            }
        }

        // --- BUÔNG J ---
        if (_isHolding && !attackHeld)
        {
            if (_chargeTriggered)
            {
                // Đã charge → release: unpause animation, chạy hết Attack2
                IsChargingAttack = false;
                if (Runner.IsForward) RPC_ReleaseChargeAttack();
            }
            else
            {
                // Tap nhanh → attack thường
                if (attackCooldown.ExpiredOrNotRunning(Runner))
                {
                    IsAttacking = true;
                    attackCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);

                    if (Runner.IsForward)
                    {
                        if (isCrouching)
                            RPC_PlayCrouchAttack();
                        else
                            RPC_PlayAttackAnimation();
                    }
                }
            }

            _isHolding = false;
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
    private void RPC_PlayAttackAnimation()
    {
        if (animationComp != null) animationComp.PlayAttack();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayChargeAttack()
    {
        if (animationComp != null) animationComp.PlayChargeAttack();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_ReleaseChargeAttack()
    {
        if (animationComp != null) animationComp.ReleaseChargeAttack();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayCrouchAttack()
    {
        if (animationComp != null) animationComp.PlayCrouchAttack();
    }

    // Gọi từ Animation Event (Attack1)
    public void TriggerAttackDamage()
    {
        DealDamage(attackDamage);
    }

    // Gọi từ Animation Event (Attack2 charge)
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