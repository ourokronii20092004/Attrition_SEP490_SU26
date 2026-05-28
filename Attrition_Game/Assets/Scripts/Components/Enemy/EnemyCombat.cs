using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class EnemyCombat : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    // ENUMS
    // ═══════════════════════════════════════════════════════════════
    public enum AttackStyle { Normal, DashSlash, LeapAttack }
    public enum HitboxShape { Cone, Circle, Rectangle }

    // ═══════════════════════════════════════════════════════════════
    // ATTACK CONFIG — Mỗi đòn đánh gom hết thông số tại 1 chỗ
    // ═══════════════════════════════════════════════════════════════
    [System.Serializable]
    public class AttackConfig
    {
        [Header("── Hitbox ──")]
        [Tooltip("Hình dạng vùng gây sát thương: Cone (hình nón), Circle (tròn 360°), Rectangle (hình chữ nhật)")]
        public HitboxShape hitboxShape = HitboxShape.Cone;

        [Header("── Phạm vi & Góc ──")]
        [Tooltip("Phạm vi tấn công (bán kính)")]
        public float range = 1.5f;
        [Tooltip("Góc đánh (chỉ dùng cho Cone). 90 = hẹp, 180 = nửa vòng, 360 = toàn bộ")]
        [Range(0, 360)]
        public float angle = 90f;
        [Tooltip("Kích thước hình chữ nhật (width × height). Chỉ dùng khi hitboxShape = Rectangle")]
        public Vector2 rectSize = new Vector2(1f, 1f);

        [Header("── Sát thương ──")]
        [Tooltip("Sát thương gây ra")]
        public int damage = 1;

        [Header("── Thời gian ──")]
        [Tooltip("Thời gian đứng yên khi đánh - nên bằng độ dài animation attack (giây)")]
        public float duration = 0.6f;
        [Tooltip("Thời gian nghỉ giữa 2 đòn đánh (Cooldown - giây)")]
        public float cooldown = 1.0f;
    }

    // ═══════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ═══════════════════════════════════════════════════════════════
    [Header("---- DANH SÁCH ĐÒN TẤN CÔNG ----")]
    [Tooltip("Mỗi phần tử = 1 đòn đánh (Attack1, Attack2, ...) với đầy đủ thông số riêng")]
    public AttackConfig[] attacks = new AttackConfig[] { new AttackConfig() };

    [Header("---- DASH-SLASH (Lướt đến chém) ----")]
    [Tooltip("Bật nếu quái lao về phía Player khi tấn công")]
    public bool enableDashSlash;
    [Tooltip("Tick đòn attack nào được dùng khi lướt (index 0 = Attack1, 1 = Attack2, ...)")]
    public bool[] dashSlashAttacks = new bool[] { true };
    [Tooltip("Tốc độ lao tới khi dash")]
    public float dashSpeed = 10f;
    [Tooltip("Thời gian lao tới (giây) - nên ngắn hơn duration của đòn")]
    public float dashDuration = 0.3f;

    [Header("---- LEAP ATTACK (Nhảy đánh xuống) ----")]
    [Tooltip("Bật nếu quái nhảy lên rồi đánh xuống vị trí Player")]
    public bool enableLeapAttack;
    [Tooltip("Tick đòn attack nào được dùng khi nhảy (index 0 = Attack1, 1 = Attack2, ...)")]
    public bool[] leapAttacks = new bool[] { true };
    [Tooltip("Chiều cao nhảy lên (units)")]
    public float leapHeight = 3f;
    [Tooltip("Tổng thời gian nhảy (giây) — bao gồm bay lên + rơi xuống")]
    public float leapDuration = 0.6f;

    [Header("---- REFERENCES ----")]
    [SerializeField] private EnemyAnimation animationComp;
    public Transform attackPoint;
    public LayerMask playerLayer;
    [Tooltip("Tốc độ phát Animation đánh (1 là mặc định, 2 là nhanh gấp đôi)")]
    public float currentAttackSpeed = 1f;

    [Header("---- RANGED ATTACK ----")]
    public bool isRanged;
    public NetworkPrefabRef projectilePrefab;
    public Transform projectileSpawnPoint;

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME (ẩn khỏi Inspector)
    // ═══════════════════════════════════════════════════════════════
    [HideInInspector][Networked] public NetworkBool IsAttacking { get; set; }
    [HideInInspector][Networked] public NetworkBool IsDashAttacking { get; set; }
    [HideInInspector][Networked] public Vector2 DashDirection { get; set; }
    [HideInInspector][Networked] public NetworkBool IsLeapAttacking { get; set; }
    [HideInInspector][Networked] public Vector2 LeapTargetPos { get; set; }
    [HideInInspector][Networked] public Vector2 LeapStartPos { get; set; }
    [HideInInspector][Networked] public float LeapProgress { get; set; }
    [HideInInspector][Networked] public int CurrentAttackIndex { get; set; }

    [Networked] private TickTimer dashTimer { get; set; }
    [Networked] private TickTimer attackTimer { get; set; }
    [Networked] private TickTimer cooldownTimer { get; set; }

    [HideInInspector] public AttackStyle currentAttackStyle;

    // ═══════════════════════════════════════════════════════════════
    // CONVENIENCE PROPERTIES — Đọc từ AttackConfig
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Số đòn đánh = attacks.Length</summary>
    public int AttackVariants => attacks != null ? attacks.Length : 1;

    /// <summary>Phạm vi xa nhất trong tất cả đòn — dùng cho AI kiểm tra khoảng cách</summary>
    public float MaxAttackRange
    {
        get
        {
            float max = 0f;
            if (attacks != null)
            {
                for (int i = 0; i < attacks.Length; i++)
                    if (attacks[i].range > max) max = attacks[i].range;
            }
            return max > 0f ? max : 1.5f;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GETTERS — Đọc thông số từ AttackConfig an toàn
    // ═══════════════════════════════════════════════════════════════

    private AttackConfig GetConfig(int index)
    {
        if (attacks != null && index >= 0 && index < attacks.Length && attacks[index] != null)
            return attacks[index];
        // Fallback config mặc định
        return new AttackConfig();
    }

    public float GetAttackRange(int index) => GetConfig(index).range;
    public float GetAttackAngle(int index) => GetConfig(index).angle;
    public HitboxShape GetHitboxShape(int index) => GetConfig(index).hitboxShape;
    public Vector2 GetRectSize(int index) => GetConfig(index).rectSize;
    public int GetAttackDamage(int index) => GetConfig(index).damage;
    public float GetAttackDuration(int index) => GetConfig(index).duration;
    public float GetAttackCooldown(int index) => GetConfig(index).cooldown;

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
    }

    public override void FixedUpdateNetwork()
    {
        // Hết thời gian dash → tắt dash + rã đông animation
        if (IsDashAttacking && dashTimer.ExpiredOrNotRunning(Runner))
        {
            IsDashAttacking = false;
            dashTimer = TickTimer.None;
            RPC_UnfreezeAnim();
        }

        // Leap attack: cập nhật vị trí bay theo arc
        if (IsLeapAttacking)
        {
            LeapProgress += Runner.DeltaTime / (leapDuration > 0.01f ? leapDuration : 0.5f);
            if (LeapProgress >= 1f)
            {
                LeapProgress = 1f;
                IsLeapAttacking = false;
                RPC_UnfreezeAnim();
            }
        }

        // Hết attack timer → tắt attack
        if (IsAttacking && attackTimer.ExpiredOrNotRunning(Runner))
        {
            IsAttacking = false;
            IsDashAttacking = false;
            IsLeapAttacking = false;
            cooldownTimer = TickTimer.CreateFromSeconds(Runner, GetAttackCooldown(CurrentAttackIndex));
            attackTimer = TickTimer.None;
        }
    }

    public bool CanAttack()
    {
        return !IsAttacking && cooldownTimer.ExpiredOrNotRunning(Runner);
    }

    // ═══════════════════════════════════════════════════════════════
    // ATTACK METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Đánh thường — đứng yên tại chỗ.
    /// Chỉ random những đòn KHÔNG được gán cho dash/leap.
    /// </summary>
    public void AttemptAttack()
    {
        if (!CanAttack()) return;

        IsAttacking = true;
        currentAttackStyle = AttackStyle.Normal;
        int atkIdx = GetRandomNormalAttackIndex();
        CurrentAttackIndex = atkIdx;

        float duration = GetAttackDuration(atkIdx);
        RPC_PlayAttackAnim(atkIdx, currentAttackSpeed);
        attackTimer = TickTimer.CreateFromSeconds(Runner, duration / currentAttackSpeed);
    }

    /// <summary>
    /// Dash-slash — lao về phía player rồi chém.
    /// </summary>
    public void AttemptDashAttack(Vector2 directionToPlayer)
    {
        if (!CanAttack()) return;

        IsAttacking = true;
        currentAttackStyle = AttackStyle.DashSlash;
        int atkIdx = GetRandomAttackIndex(dashSlashAttacks);
        CurrentAttackIndex = atkIdx;

        float duration = GetAttackDuration(atkIdx);
        RPC_PlayAttackAnim(atkIdx, currentAttackSpeed);
        // Timer = thời gian lướt + thời gian animation đánh
        attackTimer = TickTimer.CreateFromSeconds(Runner, dashDuration + duration / currentAttackSpeed);

        // Bật dash
        IsDashAttacking = true;
        DashDirection = directionToPlayer.normalized;
        dashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
    }

    /// <summary>
    /// Leap attack — nhảy lên cao rồi rơi xuống vị trí player để đánh.
    /// </summary>
    public void AttemptLeapAttack(Vector2 targetPos)
    {
        if (!CanAttack()) return;

        IsAttacking = true;
        currentAttackStyle = AttackStyle.LeapAttack;
        int atkIdx = GetRandomAttackIndex(leapAttacks);
        CurrentAttackIndex = atkIdx;

        float duration = GetAttackDuration(atkIdx);
        RPC_PlayAttackAnim(atkIdx, currentAttackSpeed);
        // Timer = thời gian nhảy + thời gian animation đánh
        attackTimer = TickTimer.CreateFromSeconds(Runner, leapDuration + duration / currentAttackSpeed);

        // Bật leap
        IsLeapAttacking = true;
        LeapStartPos = transform.position;
        LeapTargetPos = targetPos;
        LeapProgress = 0f;
    }

    /// <summary>
    /// Tính vị trí hiện tại của quái khi đang leap (arc hình parabol).
    /// </summary>
    public Vector2 GetLeapPosition()
    {
        float t = Mathf.Clamp01(LeapProgress);
        Vector2 start = LeapStartPos;
        Vector2 end = LeapTargetPos;

        // Lerp ngang
        Vector2 pos = Vector2.Lerp(start, end, t);

        // Arc dọc (parabol): cao nhất ở giữa (t=0.5)
        float arc = 4f * leapHeight * t * (1f - t);
        pos.y += arc;

        return pos;
    }

    // Vẫn giữ lại cho ai muốn dùng Animation Event (không bắt buộc)
    public void FinishAttack()
    {
        if (!IsAttacking) return;
        IsAttacking = false;
        if (HasStateAuthority)
        {
            cooldownTimer = TickTimer.CreateFromSeconds(Runner, GetAttackCooldown(CurrentAttackIndex));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS — Random chọn đòn
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Random chọn attack index dựa trên mảng toggle.
    /// </summary>
    private int GetRandomAttackIndex(bool[] attackToggles)
    {
        List<int> options = new List<int>();
        if (attackToggles != null)
        {
            for (int i = 0; i < attackToggles.Length; i++)
            {
                if (attackToggles[i]) options.Add(i);
            }
        }
        if (options.Count == 0) options.Add(0); // fallback
        return options[Random.Range(0, options.Count)];
    }

    /// <summary>
    /// Lấy attack index cho đánh thường: loại bỏ những đòn đã gán cho dash/leap.
    /// </summary>
    private int GetRandomNormalAttackIndex()
    {
        HashSet<int> reserved = new HashSet<int>();
        if (enableDashSlash && dashSlashAttacks != null)
        {
            for (int i = 0; i < dashSlashAttacks.Length; i++)
                if (dashSlashAttacks[i]) reserved.Add(i);
        }
        if (enableLeapAttack && leapAttacks != null)
        {
            for (int i = 0; i < leapAttacks.Length; i++)
                if (leapAttacks[i]) reserved.Add(i);
        }

        List<int> available = new List<int>();
        for (int i = 0; i < AttackVariants; i++)
        {
            if (!reserved.Contains(i)) available.Add(i);
        }

        if (available.Count == 0) available.Add(0);
        return available[Random.Range(0, available.Count)];
    }

    /// <summary>
    /// Trả về danh sách attack styles đang bật để AI random chọn.
    /// </summary>
    public List<AttackStyle> GetEnabledAttackStyles()
    {
        List<AttackStyle> styles = new List<AttackStyle>();
        if (enableDashSlash) styles.Add(AttackStyle.DashSlash);
        if (enableLeapAttack) styles.Add(AttackStyle.LeapAttack);

        if (HasAvailableNormalAttacks())
        {
            styles.Add(AttackStyle.Normal);
        }

        if (styles.Count == 0) styles.Add(AttackStyle.Normal);
        return styles;
    }

    private bool HasAvailableNormalAttacks()
    {
        HashSet<int> reserved = new HashSet<int>();
        if (enableDashSlash && dashSlashAttacks != null)
        {
            for (int i = 0; i < dashSlashAttacks.Length; i++)
                if (dashSlashAttacks[i]) reserved.Add(i);
        }
        if (enableLeapAttack && leapAttacks != null)
        {
            for (int i = 0; i < leapAttacks.Length; i++)
                if (leapAttacks[i]) reserved.Add(i);
        }

        for (int i = 0; i < AttackVariants; i++)
        {
            if (!reserved.Contains(i)) return true;
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // RPC
    // ═══════════════════════════════════════════════════════════════

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnim(int attackIndex, float speed)
    {
        if (animationComp != null) animationComp.PlayAttack(attackIndex, speed);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UnfreezeAnim()
    {
        if (animationComp != null) animationComp.UnfreezeAnimation();
    }

    /// <summary>
    /// Lấy hướng nhìn thực tế, có tính đến defaultFacingLeft của EnemyAnimation.
    /// </summary>
    private Vector2 GetFacingDirection()
    {
        bool defaultLeft = animationComp != null && animationComp.defaultFacingLeft;
        float scaleX = transform.localScale.x;

        if (defaultLeft)
            return scaleX > 0 ? Vector2.left : Vector2.right;
        else
            return scaleX > 0 ? Vector2.right : Vector2.left;
    }

    // ═══════════════════════════════════════════════════════════════
    // GIZMOS — Vẽ hitbox cho từng đòn
    // ═══════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null || attacks == null) return;

        // Lấy hướng nhìn có tính defaultFacingLeft
        EnemyAnimation anim = animationComp != null ? animationComp : GetComponent<EnemyAnimation>();
        bool defaultLeft = anim != null && anim.defaultFacingLeft;
        float scaleX = transform.localScale.x;
        Vector3 facingDirection;
        if (defaultLeft)
            facingDirection = scaleX > 0 ? Vector3.left : Vector3.right;
        else
            facingDirection = scaleX > 0 ? Vector3.right : Vector3.left;

        Color[] atkColors = { Color.yellow, Color.cyan, Color.green, Color.magenta, new Color(1f, 0.5f, 0f) };

        for (int i = 0; i < attacks.Length && i < 5; i++)
        {
            AttackConfig cfg = attacks[i];
            if (cfg == null) continue;

            Color col = atkColors[i % atkColors.Length];

            switch (cfg.hitboxShape)
            {
                case HitboxShape.Cone:
                    Gizmos.color = new Color(col.r, col.g, col.b, 0.4f);
                    Gizmos.DrawWireSphere(attackPoint.position, cfg.range);
                    Gizmos.color = new Color(col.r, col.g, col.b, 0.7f);
                    Vector3 upper = Quaternion.Euler(0, 0, cfg.angle / 2f) * facingDirection;
                    Vector3 lower = Quaternion.Euler(0, 0, -cfg.angle / 2f) * facingDirection;
                    Gizmos.DrawRay(attackPoint.position, upper * cfg.range);
                    Gizmos.DrawRay(attackPoint.position, lower * cfg.range);
                    break;

                case HitboxShape.Circle:
                    Gizmos.color = new Color(col.r, col.g, col.b, 0.5f);
                    Gizmos.DrawWireSphere(attackPoint.position, cfg.range);
                    Gizmos.color = new Color(col.r, col.g, col.b, 0.1f);
                    Gizmos.DrawSphere(attackPoint.position, cfg.range);
                    break;

                case HitboxShape.Rectangle:
                    float facingAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(attackPoint.position, Quaternion.Euler(0, 0, facingAngle), Vector3.one);
                    Gizmos.color = new Color(col.r, col.g, col.b, 0.5f);
                    Gizmos.DrawWireCube(new Vector3(cfg.rectSize.x / 2f, 0, 0), new Vector3(cfg.rectSize.x, cfg.rectSize.y, 0));
                    Gizmos.color = new Color(col.r, col.g, col.b, 0.08f);
                    Gizmos.DrawCube(new Vector3(cfg.rectSize.x / 2f, 0, 0), new Vector3(cfg.rectSize.x, cfg.rectSize.y, 0));
                    Gizmos.matrix = oldMatrix;
                    break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DAMAGE — Gây sát thương
    // ═══════════════════════════════════════════════════════════════

    public void TriggerAttackDamage()
    {
        if (attackPoint == null) return;

        AttackConfig cfg = GetConfig(CurrentAttackIndex);

        if (isRanged)
        {
            if (projectileSpawnPoint == null || !projectilePrefab.IsValid)
            {
                Debug.LogWarning("Chưa gắn Projectile Prefab hoặc Spawn Point cho " + gameObject.name);
                return;
            }
            
            if (!HasStateAuthority) return;

            Vector2 shootDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            Collider2D[] rangeResults = new Collider2D[5];
            ContactFilter2D rangeFilter = new ContactFilter2D();
            rangeFilter.useLayerMask = true;
            rangeFilter.layerMask = playerLayer;
            rangeFilter.useTriggers = false;
            
            int pCount = Runner.GetPhysicsScene2D().OverlapCircle(transform.position, cfg.range * 1.5f, rangeFilter, rangeResults);
            float closestDist = float.MaxValue;
            Transform targetPlayer = null;
            
            for (int i = 0; i < pCount; i++)
            {
                float dist = Vector2.Distance(transform.position, rangeResults[i].transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetPlayer = rangeResults[i].transform;
                }
            }

            if (targetPlayer != null)
            {
                shootDir = (targetPlayer.position - projectileSpawnPoint.position).normalized;
            }

            int dmg = cfg.damage;
            Runner.Spawn(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity, null, (runner, obj) =>
            {
                EnemyProjectile proj = obj.GetComponent<EnemyProjectile>();
                if (proj != null) proj.Init(shootDir, dmg, 8f);
            });
            return;
        }

        // ═══ MELEE ATTACK — Hỗ trợ nhiều hình dạng hitbox ═══
        Vector2 facingDir2D = GetFacingDirection();

        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = playerLayer;
        filter.useTriggers = false;
        int count = 0;

        switch (cfg.hitboxShape)
        {
            case HitboxShape.Circle:
                count = Runner.GetPhysicsScene2D().OverlapCircle(attackPoint.position, cfg.range, filter, results);
                for (int i = 0; i < count; i++)
                {
                    DealMeleeDamage(results[i], cfg.damage);
                }
                break;

            case HitboxShape.Rectangle:
                float facingAngleRad = Mathf.Atan2(facingDir2D.y, facingDir2D.x);
                Vector2 boxCenter = (Vector2)attackPoint.position + facingDir2D * (cfg.rectSize.x / 2f);
                count = Runner.GetPhysicsScene2D().OverlapBox(boxCenter, cfg.rectSize, facingAngleRad * Mathf.Rad2Deg, filter, results);
                for (int i = 0; i < count; i++)
                {
                    DealMeleeDamage(results[i], cfg.damage);
                }
                break;

            case HitboxShape.Cone:
            default:
                count = Runner.GetPhysicsScene2D().OverlapCircle(attackPoint.position, cfg.range, filter, results);
                for (int i = 0; i < count; i++)
                {
                    Collider2D player = results[i];
                    Vector2 directionToPlayer = (player.transform.position - transform.position).normalized;

                    if (Vector2.Angle(facingDir2D, directionToPlayer) < cfg.angle / 2f)
                    {
                        DealMeleeDamage(player, cfg.damage);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Gây sát thương cận chiến cho 1 target.
    /// </summary>
    private void DealMeleeDamage(Collider2D player, int damage)
    {
        Vector2 directionToPlayer = (player.transform.position - transform.position).normalized;
        IDamageable dmg = player.GetComponentInParent<IDamageable>();
        if (dmg != null && !dmg.IsDead)
        {
            Vector2 pushDir = new Vector2(directionToPlayer.x, 0.5f).normalized;
            dmg.TakeDamage(damage, pushDir, 0f); // force=0 → Player sẽ dùng knockbackForceOverride
        }
    }

    /// <summary>
    /// Ngắt toàn bộ hành động tấn công hiện tại (gọi khi bị stun).
    /// </summary>
    public void CancelAllActions()
    {
        IsAttacking = false;
        IsDashAttacking = false;
        IsLeapAttacking = false;
        dashTimer = TickTimer.None;
        attackTimer = TickTimer.None;
        // Reset animation speed nếu đang bị freeze
        if (animationComp != null) animationComp.UnfreezeAnimation();
    }
}