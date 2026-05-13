using Fusion;
using UnityEngine;
using System.Collections;

public class PlayerController : NetworkBehaviour, IDamageable
{
    [Header("---- INJECT COMPONENTS ----")]
    [SerializeField] private PlayerCombat combatComp;
    [SerializeField] private PlayerAnimation animationComp;

    [Header("---- MOVEMENT & PHYSICS ----")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask groundLayer;

    [Header("---- ADVANCED MOVEMENT ----")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float crouchSpeedMultiplier = 0.4f;
    [SerializeField] private float variableJumpCutMultiplier = 0.5f;
    [SerializeField] private int maxJumps = 2;

    [Header("---- STATE ----")]
    [Networked] public int currentHP { get; set; }
    [Networked] public NetworkBool isDeadNetworked { get; set; }
    [Networked] public NetworkBool IsGrounded { get; set; }
    [Networked] public NetworkBool IsFacingRight { get; set; } = true;
    [Networked] public NetworkBool IsMoving { get; set; }
    [Networked] public float NetworkVelocityY { get; set; }

    [Networked] public NetworkBool IsCrouching { get; set; }
    [Networked] public NetworkBool IsDashing { get; set; }
    [Networked] public int JumpCount { get; set; }

    [Networked] private NetworkButtons _buttonsPrev { get; set; }
    [Networked] private TickTimer _dashTimer { get; set; }
    [Networked] private TickTimer _dashCooldown { get; set; }

    public int maxHP = 100;
    private bool isInvincible = false;

    public bool IsDead => isDeadNetworked;

    public override void Spawned()
    {
        if (HasStateAuthority) currentHP = maxHP;

        if (combatComp == null) combatComp = GetComponent<PlayerCombat>();
        if (animationComp == null) animationComp = GetComponent<PlayerAnimation>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    public override void FixedUpdateNetwork()
    {
        CheckGround();
        NetworkVelocityY = rb.linearVelocity.y;

        // Reset jump count khi chạm đất
        if (IsGrounded)
        {
            JumpCount = 0;
        }

        if (isDeadNetworked)
        {
            if (IsGrounded && rb.bodyType != RigidbodyType2D.Kinematic)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
                rb.position = new Vector2(rb.position.x, rb.position.y - 1f);
            }
            return;
        }

        // --- DASH LOGIC ---
        if (IsDashing)
        {
            if (_dashTimer.Expired(Runner))
            {
                IsDashing = false;
                rb.gravityScale = 2f;
            }
            else
            {
                // Đang dash → chỉ di chuyển ngang, không xử lý input khác
                float dashDir = IsFacingRight ? 1f : -1f;
                rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0);
                return;
            }
        }


        if (GetInput(out NetworkInputData data))
        {
            // --- CROUCH LOGIC ---
            IsCrouching = data.buttons.IsSet(MyButtons.Crouch) && IsGrounded;

            // --- MOVEMENT ---
            // Đứng yên khi đang charge attack (giữ J)
            if (combatComp.IsChargingAttack)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                IsMoving = false;
            }
            else
            {
                float speed = IsCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed;
                rb.linearVelocity = new Vector2(data.horizontalInput * speed, rb.linearVelocity.y);
                IsMoving = Mathf.Abs(data.horizontalInput) > 0.1f;
            }

            var pressed = data.buttons.GetPressed(_buttonsPrev);
            var released = _buttonsPrev.GetPressed(data.buttons); // buttons that were on, now off

            // --- JUMP LOGIC (Variable height + Double jump) ---
            if (pressed.IsSet(MyButtons.Jump) && !IsCrouching)
            {
                if (IsGrounded || JumpCount < maxJumps)
                {
                    rb.position = new Vector2(rb.position.x, rb.position.y + 0.05f);
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                    JumpCount++;
                }
            }

            // Variable jump: buông Space sớm → cắt velocity lên
            // Chỉ cắt khi VỪA BUÔNG (trước đó đang giữ, giờ không giữ)
            bool wasHoldingJump = _buttonsPrev.IsSet(MyButtons.JumpHeld);
            bool isHoldingJump = data.buttons.IsSet(MyButtons.JumpHeld);
            if (wasHoldingJump && !isHoldingJump && rb.linearVelocity.y > 0 && !IsGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * variableJumpCutMultiplier);
            }

            // --- DASH LOGIC ---
            if (pressed.IsSet(MyButtons.Dash) && IsGrounded && !IsCrouching && _dashCooldown.ExpiredOrNotRunning(Runner))
            {
                IsDashing = true;
                _dashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
                _dashCooldown = TickTimer.CreateFromSeconds(Runner, 0.8f);
                rb.gravityScale = 0;
            }

            // --- FACING ---
            if (data.horizontalInput > 0) IsFacingRight = true;
            else if (data.horizontalInput < 0) IsFacingRight = false;

            _buttonsPrev = data.buttons;

            // --- COMBAT ---
            combatComp.HandleCombat(data, IsFacingRight, IsCrouching);
        }
    }

    public override void Render()
    {
        animationComp.UpdateAnimations(
            IsMoving, IsGrounded, isDeadNetworked, NetworkVelocityY, IsFacingRight,
            IsCrouching, IsDashing, combatComp.IsChargingAttack,
            combatComp.IsAttacking
        );
    }

    private void CheckGround()
    {
        IsGrounded = rb.Cast(Vector2.down, new ContactFilter2D { layerMask = groundLayer, useLayerMask = true }, new RaycastHit2D[1], 0.05f) > 0;
    }


    public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
    {
        if (isInvincible || isDeadNetworked) return;
        RPC_TakeDamage(damage, knockbackDir, knockbackForce);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
    {
        if (isDeadNetworked) return;
        currentHP -= damage;
        RPC_ApplyKnockback(knockbackDir, knockbackForce);
        if (currentHP <= 0) Die();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyKnockback(Vector2 dir, float force)
    {
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);
        animationComp.PlayHit();
        StartCoroutine(InvincibleCoroutine());
    }

    private void Die()
    {
        isDeadNetworked = true;
    }

    IEnumerator InvincibleCoroutine()
    {
        isInvincible = true;
        StartCoroutine(animationComp.BlinkRoutine(0.8f));
        yield return new WaitForSeconds(0.8f);
        isInvincible = false;
    }
}