using Fusion;
using UnityEngine;

public class EnemyAnimation : NetworkBehaviour
{
    [SerializeField] private Animator anim;
    [Tooltip("Bật lên nếu gốc của quái vật quay mặt sang trái thay vì sang phải")]
    public bool defaultFacingLeft = false;
    [Header("---- TELEGRAPH ----")]
    [Tooltip("SpriteRenderer dùng để nhấp nháy báo đòn. Bỏ trống = tự tìm trong children.")]
    [SerializeField] private SpriteRenderer telegraphRenderer;
    [Tooltip("Màu nhấp nháy khi báo đòn nặng.")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.4f, 0.4f);
    private Vector3 originalScale;
    private float lastAppliedFacing;
    private Color _baseColor = Color.white;
    private bool _telegraphActive;
    private float _telegraphBlink;

    public override void Spawned()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>();
        originalScale = transform.localScale;
        lastAppliedFacing = 0f;
        CacheClipLengths();
        if (telegraphRenderer == null) telegraphRenderer = FindBodyRenderer();
        if (telegraphRenderer != null) _baseColor = telegraphRenderer.color;
    }

    /// <summary>
    /// Tìm SpriteRenderer THÂN quái cho telegraph, BỎ QUA các renderer UI runtime
    /// (HealthBar/DmgPopup/PlayerNameTag) do EnemyWorldUI tạo trong Awake (chạy trước Spawned).
    /// </summary>
    private SpriteRenderer FindBodyRenderer()
    {
        var all = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in all)
        {
            if (sr == null) continue;
            string n = sr.gameObject.name;
            if (n == "BG" || n == "Fill" || n == "Arrow") continue;
            var p = sr.transform.parent;
            bool underUI = false;
            while (p != null)
            {
                if (p.name == "HealthBar" || p.name == "PlayerNameTag") { underUI = true; break; }
                p = p.parent;
            }
            if (!underUI) return sr;
        }
        return all.Length > 0 ? all[0] : null;
    }

    private void Update()
    {
        // Nhấp nháy báo đòn (chạy mọi máy — chỉ là hiệu ứng hình).
        if (telegraphRenderer == null) return;
        if (_telegraphActive)
        {
            _telegraphBlink += Time.deltaTime * 14f;
            float t = (Mathf.Sin(_telegraphBlink) + 1f) * 0.5f;
            telegraphRenderer.color = Color.Lerp(_baseColor, telegraphColor, t);
        }
    }

    /// <summary>Bật/tắt nhấp nháy báo đòn (gọi từ EnemyAI khi vào/ra trạng thái telegraph).</summary>
    public void SetTelegraph(bool on)
    {
        _telegraphActive = on;
        _telegraphBlink = 0f;
        if (telegraphRenderer != null && !on) telegraphRenderer.color = _baseColor;
    }

    // Cho phép đồng bộ duration (chết/hồi sinh/đánh/skill) theo đúng độ dài animation.
    private System.Collections.Generic.Dictionary<string, float> _clipLengths;

    private void CacheClipLengths()
    {
        _clipLengths = new System.Collections.Generic.Dictionary<string, float>();
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (var clip in anim.runtimeAnimatorController.animationClips)
            if (clip != null) _clipLengths[clip.name] = clip.length;
    }

    /// <summary>
    /// Độ dài clip animation (giây) theo tên. Trả về fallback nếu không tìm thấy.
    /// Dùng để khớp thời gian chết/hồi sinh/đánh/skill với animation thực tế.
    /// </summary>
    public float GetClipLength(string clipName, float fallback = 0.5f)
    {
        if (string.IsNullOrEmpty(clipName) || _clipLengths == null) return fallback;
        return _clipLengths.TryGetValue(clipName, out float len) && len > 0.01f ? len : fallback;
    }

    public void UpdateSpeed(float speed)
    {
        if (anim != null && HasParam("Speed")) anim.SetFloat("Speed", speed);
    }

    /// <summary>
    /// Animator có param bay không? Đa số quái đi đất KHÔNG có → gọi bên EnemyAI để khỏi phải chạy
    /// IsGrounded() (một rb.Cast physics) mỗi frame cho mỗi con quái chỉ để rồi bỏ đi.
    /// </summary>
    public bool NeedsAirState => anim != null && (HasParam("VelocityY") || HasParam("IsGrounded"));

    public void UpdateAirState(float velocityY, bool isGrounded)
    {
        if (anim == null) return;
        // Nhiều quái KHÔNG có param bay (VelocityY/IsGrounded) → SetFloat/SetBool sẽ spam log lỗi
        // mỗi Render frame (gây giật). Chỉ set khi animator THẬT SỰ có param đó. try/catch không
        // chặn được vì Unity log trước khi ném.
        if (HasParam("VelocityY")) anim.SetFloat("VelocityY", velocityY);
        if (HasParam("IsGrounded")) anim.SetBool("IsGrounded", isGrounded);
    }

    // Cache tên param của animator để tránh set param không tồn tại (spam log + tốn hiệu năng).
    private System.Collections.Generic.HashSet<string> _params;
    private bool HasParam(string name)
    {
        if (anim == null) return false;
        if (_params == null)
        {
            _params = new System.Collections.Generic.HashSet<string>();
            foreach (var p in anim.parameters) _params.Add(p.name);
        }
        return _params.Contains(name);
    }

    public void FaceDirection(float dirX)
    {
        if (dirX == 0) return;
        // Cache: không set localScale nếu hướng không thay đổi
        float snapped = dirX > 0 ? 1f : -1f;
        if (snapped == lastAppliedFacing) return;
        lastAppliedFacing = snapped;
        float facingMultiplier = defaultFacingLeft ? -1f : 1f;
        transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * snapped * facingMultiplier, originalScale.y, originalScale.z);
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

    private Coroutine _hitFlashCoroutine;

    public void PlayHit()
    {
        if (anim != null) anim.SetTrigger("Hit");
        if (telegraphRenderer != null)
        {
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }
    }

    private System.Collections.IEnumerator HitFlashRoutine()
    {
        telegraphRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!_telegraphActive) telegraphRenderer.color = _baseColor;
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

    public void PlayJump()
    {
        if (anim != null) anim.SetTrigger("Jump");
    }

    public void PlaySkill(int skillIndex)
    {
        if (anim != null)
        {
            try { anim.SetInteger("SkillIndex", skillIndex); } catch { }
            anim.SetTrigger("Skill");
        }
    }

    public void PlaySummon()
    {
        if (anim != null) anim.SetTrigger("Summon");
    }

    public void PlayAppear()
    {
        if (anim != null) anim.SetTrigger("Appear");
    }

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