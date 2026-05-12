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

    [Header("---- STATE ----")]
    [Networked] public int currentHP { get; set; }
    [Networked] public NetworkBool isDeadNetworked { get; set; }
    [Networked] public NetworkBool IsGrounded { get; set; }
    [Networked] public NetworkBool IsFacingRight { get; set; } = true;
    [Networked] public NetworkBool IsMoving { get; set; }
    [Networked] public float NetworkVelocityY { get; set; }

    [Networked] private NetworkButtons _buttonsPrev { get; set; }

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

        if (GetInput(out NetworkInputData data))
        {
            rb.linearVelocity = new Vector2(data.horizontalInput * moveSpeed, rb.linearVelocity.y);
            IsMoving = Mathf.Abs(data.horizontalInput) > 0.1f;

            var pressed = data.buttons.GetPressed(_buttonsPrev);
            if (pressed.IsSet(MyButtons.Jump) && IsGrounded)
            {
                rb.position = new Vector2(rb.position.x, rb.position.y + 0.05f);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }

            if (data.horizontalInput > 0) IsFacingRight = true;
            else if (data.horizontalInput < 0) IsFacingRight = false;
            _buttonsPrev = data.buttons;

            combatComp.HandleCombat(data, IsFacingRight);
        }
    }

    public override void Render()
    {
        animationComp.UpdateAnimations(IsMoving, IsGrounded, isDeadNetworked, NetworkVelocityY, IsFacingRight);
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