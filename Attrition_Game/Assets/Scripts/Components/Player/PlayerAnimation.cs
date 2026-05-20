using Fusion;
using UnityEngine;

public class PlayerAnimation : NetworkBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer sr;

    public override void Spawned()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    public System.Collections.IEnumerator BlinkRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (sr != null) sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        if (sr != null) sr.enabled = true;
    }

    public void UpdateAnimations(
        bool isMoving, bool isGrounded, bool isDead, float yVelocity, bool isFacingRight,
        bool isCrouching, bool isDashing, bool isChargingAttack,
        bool isAttacking)
    {
        if (anim != null)
        {
            anim.SetFloat("Speed", isMoving ? 1f : 0f);
            anim.SetBool("IsGrounded", isGrounded);
            anim.SetFloat("yVelocity", yVelocity);
            anim.SetBool("IsDead", isDead);
            anim.SetBool("IsCrouching", isCrouching);
            anim.SetBool("IsDashing", isDashing);
            anim.SetBool("IsChargingAttack", isChargingAttack);

            // Kiểm tra animator đang ở attack state không (đáng tin hơn Networked state trên Client)
            var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool inAttackAnim = stateInfo.IsName("Player_Attack1")
                             || stateInfo.IsName("Player_Attack2")
                             || stateInfo.IsName("Player_Crounch_Atk");
            anim.SetBool("IsAttacking", isAttacking || inAttackAnim);
        }

        if (sr != null) sr.flipX = !isFacingRight;
    }

    public void PlayAttack(float attackSpeed = 1f)
    {
        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", attackSpeed);
            anim.SetTrigger("Attack");
        }
    }

    public void PlayChargeAttack(float attackSpeed = 1f)
    {
        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", attackSpeed);
            anim.SetTrigger("ChargeAttack");
            // Pause tại frame đầu - player đang tích lực
            anim.speed = 0f;
        }
    }

    public void ReleaseChargeAttack()
    {
        if (anim != null)
        {
            // Unpause - chạy hết animation Attack2
            anim.speed = 1f;
        }
    }

    public void PlayCrouchAttack(float attackSpeed = 1f)
    {
        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", attackSpeed);
            anim.SetTrigger("CrouchAttack");
        }
    }

    public void PlayHit()
    {
        if (anim != null)
        {
            anim.speed = 1f; // Reset speed nếu đang charge
            anim.SetTrigger("Hit");
        }
    }
}