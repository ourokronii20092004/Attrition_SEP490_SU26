using Fusion;
using UnityEngine;

public class EnemyAnimation : NetworkBehaviour
{
    [SerializeField] private Animator anim;
    [Tooltip("Bật lên nếu gốc của quái vật quay mặt sang trái thay vì sang phải")]
    public bool defaultFacingLeft = false;
    private Vector3 originalScale;

    public override void Spawned()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>();
        originalScale = transform.localScale;
    }

    public void UpdateSpeed(float speed)
    {
        if (anim != null) anim.SetFloat("Speed", speed);
    }

    public void FaceDirection(float dirX)
    {
        if (dirX == 0) return;
        float facingMultiplier = defaultFacingLeft ? -1f : 1f;
        transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * dirX * facingMultiplier, originalScale.y, originalScale.z);
    }

    public void PlayAttack(int attackIndex, float attackSpeed = 1f)
    {
        if (anim != null)
        {
            try { anim.SetFloat("AttackSpeed", attackSpeed); } catch { }
            try { anim.SetInteger("AttackIndex", attackIndex); } catch { }
            
            anim.SetTrigger("Attack");
        }
    }

    public void PlayHit()
    {
        if (anim != null) anim.SetTrigger("Hit");
    }

    public void PlayDeath()
    {
        if (anim != null)
        {
            anim.SetBool("IsDead", true);
            anim.SetTrigger("DieTrigger");
        }
    }

    public void ResetAlive()
    {
        if (anim == null) return;
        anim.SetBool("IsDead", false);
        anim.SetTrigger("Resurrect");
    }
}