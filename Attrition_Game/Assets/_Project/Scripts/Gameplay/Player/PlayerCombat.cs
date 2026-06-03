using Fusion;
using UnityEngine;
using Attrition.Gameplay.Player;

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private PlayerAnimation animationComp;
    [Tooltip("Tùy chọn: nguồn chỉ số. Bỏ trống = dùng attackDamage/chargeAttackDamage serialized.")]
    [SerializeField] private PlayerStats statsComp;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [Range(0, 360)]
    [Tooltip("Góc đánh của Player (180 = nửa vòng phía trước, 360 = xung quanh)")]
    [SerializeField] private float attackAngle = 180f;
    [SerializeField] private LayerMask targetLayers;

    [Header("---- DAMAGE & SPEED ----")]
    public Attrition.Core.DamageType normalAttackType = Attrition.Core.DamageType.Physical;
    public Attrition.Core.DamageType chargeAttackType = Attrition.Core.DamageType.Physical;
    [SerializeField] private float chargeThreshold = 0.25f;
    
    private float CurrentAttackSpeed => statsComp != null ? statsComp.AttackSpeed : 1f;

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
        if (statsComp == null) statsComp = GetComponent<PlayerStats>();
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
            // Kiểm tra xem có đủ 10 Stamina để tung Light Attack không
            bool canAffordLight = statsComp == null || statsComp.HasStamina(10f);
            if (canAffordLight)
            {
                IsHoldingAttack = true;
                _holdTime = 0;
                _chargeTriggered = false;
            }
        }

        // --- ĐANG GIỮ J ---
        if (IsHoldingAttack && attackHeld)
        {
            _holdTime += Runner.DeltaTime;

            if (_holdTime >= chargeThreshold && !_chargeTriggered)
            {
                // Kiểm tra xem có đủ 20 Stamina để tung Heavy Attack không
                bool canAffordHeavy = statsComp == null || statsComp.HasStamina(20f);
                if (canAffordHeavy)
                {
                    if (statsComp != null) statsComp.TryConsumeStamina(20f);
                    
                    _chargeTriggered = true;
                    IsAttacking = true;
                    IsChargingAttack = true;

                    attackCooldown = TickTimer.CreateFromSeconds(Runner, 1.5f / CurrentAttackSpeed);

                    if (Runner.IsForward)
                    {
                        if (isCrouching)
                            RPC_PlayCrouchAttack(CurrentAttackSpeed);
                        else
                            RPC_PlayChargeAttack(CurrentAttackSpeed);
                    }
                }
                else
                {
                    // Hủy trạng thái giữ phím nếu không đủ Stamina (nhân vật không làm gì cả)
                    IsHoldingAttack = false;
                    _holdTime = 0;
                }
            }
        }

        // --- BUÔNG J ---
        if (IsHoldingAttack && !attackHeld)
        {
            if (_chargeTriggered)
            {
                // Stamina đã được trừ lúc kích hoạt Charge
                IsChargingAttack = false;
                if (Runner.IsForward) RPC_ReleaseChargeAttack();
            }
            else
            {
                if (attackCooldown.ExpiredOrNotRunning(Runner))
                {
                    // Trừ 10 Stamina cho Light Attack
                    bool consumed = statsComp == null || statsComp.TryConsumeStamina(10f);
                    if (consumed)
                    {
                        IsAttacking = true;

                        attackCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f / CurrentAttackSpeed);

                        if (Runner.IsForward)
                        {
                            if (isCrouching)
                                RPC_PlayCrouchAttack(CurrentAttackSpeed);
                            else
                                RPC_PlayAttackAnimation(CurrentAttackSpeed);
                        }
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
        DealDamage(NormalAttackRaw, normalAttackType);
    }

    public void TriggerChargeAttackDamage()
    {
        DealDamage(ChargeAttackRaw, chargeAttackType);
    }

    // Raw AD/AP truyền cho defender (defender tự trừ DEF/RES).
    private int NormalAttackRaw => statsComp != null ? (normalAttackType == Attrition.Core.DamageType.Magic ? statsComp.AP : statsComp.AD) : 1;
    private int ChargeAttackRaw => statsComp != null
        ? Mathf.RoundToInt((chargeAttackType == Attrition.Core.DamageType.Magic ? statsComp.AP : statsComp.AD) * statsComp.ChargeDamageMultiplier)
        : 2;

    private void DealDamage(int damage, Attrition.Core.DamageType type)
    {
        if (attackPoint == null) return;

        // Xác định hướng Player đang nhìn dựa vào vị trí attackPoint
        Vector2 facingDir = attackPoint.localPosition.x >= 0 ? Vector2.right : Vector2.left;

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

            // CHỈ gây sát thương cho mục tiêu trong góc đánh
            Vector2 dirToTarget = (hit.transform.position - transform.position).normalized;
            if (Vector2.Angle(facingDir, dirToTarget) > attackAngle / 2f) continue;

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
            {
                Vector2 pushDir = new Vector2(dirToTarget.x, 0.5f).normalized;
                float force = damage >= ChargeAttackRaw ? 8f : 5f;
                dmg.TakeDamage(damage, pushDir, force, type);
            }
        }
    }

    public void FinishAttack()
    {
        IsAttacking = false;
        IsChargingAttack = false;
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        // Xác định hướng nhìn
        Vector3 facingDir = attackPoint.localPosition.x >= 0 ? Vector3.right : Vector3.left;

        // Vòng tròn tầm đánh — vàng
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.4f);
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);

        // Tô mờ vùng đánh
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.07f);
        Gizmos.DrawSphere(attackPoint.position, attackRange);

        // Tia biên góc đánh
        Vector3 upperLimit = Quaternion.Euler(0, 0, attackAngle / 2f) * facingDir;
        Vector3 lowerLimit = Quaternion.Euler(0, 0, -attackAngle / 2f) * facingDir;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawRay(attackPoint.position, upperLimit * attackRange);
        Gizmos.DrawRay(attackPoint.position, lowerLimit * attackRange);

        // Đường trung tâm — cam
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
        Gizmos.DrawRay(attackPoint.position, facingDir * attackRange);
    }
}