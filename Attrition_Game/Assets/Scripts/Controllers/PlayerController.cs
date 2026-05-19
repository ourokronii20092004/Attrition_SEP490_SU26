using Fusion;
using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class PlayerController : NetworkBehaviour, IDamageable
{
    [Header("---- INJECT COMPONENTS ----")]
    [SerializeField] private PlayerCombat combatComp;
    [SerializeField] private PlayerAnimation animationComp;

    [Header("---- MOVEMENT & PHYSICS ----")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float doubleJumpForce = 12f;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask groundLayer;

    [Header("---- ADVANCED MOVEMENT ----")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float crouchSpeedMultiplier = 0.4f;
    [SerializeField] private float variableJumpCutMultiplier = 0.5f;
    [SerializeField] private int maxJumps = 2;

    [Header("---- HOLLOW KNIGHT GRAVITY ----")]
    [Tooltip("Trọng lực mặc định khi đi trên mặt đất hoặc bay lên")]
    [SerializeField] private float normalGravity = 2f;
    [Tooltip("Trọng lực khi rơi xuống (càng cao rơi càng nhanh, Hollow Knight ~4-5)")]
    [SerializeField] private float fallGravity = 4.5f;
    [Tooltip("Tốc độ rơi tối đa (giới hạn để không rơi quá nhanh)")]
    [SerializeField] private float maxFallSpeed = -25f;

    [Header("---- KNOCKBACK (KHI PLAYER BỊ ĐÁNH) ----")]
    [Tooltip("Lực đẩy lùi khi bị quái đánh (set 0 để không bị knockback)")]
    [SerializeField] private float knockbackForceOverride = 6f;
    [Tooltip("Thời gian bị khựng không điều khiển được sau khi bị đánh (giây)")]
    [SerializeField] private float knockbackDuration = 0.25f;
    [Tooltip("Thời gian bất tử sau khi bị đánh (giây)")]
    [SerializeField] private float invincibleDuration = 0.8f;

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
    [Networked] private TickTimer _knockbackTimer { get; set; }

    public int maxHP = 100;
    private bool isInvincible = false;

    public bool IsDead => isDeadNetworked;

    public override void Spawned()
    {
        if (HasStateAuthority) currentHP = maxHP;

        if (combatComp == null) combatComp = GetComponent<PlayerCombat>();
        if (animationComp == null) animationComp = GetComponent<PlayerAnimation>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // Tắt va chạm vật lý giữa Player và Enemy để không bị đẩy nhau gây nhấp nháy
        int playerLayer = gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }

        // Set camera to follow local player
        if (HasInputAuthority)
        {
            var cam = FindAnyObjectByType<CinemachineCamera>();
            if (cam != null)
            {
                cam.Follow = transform;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        CheckGround();
        NetworkVelocityY = rb.linearVelocity.y;

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
            }
            return;
        }

        // --- KNOCKBACK: Khóa toàn bộ input khi đang bị đẩy lùi ---
        if (!_knockbackTimer.ExpiredOrNotRunning(Runner))
        {
            NetworkVelocityY = rb.linearVelocity.y;
            return;
        }

        // --- HOLLOW KNIGHT FAST FALL ---
        // Khi đang rơi xuống (velocity Y < 0) và không đang dash -> tăng trọng lực
        if (!IsDashing)
        {
            if (rb.linearVelocity.y < 0)
            {
                rb.gravityScale = fallGravity;
                // Giới hạn tốc độ rơi tối đa
                if (rb.linearVelocity.y < maxFallSpeed)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);
                }
            }
            else
            {
                rb.gravityScale = normalGravity;
            }
        }

        // --- DASH LOGIC ---
        if (IsDashing)
        {
            if (_dashTimer.Expired(Runner))
            {
                IsDashing = false;
                rb.gravityScale = normalGravity;
            }
            else
            {
                float dashDir = IsFacingRight ? 1f : -1f;
                rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0);
                return;
            }
        }


        if (GetInput(out NetworkInputData data))
        {
            IsCrouching = data.buttons.IsSet(MyButtons.Crouch) && IsGrounded;

            // --- MOVEMENT ---
            // SỬA: Chỉ khóa di chuyển ngang khi đang đứng trên mặt đất. Trên không vẫn cho phép di chuyển
            if ((combatComp.IsHoldingAttack || combatComp.IsAttacking) && IsGrounded)
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
            var released = _buttonsPrev.GetPressed(data.buttons);

            // --- JUMP LOGIC ---
            // SỬA: Khóa nhảy khi đang giữ nút J
            if (pressed.IsSet(MyButtons.Jump) && !IsCrouching && !combatComp.IsHoldingAttack)
            {
                if (IsGrounded || JumpCount < maxJumps)
                {
                    // SỬA LỖI GÓC ĐẤT: Reset velocity Y về 0 trước khi nhảy
                    // Tránh trường hợp velocity Y đang dương (do trượt góc đất) cộng dồn với jumpForce gây nhảy quá cao
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                    rb.position = new Vector2(rb.position.x, rb.position.y + 0.05f);
                    float currentJumpForce = (JumpCount > 0) ? doubleJumpForce : jumpForce;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentJumpForce);
                    JumpCount++;
                }
            }

            bool wasHoldingJump = _buttonsPrev.IsSet(MyButtons.JumpHeld);
            bool isHoldingJump = data.buttons.IsSet(MyButtons.JumpHeld);
            // SỬA: Chỉ cho phép ngắt độ cao nhảy ở lần nhảy đầu tiên (JumpCount <= 1). Jump lần 2 có độ cao cố định.
            if (wasHoldingJump && !isHoldingJump && rb.linearVelocity.y > 0 && !IsGrounded && JumpCount <= 1)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * variableJumpCutMultiplier);
            }

            // --- DASH LOGIC ---
            // SỬA: XÓA ĐIỀU KIỆN `IsGrounded` ĐỂ CÓ THỂ LƯỚT TRÊN KHÔNG
            if (pressed.IsSet(MyButtons.Dash) && !IsCrouching && !combatComp.IsHoldingAttack && _dashCooldown.ExpiredOrNotRunning(Runner))
            {
                IsDashing = true;
                _dashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
                _dashCooldown = TickTimer.CreateFromSeconds(Runner, 0.8f);
                rb.gravityScale = 0; // Tắt trọng lực để nhân vật lướt thẳng băng trên không
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

        // Luôn dùng knockbackForceOverride từ Inspector để điều chỉnh lực đẩy lùi
        RPC_ApplyKnockback(knockbackDir, knockbackForceOverride);
        if (currentHP <= 0) Die();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyKnockback(Vector2 dir, float force)
    {
        if (force <= 0) 
        {
            // Không knockback, chỉ chớp sáng
            animationComp.PlayHit();
            StartCoroutine(InvincibleCoroutine());
            return;
        }
        
        rb.linearVelocity = dir * force;

        // Khóa input trong thời gian knockbackDuration để velocity không bị ghi đè
        if (HasStateAuthority)
        {
            _knockbackTimer = TickTimer.CreateFromSeconds(Runner, knockbackDuration);
        }

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
        StartCoroutine(animationComp.BlinkRoutine(invincibleDuration));
        yield return new WaitForSeconds(invincibleDuration);
        isInvincible = false;
    }
}