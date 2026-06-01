using Attrition.Controllers;
using Fusion;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════
// ENEMY STATE — Finite State Machine
// Chỉ MỘT state active tại mỗi thời điểm.
// Hướng nhìn chỉ thay đổi ở state được phép (Patrol, Chase).
// ═══════════════════════════════════════════════════════════════
public enum EnemyState : byte
{
    Patrol,             // Tuần tra ngẫu nhiên quanh spawn point
    Chase,              // Đuổi theo Player
    Attacking,          // Đang thực hiện đòn tấn công (facing LOCKED)
    Recovery,           // Hồi phục sau đòn đánh (facing LOCKED)
    Sleeping,           // Ngủ tại chỗ, chờ Player đến gần
    WakingUp,           // Đang chơi animation thức dậy (facing LOCKED)
    ReturningToSleep,   // Bay/đi về vị trí ngủ
    RetreatingUp,       // Bay lên cao sau khi đánh (Fly Melee)
    UsingSkill,         // Đang dùng skill đặc biệt (facing LOCKED)
    Summoning           // Đang triệu hồi quái phụ (facing LOCKED)
}

public class EnemyAI : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ═══════════════════════════════════════════════════════════════

    [Header("---- REFS ----")]
    [SerializeField] private EnemyAnimation animationComp;
    [SerializeField] private EnemyCombat combatComp;
    [SerializeField] private EnemyController controller;
    [Tooltip("Gắn EliteEnemySkills nếu đây là quái tinh anh (Cultist, NightBorne, Gollux). Bỏ trống nếu quái thường.")]
    [SerializeField] private EliteEnemySkills eliteSkills;
    private Rigidbody2D rb;

    [Header("---- SETTINGS ----")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5f;
    public float viewRadius = 5f;
    [Tooltip("Tuần tra ngẫu nhiên theo trục X quanh điểm spawn.")]
    public float patrolRadius = 3f;
    [Tooltip("Đánh dấu nếu quái là loại bay (di chuyển cả trục Y khi đuổi)")]
    public bool isFlying = false;

    [Header("---- FLY MELEE (Bat swoop) ----")]
    [Tooltip("Bật nếu quái bay cận chiến: tấn công xong bay lên cao rồi lao xuống đánh tiếp. Không ảnh hưởng quái bay bắn xa.")]
    public bool flyMeleeRetreat = false;
    [Tooltip("Độ cao bay lên so với vị trí spawn sau khi tấn công xong (units)")]
    public float flyMeleeRetreatAltitude = 3f;
    [Tooltip("Tốc độ bay lên vị trí cao sau khi tấn công")]
    public float flyMeleeRetreatSpeed = 8f;

    [Header("---- SLEEP / WAKEUP (Bat / Mimic) ----")]
    [Tooltip("Bật nếu quái ngủ tại chỗ spawn và thức dậy khi Player đến gần")]
    public bool enableSleep = false;
    [Tooltip("Bán kính vòng thức dậy: Player vào vùng này → quái tỉnh. Nếu = 0, dùng viewRadius để phát hiện.")]
    public float wakeUpRadius = 0f;
    [Tooltip("Thời gian chờ sau khi Player rời tầm nhìn mới bay về ngủ lại (giây)")]
    public float sleepReturnDelay = 3f;
    [Tooltip("Tốc độ bay về vị trí ngủ")]
    public float returnToSleepSpeed = 6f;
    [Tooltip("Bật = ngủ trên trần (tìm mặt phẳng phía trên). Tắt = ngủ tại vị trí spawn ban đầu")]
    public bool sleepOnCeiling = true;
    [Tooltip("Layer dùng để tìm trần/sàn khi quay về ngủ")]
    public LayerMask sleepSurfaceLayer;

    [Header("---- OBSTACLE DETECTION ----")]
    public LayerMask obstacleLayer;
    [Tooltip("Độ dài tia laser quét tường phía trước")]
    public float wallCheckDistance = 0.8f;
    [Tooltip("Độ cao của tia laser so với mặt đất (dời lên để không quét trúng sàn nhà)")]
    public float wallCheckHeightOffset = 0.5f;

    [Header("---- FACING ----")]
    [Tooltip("Dead zone: không đổi hướng nhìn khi khoảng cách X với mục tiêu nhỏ hơn giá trị này (tránh giật/nhấp nháy)")]
    public float facingDeadZone = 0.3f;

    // ═══════════════════════════════════════════════════════════════
    // NETWORKED STATE — Single source of truth
    // ═══════════════════════════════════════════════════════════════

    [HideInInspector][Networked] public EnemyState CurrentState { get; set; }
    [HideInInspector][Networked] public float NetSpeed { get; set; }
    [HideInInspector][Networked] public float NetFacingDir { get; set; } = 1f;

    /// <summary>
    /// Hướng nhìn đã KHÓA khi bắt đầu tấn công.
    /// Giữ nguyên suốt Attacking + Recovery state.
    /// </summary>
    [HideInInspector][Networked] public float AttackLockedFacingDir { get; set; } = 1f;

    [Networked] private TickTimer recoveryTimer { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // BACKWARD COMPATIBILITY — Cho MimicSleepTrigger và các script cũ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Property tương thích ngược. MimicSleepTrigger dùng property này.
    /// Đọc từ CurrentState thay vì boolean riêng.
    /// </summary>
    public bool IsSleeping => CurrentState == EnemyState.Sleeping;

    // ═══════════════════════════════════════════════════════════════
    // LOCAL STATE (không cần đồng bộ mạng)
    // ═══════════════════════════════════════════════════════════════

    private Vector2 startPosition;
    private Vector2 sleepPosition;
    private Vector2 currentTarget;
    private Transform playerTarget;
    private PlayerRef cachedChasePlayer;

    // Sleep timers
    private float noPlayerTimer;
    private float wakeUpAnimTimer;

    // Render-side animation state (tránh gọi anim lặp)
    private bool localSleepHandled;
    private bool localWakeHandled;

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        if (combatComp == null) combatComp = GetComponent<EnemyCombat>();
        if (controller == null) controller = GetComponent<EnemyController>();
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
        startPosition = transform.position;

        // Tính vị trí ngủ
        if (enableSleep)
        {
            sleepPosition = FindSleepPosition();
            if (HasStateAuthority)
            {
                CurrentState = EnemyState.Sleeping;
            }
            noPlayerTimer = 0f;
            wakeUpAnimTimer = 0f;
            localSleepHandled = false;
            localWakeHandled = false;
        }
        else
        {
            sleepPosition = startPosition;
            if (HasStateAuthority)
            {
                CurrentState = EnemyState.Patrol;
            }
        }

        // Ép điểm tuần tra đầu tiên cách xa điểm spawn để quái di chuyển ngay lập tức
        float randomDir = Random.value > 0.5f ? 1f : -1f;
        float randomDist = Random.Range(1f, patrolRadius);
        currentTarget = new Vector2(startPosition.x + randomDir * randomDist, startPosition.y);

        cachedChasePlayer = default;
        playerTarget = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // RENDER — Animation & Facing (chạy trên TẤT CẢ clients)
    // ═══════════════════════════════════════════════════════════════

    public override void Render()
    {
        if (controller == null) return;

        if (controller.isDeadNetworked || controller.IsAwaitingRevive)
        {
            animationComp.UpdateSpeed(0f);
            return;
        }

        // ─── Sleep/WakeUp animation ───
        if (enableSleep)
        {
            bool isSleepingNow = CurrentState == EnemyState.Sleeping;
            if (isSleepingNow && !localSleepHandled)
            {
                animationComp.PlaySleep();
                localSleepHandled = true;
                localWakeHandled = false;
            }
            else if (!isSleepingNow && !localWakeHandled && localSleepHandled)
            {
                animationComp.PlayWakeUp();
                localWakeHandled = true;
                localSleepHandled = false;
            }
        }

        animationComp.UpdateSpeed(NetSpeed);

        // ─── Facing direction ───
        // State-based lock: Attacking, Recovery, WakingUp, UsingSkill, Summoning → dùng AttackLockedFacingDir
        // Elite override: Healing, Teleporting → giữ nguyên NetFacingDir (không thay đổi)
        // Tất cả state khác → dùng NetFacingDir
        bool facingLocked = CurrentState == EnemyState.Attacking
                         || CurrentState == EnemyState.Recovery
                         || CurrentState == EnemyState.WakingUp
                         || CurrentState == EnemyState.UsingSkill
                         || CurrentState == EnemyState.Summoning;

        animationComp.FaceDirection(facingLocked ? AttackLockedFacingDir : NetFacingDir);
    }

    // ═══════════════════════════════════════════════════════════════
    // AI LOGIC — State Machine (chỉ chạy trên Host/StateAuthority)
    // ═══════════════════════════════════════════════════════════════

    public void RunAILogic()
    {
        // ─── KNOCKBACK OVERRIDE (ưu tiên cao nhất) ───
        if (controller.IsKnockbackActive)
        {
            HandleKnockbackOverride();
            return;
        }

        // ─── ELITE SKILL OVERRIDES ───
        if (eliteSkills != null && eliteSkills.IsHealing)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
            eliteSkills.UpdateHealing();
            return;
        }

        if (eliteSkills != null && eliteSkills.IsTeleporting)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
            eliteSkills.UpdateTeleport();
            return;
        }

        if (eliteSkills != null && eliteSkills.IsUsingSkill)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
            eliteSkills.UpdateSkill();
            if (!eliteSkills.IsUsingSkill)
            {
                // Skill xong → quay lại chase
                CurrentState = EnemyState.Chase;
            }
            return;
        }

        if (eliteSkills != null && eliteSkills.IsSummoning)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
            eliteSkills.UpdateSummon();
            if (!eliteSkills.IsSummoning)
            {
                // Summon xong → quay lại chase
                CurrentState = EnemyState.Chase;
            }
            return;
        }

        // ─── STATE MACHINE ───
        switch (CurrentState)
        {
            case EnemyState.Sleeping:         StateSleeping();         break;
            case EnemyState.WakingUp:         StateWakingUp();         break;
            case EnemyState.ReturningToSleep: StateReturningToSleep(); break;
            case EnemyState.Patrol:           StatePatrol();           break;
            case EnemyState.Chase:            StateChase();            break;
            case EnemyState.Attacking:        StateAttacking();        break;
            case EnemyState.Recovery:         StateRecovery();         break;
            case EnemyState.RetreatingUp:     StateRetreatingUp();     break;
            case EnemyState.UsingSkill:       /* handled by override above */ break;
            case EnemyState.Summoning:        /* handled by override above */ break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // STATE HANDLERS
    // ═══════════════════════════════════════════════════════════════

    // ─────────────────────── KNOCKBACK ───────────────────────

    private void HandleKnockbackOverride()
    {
        // Bị knockback → thoát khỏi mọi state đặc biệt
        switch (CurrentState)
        {
            case EnemyState.Sleeping:
                // Bị đánh khi đang ngủ → tỉnh ngay
                CurrentState = EnemyState.Chase;
                noPlayerTimer = 0f;
                break;

            case EnemyState.Attacking:
            case EnemyState.Recovery:
            case EnemyState.RetreatingUp:
                // Bị đánh khi đang tấn công/hồi phục/bay lên → hủy, chuyển chase
                CurrentState = EnemyState.Chase;
                break;

            case EnemyState.WakingUp:
                // Bị đánh khi đang thức dậy → tỉnh ngay
                CurrentState = EnemyState.Chase;
                noPlayerTimer = 0f;
                break;
        }

        NetSpeed = Mathf.Abs(rb.linearVelocity.x);
    }

    // ─────────────────────── SLEEPING ───────────────────────

    private void StateSleeping()
    {
        rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
        NetSpeed = 0f;

        // Kiểm tra player trong vòng thức dậy
        float effectiveWakeRadius = wakeUpRadius > 0f ? wakeUpRadius : viewRadius;
        if (CheckPlayerInWakeRadius(effectiveWakeRadius))
        {
            CurrentState = EnemyState.WakingUp;
            wakeUpAnimTimer = 0f;
        }
    }

    // ─────────────────────── WAKING UP ───────────────────────

    private void StateWakingUp()
    {
        rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
        NetSpeed = 0f;

        wakeUpAnimTimer += Runner.DeltaTime;
        if (wakeUpAnimTimer < 0.4f) return;

        // Animation thức dậy xong → tìm player và quyết định state
        cachedChasePlayer = default;
        playerTarget = null;
        FindPlayer();

        if (playerTarget != null)
        {
            // Khóa hướng nhìn về phía player khi vừa tỉnh
            float xDiff = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > facingDeadZone)
            {
                float dir = xDiff > 0 ? 1f : -1f;
                NetFacingDir = dir;
                AttackLockedFacingDir = dir;
            }
            CurrentState = EnemyState.Chase;
        }
        else
        {
            CurrentState = EnemyState.Patrol;
        }
        noPlayerTimer = 0f;
    }

    // ─────────────────────── RETURNING TO SLEEP ───────────────────────

    private void StateReturningToSleep()
    {
        float distToSleep = Vector2.Distance(transform.position, sleepPosition);

        if (distToSleep < 0.3f)
        {
            // Đã về đến nơi → ngủ
            transform.position = new Vector3(sleepPosition.x, sleepPosition.y, transform.position.z);
            rb.linearVelocity = Vector2.zero;
            CurrentState = EnemyState.Sleeping;
            cachedChasePlayer = default;
            playerTarget = null;
            NetSpeed = 0f;
            return;
        }

        // Bay về vị trí ngủ
        Vector2 dir = (sleepPosition - (Vector2)transform.position).normalized;
        rb.linearVelocity = dir * returnToSleepSpeed;
        UpdateFacing(dir.x);
        NetSpeed = returnToSleepSpeed;

        // Nếu bất ngờ thấy player khi đang bay về → tỉnh dậy đuổi
        FindPlayer();
        if (playerTarget != null)
        {
            CurrentState = EnemyState.Chase;
            noPlayerTimer = 0f;
        }
    }

    // ─────────────────────── PATROL ───────────────────────

    private void StatePatrol()
    {
        // Sleep check: đếm thời gian không thấy player
        if (enableSleep)
        {
            noPlayerTimer += Runner.DeltaTime;
            if (noPlayerTimer >= sleepReturnDelay)
            {
                CurrentState = EnemyState.ReturningToSleep;
                noPlayerTimer = 0f;
                return;
            }
        }

        // Tìm player
        FindPlayer();
        if (playerTarget != null)
        {
            CurrentState = EnemyState.Chase;
            noPlayerTimer = 0f;
            return;
        }

        // Sleep enemies không patrol, đứng yên chờ timer
        if (enableSleep)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
        }
        else
        {
            DoPatrolMovement();
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
        }

        // Elite: roll heal và summon ngẫu nhiên khi patrol
        if (eliteSkills != null)
        {
            eliteSkills.TryRandomHeal(controller.CurrentHealth, controller.maxHealth);

            // Roll summon khi patrol
            if (eliteSkills.TryUseSummon())
            {
                float facingDir = NetFacingDir > 0 ? 1f : -1f;
                AttackLockedFacingDir = facingDir;
                CurrentState = EnemyState.Summoning;
            }
        }
    }

    // ─────────────────────── CHASE ───────────────────────

    private void StateChase()
    {
        FindPlayer();

        if (playerTarget == null)
        {
            // Mất target → chuyển patrol
            currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);
            CurrentState = EnemyState.Patrol;
            noPlayerTimer = 0f;
            NetSpeed = 0f;
            return;
        }

        // Đang thấy player → reset sleep timer
        noPlayerTimer = 0f;

        currentTarget = playerTarget.position;
        float dist = Vector2.Distance(transform.position, currentTarget);
        float xDiff = currentTarget.x - transform.position.x;

        // Kiểm tra tầm nhìn tới Player (không bị tường che)
        bool hasLineOfSight = !Physics2D.Linecast(transform.position, playerTarget.position, obstacleLayer);

        if (dist <= combatComp.MaxAttackRange && hasLineOfSight)
        {
            // ═══ TRONG TẦM ĐÁNH VÀ CÓ TẦM NHÌN → dừng lại ═══
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            UpdateFacing(xDiff);
            NetSpeed = 0f;

            if (combatComp.CanAttack())
            {
                // Thử dùng Skill trước (nếu có Elite Skill)
                if (eliteSkills != null && eliteSkills.TryUseSkill())
                {
                    // Khoá hướng nhìn về phía player khi dùng skill
                    float facingDir = Mathf.Abs(xDiff) < facingDeadZone ? (NetFacingDir > 0 ? 1f : -1f) : (xDiff > 0 ? 1f : -1f);
                    AttackLockedFacingDir = facingDir;
                    NetFacingDir = facingDir;
                    CurrentState = EnemyState.UsingSkill;
                }
                else
                {
                    // Không dùng Skill → Attack thường
                    TransitionToAttacking(xDiff);
                }
            }
            else if (eliteSkills != null)
            {
                // Chờ cooldown → thử teleport
                eliteSkills.TryTeleport(dist, playerTarget);
            }
        }
        else
        {
            // ═══ NGOÀI TẦM ĐÁNH → đuổi theo ═══
            if (eliteSkills != null && eliteSkills.TryTeleport(dist, playerTarget))
            {
                // Đã bắt đầu teleport → RunAILogic sẽ xử lý ở elite override
            }
            else if (!isFlying && IsPathBlocked(xDiff > 0 ? 1f : -1f))
            {
                // Bị chặn bởi tường
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                UpdateFacing(xDiff);
                NetSpeed = 0f;
            }
            else
            {
                MoveTowards(currentTarget, chaseSpeed);
                NetSpeed = Mathf.Abs(rb.linearVelocity.x);
            }
        }

        // Elite: roll heal và summon ngẫu nhiên khi đang chase nhưng ngoài tầm đánh
        if (eliteSkills != null && !combatComp.IsAttacking && (dist > combatComp.MaxAttackRange || !hasLineOfSight))
        {
            eliteSkills.TryRandomHeal(controller.CurrentHealth, controller.maxHealth);

            // Roll summon (yêu cầu có tầm nhìn)
            if (hasLineOfSight && eliteSkills.TryUseSummon())
            {
                float facingDir2 = playerTarget != null ? (playerTarget.position.x - transform.position.x > 0 ? 1f : -1f) : NetFacingDir;
                AttackLockedFacingDir = facingDir2;
                NetFacingDir = facingDir2;
                CurrentState = EnemyState.Summoning;
            }
        }
    }

    // ─────────────────────── ATTACKING ───────────────────────

    private void StateAttacking()
    {
        // Kiểm tra attack đã kết thúc chưa
        if (!combatComp.IsAttacking)
        {
            // Attack xong → chuyển sang Recovery
            float recoveryDuration = combatComp.GetRecoveryDuration(combatComp.CurrentAttackIndex);
            recoveryTimer = TickTimer.CreateFromSeconds(Runner, recoveryDuration);
            CurrentState = EnemyState.Recovery;
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
            return;
        }

        // ─── Đang tấn công: xử lý movement theo kiểu attack ───
        // KHÔNG thay đổi hướng nhìn trong state này!

        if (combatComp.IsLeapAttacking)
        {
            // Leap attack: di chuyển theo arc parabol
            Vector2 leapPos = combatComp.GetLeapPosition();
            transform.position = new Vector3(leapPos.x, leapPos.y, transform.position.z);
            rb.linearVelocity = Vector2.zero;
            NetSpeed = 0f;
        }
        else if (combatComp.IsDashAttacking)
        {
            // Dash: lao về phía player
            Vector2 dashDir = combatComp.DashDirection;
            if (isFlying)
                rb.linearVelocity = dashDir * combatComp.dashSpeed;
            else
                rb.linearVelocity = new Vector2(dashDir.x * combatComp.dashSpeed, rb.linearVelocity.y);

            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
        }
        else
        {
            // Normal: đứng yên
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
        }
    }

    // ─────────────────────── RECOVERY ───────────────────────

    private void StateRecovery()
    {
        // Đứng yên, giữ hướng nhìn khóa
        rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
        NetSpeed = 0f;

        // Chờ recovery timer hết
        if (!recoveryTimer.ExpiredOrNotRunning(Runner)) return;

        // Recovery xong → quyết định state tiếp theo
        if (flyMeleeRetreat && isFlying)
        {
            // Quái bay cận chiến → bay lên cao trước khi lao xuống đánh tiếp
            CurrentState = EnemyState.RetreatingUp;
            return;
        }

        // Tìm player để quyết định chase hay patrol
        FindPlayer();
        if (playerTarget != null)
        {
            CurrentState = EnemyState.Chase;
        }
        else
        {
            currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);
            CurrentState = EnemyState.Patrol;
        }
        noPlayerTimer = 0f;
    }

    // ─────────────────────── RETREATING UP (Fly Melee) ───────────────────────

    private void StateRetreatingUp()
    {
        float retreatY;
        float retreatX = transform.position.x;

        if (playerTarget != null)
        {
            retreatY = playerTarget.position.y + flyMeleeRetreatAltitude;
            retreatX = playerTarget.position.x;
        }
        else
        {
            retreatY = startPosition.y + flyMeleeRetreatAltitude;
        }

        // Clamp tối thiểu: luôn bay cao hơn player/spawn ít nhất 1 unit
        float minY = playerTarget != null ? playerTarget.position.y + 1f : startPosition.y + 1f;
        retreatY = Mathf.Max(retreatY, minY);

        if (transform.position.y >= retreatY - 0.3f)
        {
            // Đã lên cao đủ → tiếp tục AI bình thường
            CurrentState = EnemyState.Chase;
            return;
        }

        // Bay lên vị trí retreat
        Vector2 retreatTarget = new Vector2(retreatX, retreatY);
        Vector2 dir = (retreatTarget - (Vector2)transform.position).normalized;
        rb.linearVelocity = dir * flyMeleeRetreatSpeed;

        // Quay mặt về phía player khi đang bay lên
        if (playerTarget != null)
            UpdateFacing(playerTarget.position.x - transform.position.x);

        NetSpeed = flyMeleeRetreatSpeed;
    }

    // ═══════════════════════════════════════════════════════════════
    // ATTACK EXECUTION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// COMMIT hướng nhìn và bắt đầu tấn công.
    /// Sau khi gọi method này, hướng nhìn sẽ bị KHÓA cho đến hết Recovery.
    /// </summary>
    private void TransitionToAttacking(float xDiff)
    {
        float facingDir;
        if (Mathf.Abs(xDiff) < facingDeadZone)
        {
            // Player quá gần trục X → giữ hướng nhìn hiện tại
            facingDir = NetFacingDir > 0 ? 1f : -1f;
        }
        else
        {
            facingDir = xDiff > 0 ? 1f : -1f;
        }

        // KHÓA hướng nhìn
        AttackLockedFacingDir = facingDir;
        NetFacingDir = facingDir;

        CurrentState = EnemyState.Attacking;
        ExecuteAttack(facingDir);
    }

    /// <summary>
    /// Chọn và thực thi kiểu tấn công (random giữa các kiểu đã bật).
    /// </summary>
    private void ExecuteAttack(float facingDirX)
    {
        var styles = combatComp.GetEnabledAttackStyles();
        var chosen = styles[Random.Range(0, styles.Count)];

        switch (chosen)
        {
            case EnemyCombat.AttackStyle.DashSlash:
                Vector2 dashDir = playerTarget != null
                    ? ((Vector2)(playerTarget.position - transform.position)).normalized
                    : new Vector2(facingDirX, 0);
                combatComp.AttemptDashAttack(dashDir);
                break;

            case EnemyCombat.AttackStyle.LeapAttack:
                Vector2 leapTarget = playerTarget != null
                    ? (Vector2)playerTarget.position
                    : (Vector2)transform.position + new Vector2(facingDirX * 2f, 0);
                combatComp.AttemptLeapAttack(leapTarget);
                break;

            default:
                combatComp.AttemptAttack();
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MOVEMENT HELPERS
    // ═══════════════════════════════════════════════════════════════

    private void DoPatrolMovement()
    {
        if (patrolRadius <= 0.05f)
        {
            MoveTowards(startPosition, patrolSpeed);
            return;
        }

        float dirX = currentTarget.x > transform.position.x ? 1f : -1f;

        // Nếu đụng tường, lập tức quay đầu
        if (!isFlying && IsPathBlocked(dirX))
        {
            float newTargetX = transform.position.x - (dirX * Random.Range(1f, patrolRadius));
            newTargetX = Mathf.Clamp(newTargetX, startPosition.x - patrolRadius, startPosition.x + patrolRadius);
            currentTarget = new Vector2(newTargetX, isFlying ? startPosition.y : transform.position.y);
            dirX = currentTarget.x > transform.position.x ? 1f : -1f;
        }

        if (Mathf.Abs(transform.position.x - currentTarget.x) < 0.25f)
            currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);

        MoveTowards(currentTarget, patrolSpeed);
    }

    private void MoveTowards(Vector2 target, float speed)
    {
        float xDiff = target.x - transform.position.x;
        float dirX = xDiff > 0 ? 1f : -1f;
        UpdateFacing(xDiff);

        if (isFlying)
        {
            Vector2 dir = (target - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * speed;
        }
        else
        {
            rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FACING DIRECTION — Chỉ thay đổi khi STATE cho phép
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Cập nhật hướng nhìn với dead zone chống jitter.
    /// Tự động kiểm tra CanChangeFacing() — chỉ cập nhật ở state cho phép.
    /// </summary>
    private void UpdateFacing(float xDiff)
    {
        if (!CanChangeFacing()) return;
        if (Mathf.Abs(xDiff) < facingDeadZone) return;
        NetFacingDir = xDiff > 0 ? 1f : -1f;
    }

    /// <summary>
    /// Quái chỉ được quay mặt khi ở state di chuyển.
    /// Attacking, Recovery, WakingUp, Sleeping → LOCKED.
    /// </summary>
    private bool CanChangeFacing()
    {
        switch (CurrentState)
        {
            case EnemyState.Patrol:
            case EnemyState.Chase:
            case EnemyState.ReturningToSleep:
            case EnemyState.RetreatingUp:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Ép quay mặt về phía player (gọi từ EnemyController.TakeDamage).
    /// Chỉ hoạt động khi state cho phép thay đổi hướng.
    /// </summary>
    public void ForceFacePlayer()
    {
        if (!CanChangeFacing()) return;
        if (playerTarget != null)
        {
            float xDiff = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) >= facingDeadZone)
                NetFacingDir = xDiff > 0 ? 1f : -1f;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PLAYER DETECTION
    // ═══════════════════════════════════════════════════════════════

    private void FindPlayer()
    {
        if (TryUseCachedChaseTarget()) return;

        cachedChasePlayer = default;
        playerTarget = null;

        foreach (var player in Runner.ActivePlayers)
        {
            NetworkObject pObj = Runner.GetPlayerObject(player);
            if (pObj == null) continue;

            PlayerController pController = pObj.GetComponent<PlayerController>();
            if (pController == null || pController.IsDead) continue;

            float dst = Vector2.Distance(transform.position, pObj.transform.position);
            if (dst > viewRadius) continue;

            playerTarget = pObj.transform;
            cachedChasePlayer = player;
            break;
        }
    }

    private bool TryUseCachedChaseTarget()
    {
        if (!cachedChasePlayer.IsRealPlayer) return false;

        NetworkObject pObj = Runner.GetPlayerObject(cachedChasePlayer);
        if (pObj == null) return false;

        PlayerController pController = pObj.GetComponent<PlayerController>();
        if (pController == null || pController.IsDead) return false;

        float dst = Vector2.Distance(transform.position, pObj.transform.position);
        if (dst > viewRadius * 1.05f) return false;

        playerTarget = pObj.transform;
        return true;
    }

    /// <summary>
    /// Kiểm tra xem có player nào trong bán kính wakeRadius không (dùng cho sleep/wakeup).
    /// Không thay đổi state — chỉ trả về true/false.
    /// </summary>
    private bool CheckPlayerInWakeRadius(float wakeRadius)
    {
        foreach (var player in Runner.ActivePlayers)
        {
            NetworkObject pObj = Runner.GetPlayerObject(player);
            if (pObj == null) continue;

            PlayerController pController = pObj.GetComponent<PlayerController>();
            if (pController == null || pController.IsDead) continue;

            float dst = Vector2.Distance(transform.position, pObj.transform.position);
            if (dst <= wakeRadius)
                return true;
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // OBSTACLE DETECTION
    // ═══════════════════════════════════════════════════════════════

    private bool IsPathBlocked(float dirX)
    {
        Vector2 origin = new Vector2(transform.position.x, transform.position.y + wallCheckHeightOffset);
        Vector2 direction = new Vector2(dirX, 0);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, wallCheckDistance, obstacleLayer);
        Debug.DrawRay(origin, direction * wallCheckDistance, Color.red);

        return hit.collider != null;
    }

    private float PickRandomPatrolX()
    {
        float minX = startPosition.x - patrolRadius;
        float maxX = startPosition.x + patrolRadius;
        return Random.Range(minX, maxX);
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API — Cho các script khác gọi
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Gọi bởi MimicSleepTrigger khi Player chạm collider.
    /// Cơ chế bổ sung cho wakeUpRadius — hỗ trợ collider-based wake.
    /// </summary>
    public void WakeUpFromTouch()
    {
        if (!enableSleep) return;
        if (CurrentState != EnemyState.Sleeping) return;

        CurrentState = EnemyState.WakingUp;
        wakeUpAnimTimer = 0f;
    }

    public void NotifyRevived()
    {
        if (!HasStateAuthority) return;
        currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);
        cachedChasePlayer = default;
        playerTarget = null;
        noPlayerTimer = 0f;

        // Quái hồi sinh → không ngủ lại ngay
        if (enableSleep)
        {
            CurrentState = EnemyState.Patrol;
        }
        else
        {
            CurrentState = EnemyState.Patrol;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SLEEP POSITION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tìm vị trí ngủ: Raycast lên trần (hoặc xuống sàn) từ điểm spawn.
    /// Nếu không tìm được surface → dùng startPosition.
    /// </summary>
    private Vector2 FindSleepPosition()
    {
        if (sleepSurfaceLayer == 0)
            return startPosition;

        Vector2 rayDir = sleepOnCeiling ? Vector2.up : Vector2.down;
        RaycastHit2D hitSurface = Physics2D.Raycast(startPosition, rayDir, 20f, sleepSurfaceLayer);
        if (hitSurface.collider != null)
        {
            float offset = sleepOnCeiling ? -0.3f : 0.3f;
            return hitSurface.point + new Vector2(0f, offset);
        }
        return startPosition;
    }

    // ═══════════════════════════════════════════════════════════════
    // GIZMOS — Debug visualization
    // ═══════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Vòng tròn tầm nhìn (View Radius) - CYAN
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Gizmos.color = new Color(0f, 1f, 1f, 0.05f);
        Gizmos.DrawSphere(transform.position, viewRadius);

        // Vùng tuần tra (Patrol Radius) - GREEN
        Vector2 spawnPos = Application.isPlaying ? startPosition : (Vector2)transform.position;
        if (patrolRadius > 0.05f)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(spawnPos, patrolRadius);
        }

        // Đường đến mục tiêu hiện tại - RED (chỉ khi đang chạy và chase)
        if (Application.isPlaying && CurrentState == EnemyState.Chase && playerTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }

        // State label - WHITE
        if (Application.isPlaying)
        {
            // Hiển thị state hiện tại bằng Gizmo sphere color
            Color stateColor = CurrentState switch
            {
                EnemyState.Sleeping => Color.blue,
                EnemyState.WakingUp => new Color(0.5f, 0.5f, 1f),
                EnemyState.Chase => Color.red,
                EnemyState.Attacking => new Color(1f, 0.3f, 0f),
                EnemyState.Recovery => Color.yellow,
                EnemyState.RetreatingUp => Color.cyan,
                EnemyState.ReturningToSleep => new Color(0.3f, 0.3f, 1f),
                _ => Color.green
            };
            Gizmos.color = stateColor;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }

        // Tia quét tường - MAGENTA
        Gizmos.color = Color.magenta;
        Vector2 wallOrigin = new Vector2(transform.position.x, transform.position.y + wallCheckHeightOffset);
        Gizmos.DrawRay(wallOrigin, Vector2.right * wallCheckDistance);
        Gizmos.DrawRay(wallOrigin, Vector2.left * wallCheckDistance);

        // Vị trí ngủ - BLUE (Sleep)
        if (enableSleep)
        {
            Vector2 sleepPos = Application.isPlaying ? sleepPosition : (Vector2)transform.position;
            Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.7f);
            Gizmos.DrawWireSphere(sleepPos, 0.3f);
            Gizmos.DrawLine(transform.position, (Vector3)sleepPos);

            // Vòng thức dậy (Wake Up Radius) - ORANGE
            float effectiveWakeRadius = wakeUpRadius > 0f ? wakeUpRadius : viewRadius;
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
            Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)(Vector2)transform.position : transform.position, effectiveWakeRadius);
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.06f);
            Gizmos.DrawSphere(Application.isPlaying ? (Vector3)(Vector2)transform.position : transform.position, effectiveWakeRadius);

            // Vẽ tia raycast tìm trần/sàn
            if (!Application.isPlaying)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.4f);
                Vector2 rayDir = sleepOnCeiling ? Vector2.up : Vector2.down;
                Gizmos.DrawRay(transform.position, (Vector3)(rayDir * 20f));
            }
        }
    }
}