using Fusion;
using UnityEngine;

public class EnemyAnimation : NetworkBehaviour
{
    [SerializeField] private Animator anim;
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
        transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * dirX, originalScale.y, originalScale.z);
    }

    public void PlayAttack(int attackIndex)
    {
        if (anim != null)
        {
            anim.SetInteger("AttackIndex", attackIndex);
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
            // Thêm Trigger để giải quyết triệt để lỗi Any State lặp vòng
            anim.SetTrigger("DieTrigger");
        }
    }

    public void ResetAlive()
    {
        if (anim == null) return;
        anim.SetBool("IsDead", false);
        // Gọi Trigger hồi sinh để Skeleton biết lúc nào cần đứng dậy
        anim.SetTrigger("Resurrect");
    }
}