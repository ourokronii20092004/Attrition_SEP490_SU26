using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class EnemyCombat : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    // ATTACK STYLES — Tick chọn kiểu tấn công
    // ═══════════════════════════════════════════════════════════════
    [Header("---- DASH-SLASH (Lướt đến chém) ----")]
    [Tooltip("Bật nếu quái lao về phía Player khi tấn công")]
    public bool enableDashSlash;
    [Tooltip("Tick đòn attack nào được dùng khi lướt (size = attackVariants, index 0 = Attack1, 1 = Attack2, ...)")]
    public bool[] dashSlashAttacks = new bool[] { true };
    [Tooltip("Tốc độ lao tới khi dash")]
    public float dashSpeed = 10f;
    [Tooltip("Thời gian lao tới (giây) - nên ngắn hơn attackDuration")]
    public float dashDuration = 0.3f;

    [Header("---- LEAP ATTACK (Nhảy đánh xuống) ----")]
    [Tooltip("Bật nếu quái nhảy lên rồi đánh xuống vị trí Player")]
    public bool enableLeapAttack;
    [Tooltip("Tick đòn attack nào được dùng khi nhảy (size = attackVariants, index 0 = Attack1, 1 = Attack2, ...)")]
    public bool[] leapAttacks = new bool[] { true };
    [Tooltip("Chiều cao nhảy lên (units)")]
    public float leapHeight = 3f;
    [Tooltip("Tổng thời gian nhảy (giây) — bao gồm bay lên + rơi xuống")]
    public float leapDuration = 0.6f;

    [Header("---- DAMAGE & SPEED ----")]
    [SerializeField] private EnemyAnimation animationComp;
    public Transform attackPoint;
    [Tooltip("Phạm vi tấn công mặc định (dùng khi attackRanges chưa set)")]
    public float attackRange = 1.5f;
    [Tooltip("Phạm vi tấn công cho từng đòn (index 0 = Attack1, 1 = Attack2, ...). Để trống thì dùng attackRange mặc định.")]
    public float[] attackRanges;
    [Range(0, 360)] public float attackAngle = 90f;
    public LayerMask playerLayer;
    public int attackDamage = 1;
    [Tooltip("Số kiểu đòn đánh (1 = chỉ có Attack1, 2 = có Attack1 và Attack2)")]
    public int attackVariants = 1;
    [Tooltip("Thời gian đứng yên khi đánh - nên bằng độ dài animation attack (giây)")]
    public float attackDuration = 0.6f;
    [Tooltip("Thời gian nghỉ giữa 2 đòn đánh (Cooldown - Tính bằng giây)")]
    public float attackCooldown = 1.0f;
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

    /// <summary>
    /// Kiểu tấn công hiện tại đã chọn (để AI biết phải xử lý kiểu nào).
    /// </summary>
    public enum AttackStyle { Normal, DashSlash, LeapAttack }
    [HideInInspector] public AttackStyle currentAttackStyle;

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
            cooldownTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
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

        RPC_PlayAttackAnim(atkIdx, currentAttackSpeed);
        attackTimer = TickTimer.CreateFromSeconds(Runner, attackDuration / currentAttackSpeed);
    }

    /// <summary>
    /// Dash-slash — lao về phía player rồi chém.
    /// Animation chạy bình thường, dùng Animation Event (FreezeAnimation) để giữ frame.
    /// Khi lướt xong, code gọi UnfreezeAnimation để tiếp tục.
    /// </summary>
    public void AttemptDashAttack(Vector2 directionToPlayer)
    {
        if (!CanAttack()) return;

        IsAttacking = true;
        currentAttackStyle = AttackStyle.DashSlash;
        int atkIdx = GetRandomAttackIndex(dashSlashAttacks);
        CurrentAttackIndex = atkIdx;

        RPC_PlayAttackAnim(atkIdx, currentAttackSpeed);
        // Timer = thời gian lướt + thời gian animation đánh
        attackTimer = TickTimer.CreateFromSeconds(Runner, dashDuration + attackDuration / currentAttackSpeed);

        // Bật dash
        IsDashAttacking = true;
        DashDirection = directionToPlayer.normalized;
        dashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
    }

    /// <summary>
    /// Leap attack — nhảy lên cao rồi rơi xuống vị trí player để đánh.
    /// Animation chạy bình thường, dùng Animation Event (FreezeAnimation) để giữ frame.
    /// Khi nhảy xong, code gọi UnfreezeAnimation để tiếp tục.
    /// </summary>
    public void AttemptLeapAttack(Vector2 targetPos)
    {
        if (!CanAttack()) return;

        IsAttacking = true;
        currentAttackStyle = AttackStyle.LeapAttack;
        int atkIdx = GetRandomAttackIndex(leapAttacks);
        CurrentAttackIndex = atkIdx;

        RPC_PlayAttackAnim(atkIdx, currentAttackSpeed);
        // Timer = thời gian nhảy + thời gian animation đánh
        attackTimer = TickTimer.CreateFromSeconds(Runner, leapDuration + attackDuration / currentAttackSpeed);

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
            cooldownTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Random chọn attack index dựa trên mảng toggle.
    /// Mảng index 0 = Attack1, 1 = Attack2, 2 = Attack3, ...
    /// Nếu không tick cái nào → mặc định 0 (Attack1).
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
        // Thu thập các index đã bị dùng bởi dash/leap
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

        // Chỉ lấy những đòn chưa bị reserve
        List<int> available = new List<int>();
        for (int i = 0; i < attackVariants; i++)
        {
            if (!reserved.Contains(i)) available.Add(i);
        }

        // Nếu tất cả đòn đều đã gán cho dash/leap → fallback dùng đòn 0
        if (available.Count == 0) available.Add(0);
        return available[Random.Range(0, available.Count)];
    }

    /// <summary>
    /// Lấy phạm vi tấn công của đòn chỉ định. Fallback về attackRange nếu chưa set.
    /// </summary>
    public float GetAttackRange(int index)
    {
        if (attackRanges != null && index >= 0 && index < attackRanges.Length && attackRanges[index] > 0f)
            return attackRanges[index];
        return attackRange;
    }

    /// <summary>
    /// Trả về danh sách attack styles đang bật để AI random chọn.
    /// Normal luôn có trong danh sách.
    /// </summary>
    public List<AttackStyle> GetEnabledAttackStyles()
    {
        List<AttackStyle> styles = new List<AttackStyle>();
        styles.Add(AttackStyle.Normal);
        if (enableDashSlash) styles.Add(AttackStyle.DashSlash);
        if (enableLeapAttack) styles.Add(AttackStyle.LeapAttack);
        return styles;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnim(int attackIndex, float speed)
    {
        if (animationComp != null) animationComp.PlayAttack(attackIndex, speed);
    }

    /// <summary>
    /// Rã đông animation sau khi lướt/nhảy xong.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UnfreezeAnim()
    {
        if (animationComp != null) animationComp.UnfreezeAnimation();
    }

    /// <summary>
    /// Lấy hướng nhìn thực tế, có tính đến defaultFacingLeft của EnemyAnimation.
    /// Khi sprite gốc quay trái, scale.x > 0 nghĩa là đang nhìn trái (ngược lại bình thường).
    /// </summary>
    private Vector2 GetFacingDirection()
    {
        bool defaultLeft = animationComp != null && animationComp.defaultFacingLeft;
        float scaleX = transform.localScale.x;

        // defaultFacingLeft = false (gốc quay phải): scale.x > 0 → phải, scale.x < 0 → trái
        // defaultFacingLeft = true  (gốc quay trái): scale.x > 0 → trái, scale.x < 0 → phải
        if (defaultLeft)
            return scaleX > 0 ? Vector2.left : Vector2.right;
        else
            return scaleX > 0 ? Vector2.right : Vector2.left;
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);

        // Vẽ thêm các vòng tròn cho từng attack variant (nếu có)
        if (attackRanges != null)
        {
            Color[] rangeColors = { Color.cyan, Color.green, Color.magenta, new Color(1f, 0.5f, 0f), Color.blue };
            for (int i = 0; i < attackRanges.Length && i < 5; i++)
            {
                if (attackRanges[i] > 0f)
                {
                    Gizmos.color = new Color(rangeColors[i].r, rangeColors[i].g, rangeColors[i].b, 0.4f);
                    Gizmos.DrawWireSphere(attackPoint.position, attackRanges[i]);
                }
            }
        }

        // Lấy hướng nhìn có tính defaultFacingLeft
        EnemyAnimation anim = animationComp != null ? animationComp : GetComponent<EnemyAnimation>();
        bool defaultLeft = anim != null && anim.defaultFacingLeft;
        float scaleX = transform.localScale.x;
        Vector3 facingDirection;
        if (defaultLeft)
            facingDirection = scaleX > 0 ? Vector3.left : Vector3.right;
        else
            facingDirection = scaleX > 0 ? Vector3.right : Vector3.left;

        Vector3 upperLimit = Quaternion.Euler(0, 0, attackAngle / 2f) * facingDirection;
        Vector3 lowerLimit = Quaternion.Euler(0, 0, -attackAngle / 2f) * facingDirection;

        Gizmos.color = new Color(1, 0.92f, 0.016f, 0.7f);
        Gizmos.DrawRay(transform.position, upperLimit * attackRange);
        Gizmos.DrawRay(transform.position, lowerLimit * attackRange);
    }

    public void TriggerAttackDamage()
    {
        if (attackPoint == null) return;

        if (isRanged)
        {
            if (projectileSpawnPoint == null || !projectilePrefab.IsValid)
            {
                Debug.LogWarning("Chưa gắn Projectile Prefab hoặc Spawn Point cho " + gameObject.name);
                return;
            }
            
            if (!HasStateAuthority) return; // Chỉ có Host mới được tạo đạn để đồng bộ cho các máy khác

            // Tìm người chơi gần nhất trong tầm để bắn chính xác (đặc biệt khi quái bay cần bắn chéo xuống)
            Vector2 shootDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            Collider2D[] rangeResults = new Collider2D[5];
            ContactFilter2D rangeFilter = new ContactFilter2D();
            rangeFilter.useLayerMask = true;
            rangeFilter.layerMask = playerLayer;
            rangeFilter.useTriggers = false;
            
            int pCount = Runner.GetPhysicsScene2D().OverlapCircle(transform.position, GetAttackRange(CurrentAttackIndex) * 1.5f, rangeFilter, rangeResults);
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

            Runner.Spawn(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity, null, (runner, obj) =>
            {
                EnemyProjectile proj = obj.GetComponent<EnemyProjectile>();
                if (proj != null) proj.Init(shootDir, attackDamage, 8f);
            });
            return;
        }

        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = playerLayer;
        filter.useTriggers = false;

        float currentRange = GetAttackRange(CurrentAttackIndex);
        int count = Runner.GetPhysicsScene2D().OverlapCircle(attackPoint.position, currentRange, filter, results);

        for (int i = 0; i < count; i++)
        {
            Collider2D player = results[i];

            Vector2 directionToPlayer = (player.transform.position - transform.position).normalized;
            Vector2 facingDirection = GetFacingDirection();

            if (Vector2.Angle(facingDirection, directionToPlayer) < attackAngle / 2f)
            {
                IDamageable dmg = player.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead)
                {
                    Vector2 pushDir = new Vector2(directionToPlayer.x, 0.5f).normalized;
                    dmg.TakeDamage(attackDamage, pushDir, 0f); // force=0 → Player sẽ dùng knockbackForceOverride
                }
            }
        }
    }
}