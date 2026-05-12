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
    public void UpdateAnimations(bool isMoving, bool isGrounded, bool isDead, float yVelocity, bool isFacingRight)
    {
        if (anim != null)
        {
            anim.SetFloat("Speed", isMoving ? 1f : 0f); 
            anim.SetBool("IsGrounded", isGrounded);
            anim.SetFloat("yVelocity", yVelocity); 
            anim.SetBool("IsDead", isDead);
        }

        if (sr != null) sr.flipX = !isFacingRight;
    }

    public void PlayAttack() { if (anim != null) anim.SetTrigger("Attack"); }
    public void PlayHit() { if (anim != null) anim.SetTrigger("Hit"); } 
}