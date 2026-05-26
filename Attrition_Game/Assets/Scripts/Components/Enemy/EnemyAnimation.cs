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

    // ─── ANIMATION EVENT: Đóng băng / Rã đông ───
    // Đặt FreezeAnimation() làm Animation Event ở frame muốn giữ lại.
    // Code sẽ gọi UnfreezeAnimation() khi dash/leap xong để tiếp tục animation.

    /// <summary>
    /// [ANIMATION EVENT] Đóng băng animation tại frame hiện tại.
    /// Thêm event này vào animation clip ở frame muốn giữ (vd: frame giơ kiếm).
    /// </summary>
    public void FreezeAnimation()
    {
        if (anim != null) anim.speed = 0f;
    }

    /// <summary>
    /// Rã đông animation — tiếp tục chơi từ frame đang đóng băng.
    /// Gọi bởi code khi dash/leap kết thúc.
    /// </summary>
    public void UnfreezeAnimation()
    {
        if (anim != null) anim.speed = 1f;
    }

    public void PlayTeleport()
    {
        if (anim != null) anim.SetTrigger("Teleport");
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

    // ─── SLEEP / WAKEUP (Bat-type enemies) ───
    public void PlaySleep()
    {
        if (anim != null)
        {
            anim.SetBool("IsSleeping", true);
            anim.SetTrigger("Sleep");
        }
    }

    public void PlayWakeUp()
    {
        if (anim != null)
        {
            anim.SetBool("IsSleeping", false);
            anim.SetTrigger("WakeUp");
        }
    }

    // ─── HEALING (Elite only) ───
    public void PlayHealing()
    {
        if (anim != null)
        {
            anim.SetBool("IsHealing", true);
            anim.SetTrigger("Heal");
        }
    }

    public void StopHealing()
    {
        if (anim != null)
        {
            anim.SetBool("IsHealing", false);
        }
    }
}