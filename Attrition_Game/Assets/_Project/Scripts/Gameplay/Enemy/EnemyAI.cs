using Attrition.Controllers;
using Fusion;
using UnityEngine;
using Attrition.Gameplay.Enemy;

// ENEMY STATE — Finite State Machine
// Chỉ MỘT state active tại mỗi thời điểm.
// Hướng nhìn chỉ thay đổi ở state được phép (Patrol, Chase).
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
    Summoning,          // Đang triệu hồi quái phụ (facing LOCKED)
    Jumping,            // Đang nhảy (tránh né / di chuyển)
    Telegraphing        // Đang "lấy đà" báo đòn trước khi đánh (facing LOCKED, đứng yên)
}

public class EnemyAI : NetworkBehaviour
{

    [Header("---- REFS ----")]
    [SerializeField] protected EnemyAnimation animationComp;
    [SerializeField] protected EnemyCombat combatComp;
    [SerializeField] protected EnemyController controller;
    [Tooltip("Gắn EliteEnemySkills nếu đây là quái tinh anh (Cultist, NightBorne, Gollux). Bỏ trống nếu quái thường.")]
    [SerializeField] private EliteEnemySkills eliteSkills;
    protected Rigidbody2D rb;

    [Header("---- SETTINGS ----")]
    public float viewRadius = 5f;
    [Tooltip("Tuần tra ngẫu nhiên theo trục X quanh điểm spawn.")]
    public float patrolRadius = 3f;
    [Tooltip("Đánh dấu nếu quái là loại bay (di chuyển cả trục Y khi đuổi)")]
    public bool isFlying = false;

    [Header("---- FLY ENGAGEMENT (cao độ giao chiến) ----")]
    [Tooltip("Độ cao quái bay muốn giữ SO VỚI thân player khi đuổi (units). 0 = ngang thân, dương = hơi cao hơn. Nên 0.5-1.2 để chéo xuống đánh.")]
    public float flyHoverOffsetY = 0.8f;
    [Tooltip("Khoảng cách an toàn TỐI THIỂU so với mặt đất khi bay (units) — tránh sà sát đất.")]
    public float flyMinGroundClearance = 1.2f;
    [Tooltip("Giới hạn lệch cao độ TỐI ĐA so với player (units) — tránh bay quá cao trên đầu rồi không đánh được.")]
    public float flyMaxOffsetFromPlayer = 2.5f;
    [Tooltip("Layer mặt đất để đo ground clearance (thường = obstacleLayer).")]
    public LayerMask flyGroundLayer;

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

    [Header("---- LINE OF SIGHT ----")]
    [Tooltip("Độ cao 'mắt' quái so với pivot khi quét tầm nhìn. Dời lên để tia không bắt đầu trong sàn.")]
    public float eyeHeightOffset = 0.6f;
    [Tooltip("Giữ mục tiêu thêm bao lâu khi bị tường che (giây) — tránh mất aggro vì che chớp nhoáng.")]
    public float sightMemoryDuration = 1.5f;

    [Header("---- STEP UP (leo bậc 1 ô) ----")]
    [Tooltip("Bật để quái nhún lên bậc/ô cao khi đuổi player. Tắt = dừng trước mọi vật cản như cũ.")]
    public bool canStepUp = true;
    [Tooltip("Chiều cao bậc TỐI ĐA quái vượt được (units). ~1 ô tile.")]
    public float stepUpMaxHeight = 1.2f;
    [Tooltip("Tốc ngang tối thiểu khi nhún lên bậc.")]
    public float stepUpForwardSpeed = 4f;
    [Tooltip("Cooldown giữa 2 lần leo bậc (giây) — chống nhún liên tục vào chân tường.")]
    public float stepUpCooldown = 0.6f;

    [Header("---- JUMP / EVADE (Elite) ----")]
    [Tooltip("Bật để quái có thể nhảy lùi né đòn (Backstep) khi player tới quá gần")]
    public bool canEvadeJump = false;
    [Tooltip("Khoảng cách kích hoạt nhảy lùi")]
    public float evadeTriggerDistance = 2f;
    [Tooltip("Lực nhảy (Y)")]
    public float jumpForce = 12f;
    [Tooltip("Lực lùi (X)")]
    public float evadeBackwardSpeed = 8f;
    [Tooltip("Cooldown giữa 2 lần nhảy")]
    public float jumpCooldown = 3f;

    [Header("---- LUNGE JUMP (Elite, lao tới) ----")]
    [Tooltip("Bật để quái nhảy lao TỚI player khi player ở ngoài tầm đánh nhưng trong khoảng lao.")]
    public bool canLungeJump = false;
    [Tooltip("Player phải xa hơn mức này (thường = MaxAttackRange) mới lao tới.")]
    public float lungeMinDistance = 2.5f;
    [Tooltip("Player phải gần hơn mức này thì mới đáng để lao (ngoài khoảng này thì đi bộ tới).")]
    public float lungeMaxDistance = 7f;
    [Tooltip("Lực ngang khi lao tới.")]
    public float lungeForwardSpeed = 11f;
    [Tooltip("Lực nhảy (Y) khi lao tới.")]
    public float lungeJumpForce = 10f;

    [Header("---- FACING ----")]
    [Tooltip("Dead zone: không đổi hướng nhìn khi khoảng cách X với mục tiêu nhỏ hơn giá trị này (tránh giật/nhấp nháy)")]
    public float facingDeadZone = 0.3f;

    // NETWORKED STATE — Single source of truth

    [HideInInspector][Networked] public EnemyState CurrentState { get; set; }
    [HideInInspector][Networked] public float NetSpeed { get; set; }
    [HideInInspector][Networked] public float NetFacingDir { get; set; } = 1f;
    [HideInInspector][Networked] public NetworkBool IsJumping { get; set; }

    /// <summary>
    /// Hướng nhìn đã KHÓA khi bắt đầu tấn công.
    /// Giữ nguyên suốt Attacking + Recovery state.
    /// </summary>
    [HideInInspector][Networked] public float AttackLockedFacingDir { get; set; } = 1f;

    [Networked] private TickTimer recoveryTimer { get; set; }
    [Networked] private TickTimer jumpCooldownTimer { get; set; }
    [Networked] private TickTimer telegraphTimer { get; set; }

    // BACKWARD COMPATIBILITY — Cho MimicSleepTrigger và các script cũ

    /// <summary>
    /// Property tương thích ngược. MimicSleepTrigger dùng property này.
    /// Đọc từ CurrentState thay vì boolean riêng.
    /// </summary>
    public bool IsSleeping => CurrentState == EnemyState.Sleeping;

    /// <summary>Đang giao chiến (đuổi/đánh/skill) — dùng để chặn Rest. Patrol/Sleep = không giao chiến.</summary>
    public bool IsAggressive =>
        CurrentState == EnemyState.Chase ||
        CurrentState == EnemyState.Attacking ||
        CurrentState == EnemyState.Recovery ||
        CurrentState == EnemyState.UsingSkill ||
        CurrentState == EnemyState.Summoning ||
        CurrentState == EnemyState.RetreatingUp;

    // LOCAL STATE (không cần đồng bộ mạng)

    protected Vector2 startPosition;
    private Vector2 sleepPosition;
    private Vector2 currentTarget;
    protected Transform playerTarget;
    private PlayerRef cachedChasePlayer;
    protected EnemyStats statsComp;

    // Sleep timers
    private float noPlayerTimer;
    private float wakeUpAnimTimer;

    // Đòn đã chốt khi bắt đầu telegraph (local — chỉ host chạy AI nên không cần sync).
    private EnemyCombat.AttackStyle _committedAttackStyle;

    // Per-attack chase: đã chọn đòn trước khi check tầm chưa
    private bool _hasCommittedAttack;
    // Anti-jitter: đếm thời gian bị tường/vực chặn liên tục
    private float _chaseBlockedTimer;
    // Sight memory: còn bao lâu vẫn giữ target sau khi mất tầm nhìn (giây, host-only)
    private float _sightMemoryTimer;
    // Chống spam nhún bậc
    private float _stepUpCooldownTimer;
    // Đang trong cú nhún leo bậc (giữ animation DI CHUYỂN, không phải animation nhảy)
    private bool _isStepUpHop;
    // Hướng + tốc ngang phải DUY TRÌ suốt cú nhún: thân tì vào vách bậc thì collision triệt tiêu
    // vx ngay tick sau → nhảy thẳng lên rồi rơi tại chỗ, không lao được lên bậc.
    private float _stepUpDirX;
    private float _stepUpSpeed;

    // Render-side animation state (tránh gọi anim lặp)
    private bool localSleepHandled;
    private bool localWakeHandled;

    // Material dùng CHUNG cho mọi quái (một instance duy nhất, không phải mỗi con một cái).
    private static PhysicsMaterial2D _frictionlessBody;
    private static PhysicsMaterial2D FrictionlessBody
    {
        get
        {
            if (_frictionlessBody == null)
                _frictionlessBody = new PhysicsMaterial2D("EnemyBody_NoFriction") { friction = 0f, bounciness = 0f };
            return _frictionlessBody;
        }
    }


    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();

        // Prefab quái không gán PhysicsMaterial2D → dùng default friction 0.4 → thân CÀ vào tường,
        // bị hãm lại khi nhún leo bậc và dính vào vách. Player đã dùng material friction 0; gán
        // material dùng chung ở runtime thay vì sửa tay 25 prefab.
        // ponytail: material tạo bằng code, không lộ ra Inspector. Nếu sau này cần bounciness/friction
        // riêng theo loại quái thì tạo asset .physicsMaterial2D và gán trong prefab.
        if (rb != null && rb.sharedMaterial == null) rb.sharedMaterial = FrictionlessBody;

        if (combatComp == null) combatComp = GetComponent<EnemyCombat>();
        if (controller == null) controller = GetComponent<EnemyController>();
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
        statsComp = GetComponent<EnemyStats>();
        startPosition = transform.position;

        // Client physics = ForwardOnly (đặt ở NetworkLauncher) → Fusion muốn DỰ ĐOÁN physics của
        // enemy trên client. Nếu object không được đánh dấu IsSimulated, Fusion spam cảnh báo mỗi
        // frame + enemy giật trên client. Đánh dấu để client simulate mượt (host vẫn là nguồn chân lý).
        if (Object != null) Runner.SetIsSimulated(Object, true);

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

    // RENDER — Animation & Facing (chạy trên TẤT CẢ clients)

    public override void Render()
    {
        if (controller == null) return;

        if (controller.isDeadNetworked || controller.IsAwaitingRevive)
        {
            animationComp.UpdateSpeed(0f);
            return;
        }

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

        // IsGrounded() = 1 rb.Cast physics. Chỉ chạy khi animator THẬT SỰ có param bay — quái đi đất
        // không có param nào, trước đây vẫn tốn 1 cast/frame/con quái (nhân thêm khi client resimulate).
        if (rb != null && animationComp.NeedsAirState)
        {
            animationComp.UpdateAirState(rb.linearVelocity.y, IsGrounded());
        }

        // State-based lock: Attacking, Recovery, WakingUp, UsingSkill, Summoning → dùng AttackLockedFacingDir
        // Elite override: Healing, Teleporting → giữ nguyên NetFacingDir (không thay đổi)
        // Tất cả state khác → dùng NetFacingDir
        bool facingLocked = CurrentState == EnemyState.Attacking
                         || CurrentState == EnemyState.Recovery
                         || CurrentState == EnemyState.WakingUp
                         || CurrentState == EnemyState.UsingSkill
                         || CurrentState == EnemyState.Summoning
                         || CurrentState == EnemyState.Telegraphing;

        animationComp.FaceDirection(facingLocked ? AttackLockedFacingDir : NetFacingDir);

        // Nhấp nháy báo đòn khi đang telegraph (hiệu ứng hình, chạy mọi máy).
        animationComp.SetTelegraph(CurrentState == EnemyState.Telegraphing);
    }

    // AI LOGIC — State Machine (chỉ chạy trên Host/StateAuthority)

    public virtual void RunAILogic()
    {
        if (controller.IsKnockbackActive)
        {
            HandleKnockbackOverride();
            return;
        }

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
                // Skill xong → Bắt đầu trạng thái Recovery để đợi một lúc rồi mới đánh tiếp (tránh cancel lẫn nhau)
                float recov = eliteSkills.GetCurrentSkillRecovery();
                recoveryTimer = TickTimer.CreateFromSeconds(Runner, recov);
                CurrentState = EnemyState.Recovery;
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

        if (IsJumping)
        {
            StateJumping();
            return;
        }

        switch (CurrentState)
        {
            case EnemyState.Sleeping:         StateSleeping();         break;
            case EnemyState.WakingUp:         StateWakingUp();         break;
            case EnemyState.ReturningToSleep: StateReturningToSleep(); break;
            case EnemyState.Patrol:           StatePatrol();           break;
            case EnemyState.Chase:            StateChase();            break;
            case EnemyState.Attacking:        StateAttacking();        break;
            case EnemyState.Recovery:         StateRecovery();         break;
            case EnemyState.Telegraphing:     StateTelegraphing();     break;
            case EnemyState.RetreatingUp:     StateRetreatingUp();     break;
            case EnemyState.UsingSkill:       /* handled by override above */ break;
            case EnemyState.Summoning:        /* handled by override above */ break;
        }
    }



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
            case EnemyState.UsingSkill:
            case EnemyState.Summoning:
            case EnemyState.Telegraphing:
                // Bị đánh khi đang tấn công/hồi phục/bay lên/dùng skill/báo đòn → hủy, chuyển chase
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


    private void StateReturningToSleep()
    {
        // Nếu bất ngờ thấy player khi đang về → tỉnh dậy đuổi ngay (ưu tiên cao nhất).
        FindPlayer();
        if (playerTarget != null)
        {
            CurrentState = EnemyState.Chase;
            noPlayerTimer = 0f;
            return;
        }

        if (isFlying)
        {
            // QUÁI BAY: bay thẳng (chéo) lên vị trí ngủ trên trần.
            float dist = Vector2.Distance(transform.position, sleepPosition);
            if (dist < 0.3f)
            {
                transform.position = new Vector3(sleepPosition.x, sleepPosition.y, transform.position.z);
                rb.linearVelocity = Vector2.zero;
                EnterSleep();
                return;
            }

            Vector2 dir = (sleepPosition - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * returnToSleepSpeed;
            UpdateFacing(dir.x);
            NetSpeed = returnToSleepSpeed;
        }
        else
        {
            // QUÁI ĐẤT: KHÔNG bay lơ lửng. Đi NGANG về cột X của spawn, để trọng lực giữ chân trên đất.
            float xDiff = sleepPosition.x - transform.position.x;
            if (Mathf.Abs(xDiff) <= 0.25f)
            {
                // Đã về đúng cột X spawn → đứng yên, để rơi/đứng trên nền rồi ngủ.
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                NetSpeed = 0f;
                if (IsGrounded()) EnterSleep();
                return;
            }

            float dirX = xDiff > 0 ? 1f : -1f;
            // Bị tường chặn giữa đường → ngủ luôn tại chỗ (không kẹt mãi).
            if (IsPathBlocked(dirX))
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                NetSpeed = 0f;
                if (IsGrounded()) EnterSleep();
                return;
            }

            float rSpeed = returnToSleepSpeed;
            rb.linearVelocity = new Vector2(dirX * rSpeed, rb.linearVelocity.y);
            UpdateFacing(xDiff);
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
        }
    }

    /// <summary>Chốt trạng thái ngủ: reset target, hướng nhìn về spawn.</summary>
    private void EnterSleep()
    {
        CurrentState = EnemyState.Sleeping;
        cachedChasePlayer = default;
        playerTarget = null;
        NetSpeed = 0f;
    }


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
            _hasCommittedAttack = false;
            _chaseBlockedTimer = 0f;
            return;
        }

        // Đang thấy player → reset sleep timer
        noPlayerTimer = 0f;

        currentTarget = playerTarget.position;
        float dist = Vector2.Distance(transform.position, currentTarget);
        float xDiff = currentTarget.x - transform.position.x;

        if (_stepUpCooldownTimer > 0f) _stepUpCooldownTimer -= Runner.DeltaTime;

        PathObstacle obstacle = PathObstacle.None;
        if (!isFlying)
        {
            obstacle = ProbePath(xDiff > 0 ? 1f : -1f);
            float yDiff = Mathf.Abs(playerTarget.position.y - transform.position.y);
            float xDist = Mathf.Abs(xDiff);

            // Player quá cao (trên đầu quái) VÀ gần cùng cột X
            // HOẶC bị TƯỜNG (không leo được) chặn liên tục > 1 giây → bỏ đuổi.
            // Bậc leo được (StepUp) KHÔNG tính là kẹt: dưới đây sẽ nhún lên, kể cả khi player cao
            // hơn nhiều — leo từng bậc vẫn tới được, chỉ bỏ khi thật sự hết đường.
            bool canClimbForward = canStepUp && obstacle == PathObstacle.StepUp;
            if (!canClimbForward
                && ((yDiff > 2.5f && xDist < 1.5f) || (obstacle == PathObstacle.Wall && _chaseBlockedTimer > 1f)))
            {
                currentTarget = new Vector2(PickRandomPatrolX(), transform.position.y);
                CurrentState = EnemyState.Patrol;
                noPlayerTimer = 0f;
                _hasCommittedAttack = false;
                _chaseBlockedTimer = 0f;
                NetSpeed = 0f;
                return;
            }
        }

        // Kiểm tra tầm nhìn tới Player (không bị tường/sàn che) — origin ở tầm mắt, xem HasLineOfSightTo.
        bool hasLineOfSight = HasLineOfSightTo(playerTarget.position);

        if (hasLineOfSight)
        {
            // 1. KIỂM TRA NHẢY LÙI (EVADE) — player áp sát quá gần
            if (canEvadeJump && !isFlying && dist <= evadeTriggerDistance && jumpCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                ExecuteEvadeJump(xDiff);
                return;
            }

            // 1b. KIỂM TRA NHẢY LAO TỚI (LUNGE) — player ngoài tầm đánh nhưng trong khoảng lao
            if (canLungeJump && !isFlying && dist > combatComp.MaxEngageRange
                && dist >= lungeMinDistance && dist <= lungeMaxDistance
                && IsGrounded() && jumpCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                ExecuteLungeJump(xDiff);
                return;
            }

            // 2. KIỂM TRA DÙNG SKILL TẦM XA (NÉM LAO)
            if (eliteSkills != null && eliteSkills.TryUseSkill(dist))
            {
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
                UpdateFacing(xDiff);
                NetSpeed = 0f;
                float facingDir = Mathf.Abs(xDiff) < facingDeadZone ? (NetFacingDir > 0 ? 1f : -1f) : (xDiff > 0 ? 1f : -1f);
                AttackLockedFacingDir = facingDir;
                NetFacingDir = facingDir;
                CurrentState = EnemyState.UsingSkill;
                return;
            }

            // 3. KIỂM TRA ĐÁNH CẬN CHIẾN — dùng tầm tiếp cận CỦA ĐÒN ĐÃ CHỌN
            // Chuẩn bị đòn TRƯỚC → biết tầm cần chạy tới
            if (combatComp.CanAttack() && !_hasCommittedAttack)
            {
                _committedAttackStyle = combatComp.PrepareNextAttack();
                _hasCommittedAttack = true;
            }
            float neededRange = _hasCommittedAttack
                ? combatComp.GetEngageRangeForCurrentAttack()
                : combatComp.MaxEngageRange;

            if (dist <= neededRange)
            {
                // TRONG TẦM ĐÁNH VÀ CÓ TẦM NHÌN → dừng lại
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
                UpdateFacing(xDiff);
                NetSpeed = 0f;

                if (combatComp.CanAttack())
                {
                    TransitionToAttacking(xDiff);
                    _hasCommittedAttack = false;
                }
                else if (eliteSkills != null)
                {
                    eliteSkills.TryTeleport(dist, playerTarget);
                }
                return;
            }
        }

        // 4. NGOÀI TẦM ĐÁNH VÀ KHÔNG DÙNG SKILL → đuổi theo
        if (eliteSkills != null && eliteSkills.TryTeleport(dist, playerTarget))
        {
            // Đã bắt đầu teleport → RunAILogic sẽ xử lý ở elite override
        }
        else if (!isFlying && obstacle == PathObstacle.StepUp
                 && canStepUp && _stepUpCooldownTimer <= 0f && IsGrounded())
        {
            // Bậc ~1 ô phía trước → nhún lên để tiếp tục đuổi (giữ animation di chuyển).
            _chaseBlockedTimer = 0f;
            ExecuteStepUpHop(xDiff);
            return;
        }
        else if (!isFlying && obstacle != PathObstacle.None)
        {
            // Tường không vượt được (hoặc bậc đang cooldown) → ĐỨNG YÊN, không đập mặt vào tường.
            // _chaseBlockedTimer sẽ đẩy quái về Patrol sau 1 giây (xem đầu StateChase).
            _chaseBlockedTimer += Runner.DeltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            UpdateFacing(xDiff);
            NetSpeed = 0f;
        }
        else
        {
            _chaseBlockedTimer = 0f; // Đường thông → reset bộ đếm
            float cSpeed = (statsComp != null ? statsComp.ChaseSpeed : 5f) * SlowFactor();
            // Quái bay: nhắm cao độ ngang thân player (clamp ground clearance + max offset), không sà đất / bay quá cao.
            Vector2 chaseTarget = isFlying ? ComputeFlyTarget(currentTarget) : currentTarget;
            MoveTowards(chaseTarget, cSpeed);
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
        }

        // Elite: roll heal và summon ngẫu nhiên khi đang chase nhưng ngoài tầm đánh
        if (eliteSkills != null && !combatComp.IsAttacking && (dist > combatComp.MaxEngageRange || !hasLineOfSight))
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

        // Clamp tối đa: không vọt quá cao khỏi player (giữ trong tầm lao xuống đánh được).
        if (playerTarget != null)
            retreatY = Mathf.Min(retreatY, playerTarget.position.y + flyMaxOffsetFromPlayer + flyMeleeRetreatAltitude);

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


    private void ExecuteEvadeJump(float xDiff)
    {
        CurrentState = EnemyState.Jumping;
        IsJumping = true;
        _isStepUpHop = false; // nhảy né có animation nhảy riêng
        jumpCooldownTimer = TickTimer.CreateFromSeconds(Runner, jumpCooldown);
        
        // Nhảy lùi (ngược hướng xDiff)
        float jumpDirX = xDiff > 0 ? -1f : 1f;
        
        // Khóa hướng nhìn về phía player khi nhảy lùi
        float facingDir = xDiff > 0 ? 1f : -1f;
        NetFacingDir = facingDir;
        AttackLockedFacingDir = facingDir;
        
        rb.linearVelocity = new Vector2(jumpDirX * evadeBackwardSpeed, jumpForce);

        RPC_PlayJumpAnim();
    }

    /// <summary>Nhảy lao TỚI player (lunge). Dùng chung Jumping state để xử lý chạm đất.</summary>
    private void ExecuteLungeJump(float xDiff)
    {
        CurrentState = EnemyState.Jumping;
        IsJumping = true;
        _isStepUpHop = false; // nhảy lao có animation nhảy riêng
        jumpCooldownTimer = TickTimer.CreateFromSeconds(Runner, jumpCooldown);

        // Lao về phía player (cùng hướng xDiff)
        float lungeDirX = xDiff > 0 ? 1f : -1f;
        float facingDir = lungeDirX;
        NetFacingDir = facingDir;
        AttackLockedFacingDir = facingDir;

        rb.linearVelocity = new Vector2(lungeDirX * lungeForwardSpeed, lungeJumpForce);

        RPC_PlayJumpAnim();
    }

    /// <summary>
    /// Nhún lên bậc ~1 ô để tiếp tục đuổi player ở trên cao.
    /// KHÔNG gọi RPC_PlayJumpAnim: quái thường không có animation nhảy → giữ animation DI CHUYỂN
    /// (NetSpeed > 0 suốt cú nhún, xem StateJumping).
    /// Lực Y tính từ chiều cao bậc: v = sqrt(2 * g * h) + dư 25% cho ma sát/độ trễ tick.
    /// </summary>
    private void ExecuteStepUpHop(float xDiff)
    {
        CurrentState = EnemyState.Jumping;
        IsJumping = true;
        _isStepUpHop = true;
        _stepUpCooldownTimer = stepUpCooldown;
        // Dùng jumpCooldownTimer làm mốc "đã nhảy đủ lâu" cho StateJumping (không chặn evade/lunge lâu).
        jumpCooldownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Min(jumpCooldown, 0.5f));

        float dirX = xDiff > 0 ? 1f : -1f;
        NetFacingDir = dirX;
        AttackLockedFacingDir = dirX;

        float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.01f, rb.gravityScale);
        float vy = Mathf.Sqrt(2f * g * (stepUpMaxHeight + 0.35f)) * 1.25f;
        float vx = Mathf.Max(stepUpForwardSpeed, Mathf.Abs(rb.linearVelocity.x));

        _stepUpDirX = dirX;
        _stepUpSpeed = vx;
        rb.linearVelocity = new Vector2(dirX * vx, vy);
    }

    private void StateJumping()
    {
        // Khóa hướng nhìn
        animationComp.FaceDirection(AttackLockedFacingDir);

        if (_isStepUpHop)
        {
            // DUY TRÌ tốc ngang mỗi tick. Không làm vậy thì thân tì vào vách bậc → collision
            // triệt tiêu vx ngay tick sau → quái nhảy thẳng lên rồi rơi đúng chỗ cũ, không lên bậc.
            rb.linearVelocity = new Vector2(_stepUpDirX * _stepUpSpeed, rb.linearVelocity.y);
            // Không có animation nhảy → giữ animation di chuyển bằng NetSpeed > 0.
            NetSpeed = _stepUpSpeed;
        }
        else
        {
            NetSpeed = 0f;
        }

        // An toàn: Phải nhảy được ít nhất 0.2 giây thì mới bắt đầu check rớt xuống chạm đất
        // Để tránh việc Frame 1 Physics chưa kịp đẩy lên đã bị coi là đang ở mặt đất
        bool hasJumpedLongEnough = _isStepUpHop
            ? jumpCooldownTimer.RemainingTime(Runner).GetValueOrDefault() < 0.35f  // hop ngắn: mốc riêng
            : (!jumpCooldownTimer.IsRunning || jumpCooldownTimer.RemainingTime(Runner) < (jumpCooldown - 0.2f));

        // Nếu rớt xuống (vận tốc Y <= 0.1) và chạm đất -> kết thúc nhảy
        if (hasJumpedLongEnough && rb.linearVelocity.y <= 0.1f && IsGrounded())
        {
            rb.linearVelocity = new Vector2(0f, 0f);
            CurrentState = EnemyState.Chase;
            IsJumping = false;
            _isStepUpHop = false;
            noPlayerTimer = 0f;
        }
    }

    // Buffer + filter dùng lại. IsGrounded() được gọi trong Render (mỗi frame, MỖI con quái) → cấp phát
    // mới ở đây tạo rác GC tỉ lệ (số quái × FPS); trên client còn nhân thêm vì resimulation.
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[1];
    private ContactFilter2D _groundFilter;
    private bool _groundFilterReady;

    private bool IsGrounded()
    {
        if (rb == null) return true;
        // Sử dụng rb.Cast giống hệt như PlayerController để quét nguyên hình dáng Collider xuống đất
        // Khắc phục hoàn toàn lỗi sai pivot hoặc lệch tâm
        if (!_groundFilterReady)
        {
            _groundFilter = new ContactFilter2D { layerMask = obstacleLayer, useLayerMask = true };
            _groundFilterReady = true;
        }
        return rb.Cast(Vector2.down, _groundFilter, _groundHits, 0.1f) > 0;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayJumpAnim()
    {
        if (animationComp != null) animationComp.PlayJump();
    }


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

        // Chọn TRƯỚC đòn sẽ đánh để biết telegraph bao lâu (đòn nặng báo lâu hơn).
        // Nếu đã chọn trong StateChase (per-attack range) → dùng lại, không random lần nữa.
        if (!_hasCommittedAttack)
        {
            _committedAttackStyle = combatComp.PrepareNextAttack();
        }
        _hasCommittedAttack = false;
        float telegraph = combatComp.GetTelegraphDuration(combatComp.CurrentAttackIndex);

        if (telegraph > 0.01f)
        {
            // Có telegraph → vào trạng thái "lấy đà" (đứng yên, nhấp nháy) rồi mới đánh.
            telegraphTimer = TickTimer.CreateFromSeconds(Runner, telegraph);
            CurrentState = EnemyState.Telegraphing;
        }
        else
        {
            // Không telegraph → đánh ngay.
            CurrentState = EnemyState.Attacking;
            ExecuteCommittedAttack(facingDir);
        }
    }


    private void StateTelegraphing()
    {
        // Đứng yên, giữ hướng nhìn khóa, chờ hết "lấy đà".
        rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
        NetSpeed = 0f;

        if (!telegraphTimer.ExpiredOrNotRunning(Runner)) return;

        CurrentState = EnemyState.Attacking;
        ExecuteCommittedAttack(AttackLockedFacingDir);
    }

    /// <summary>
    /// Thực thi đòn ĐÃ CHỌN (committed) trong PrepareNextAttack — dùng index đã chốt để telegraph khớp.
    /// </summary>
    private void ExecuteCommittedAttack(float facingDirX)
    {
        int idx = combatComp.CurrentAttackIndex;
        switch (_committedAttackStyle)
        {
            case EnemyCombat.AttackStyle.DashSlash:
                Vector2 dashDir = playerTarget != null
                    ? ((Vector2)(playerTarget.position - transform.position)).normalized
                    : new Vector2(facingDirX, 0);
                combatComp.AttemptDashAttack(dashDir, idx);
                break;

            case EnemyCombat.AttackStyle.LeapAttack:
                Vector2 leapTarget = playerTarget != null
                    ? (Vector2)playerTarget.position
                    : (Vector2)transform.position + new Vector2(facingDirX * 2f, 0);
                combatComp.AttemptLeapAttack(leapTarget, idx);
                break;

            default:
                combatComp.AttemptAttack(idx);
                break;
        }
    }


    /// <summary>Hệ số tốc độ do hiệu ứng LÀM CHẬM (accessory) áp lên quái. 1 = bình thường.</summary>
    private float SlowFactor() => controller != null ? controller.SlowMultiplier : 1f;

    private void DoPatrolMovement()
    {
        float pSpeed = (statsComp != null ? statsComp.PatrolSpeed : 2f) * SlowFactor();

        if (patrolRadius <= 0.05f)
        {
            MoveTowards(startPosition, pSpeed);
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

        MoveTowards(currentTarget, pSpeed);
    }

    /// <summary>
    /// Cao độ giao chiến cho quái bay: nhắm ngang thân player + offset, nhưng
    /// KẸP để (1) không sà sát đất (flyMinGroundClearance) và (2) không bay quá cao trên đầu player
    /// (flyMaxOffsetFromPlayer). Trả về target đã điều chỉnh Y; giữ nguyên X.
    /// </summary>
    private Vector2 ComputeFlyTarget(Vector2 rawTarget)
    {
        float desiredY = rawTarget.y + flyHoverOffsetY;

        // (2) Không lệch quá xa so với player theo cả 2 chiều.
        float maxY = rawTarget.y + flyMaxOffsetFromPlayer;
        float minY = rawTarget.y - flyMaxOffsetFromPlayer;
        desiredY = Mathf.Clamp(desiredY, minY, maxY);

        // (1) Giữ khoảng hở tối thiểu với mặt đất ngay dưới vị trí target.
        LayerMask groundMask = flyGroundLayer.value != 0 ? flyGroundLayer : obstacleLayer;
        RaycastHit2D ground = Phys.Raycast(new Vector2(rawTarget.x, desiredY), Vector2.down, 30f, groundMask);
        if (ground.collider != null)
        {
            float minAboveGround = ground.point.y + flyMinGroundClearance;
            if (desiredY < minAboveGround) desiredY = minAboveGround;
        }

        return new Vector2(rawTarget.x, desiredY);
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

    // FACING DIRECTION — Chỉ thay đổi khi STATE cho phép

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
    public virtual void ForceFacePlayer()
    {
        if (!CanChangeFacing()) return;
        if (playerTarget != null)
        {
            float xDiff = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) >= facingDeadZone)
                NetFacingDir = xDiff > 0 ? 1f : -1f;
        }
    }


    protected void FindPlayer()
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

            // Không nhìn xuyên tường/mặt đất: phải THẤY mới được aggro mới.
            if (!HasLineOfSightTo(pObj.transform.position)) continue;

            playerTarget = pObj.transform;
            cachedChasePlayer = player;
            _sightMemoryTimer = sightMemoryDuration;
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

        // Mất tầm nhìn → vẫn giữ target trong sightMemoryDuration (quái "nhớ" chỗ vừa thấy),
        // hết bộ nhớ mà vẫn bị che → nhả target. Tránh vừa mất thấy đã quên (giật aggro).
        if (HasLineOfSightTo(pObj.transform.position))
        {
            _sightMemoryTimer = sightMemoryDuration;
        }
        else
        {
            _sightMemoryTimer -= Runner.DeltaTime;
            if (_sightMemoryTimer <= 0f) return false;
        }

        playerTarget = pObj.transform;
        return true;
    }

    /// <summary>
    /// Tia từ "mắt" quái tới thân mục tiêu, chặn bởi obstacleLayer (đất + tường).
    /// Origin dời lên eyeHeightOffset vì pivot quái thường nằm SÁT/TRONG collider sàn → tia tự chặn chính nó.
    /// </summary>
    protected bool HasLineOfSightTo(Vector2 worldPos)
    {
        if (obstacleLayer.value == 0) return true; // chưa cấu hình layer → không chặn (giữ hành vi cũ)
        return Phys.Linecast(EyePosition, worldPos, obstacleLayer).collider == null;
    }

    /// <summary>
    /// Vị trí "mắt": TÂM collider thân nếu có (chắc chắn nằm trên mặt sàn, không phụ thuộc pivot),
    /// không thì pivot + eyeHeightOffset. Tia LOS bắt đầu từ trong sàn thì luôn báo bị chặn.
    /// </summary>
    private Vector2 EyePosition
    {
        get
        {
            var col = BodyCollider;
            if (col != null) return col.bounds.center;
            return (Vector2)transform.position + Vector2.up * eyeHeightOffset;
        }
    }

    /// <summary>
    /// Physics scene CỦA RUNNER. BẮT BUỘC dùng cái này thay cho Physics2D tĩnh: Fusion chạy
    /// multi-peer thì mỗi peer có physics scene riêng, query tĩnh bắn vào scene rỗng → KHÔNG trúng gì
    /// (LOS/quét tường/leo bậc im lặng vô hiệu). Xem PlayerController.IsGrounded cùng lý do.
    /// </summary>
    private PhysicsScene2D Phys => Runner != null ? Runner.GetPhysicsScene2D() : Physics2D.defaultPhysicsScene;

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


    /// <summary>Loại vật cản phía trước — quyết định nên nhún lên, hay bỏ đuổi.</summary>
    protected enum PathObstacle
    {
        None,     // đường thông
        StepUp,   // bậc thấp: nhún 1 cái là lên được
        Wall      // tường cao/vực: không vượt được
    }

    /// <summary>
    /// Collider THÂN (non-trigger) — thường nằm ở CHILD, không phải root. Cần bounds thật của nó vì:
    /// - pivot quái nằm giữa thân, không phải ở chân → tia "sát chân" tính từ pivot lại nằm lưng lửng
    /// - thân Slime rộng 1.7 mà wallCheckDistance chỉ 0.8 → tia chưa ra khỏi người đã hết tầm
    /// </summary>
    private Collider2D _bodyCol;
    private bool _bodyColSearched;
    private Collider2D BodyCollider
    {
        get
        {
            if (!_bodyColSearched)
            {
                _bodyColSearched = true;
                foreach (var c in GetComponentsInChildren<Collider2D>(true))
                {
                    if (c == null || c.isTrigger) continue;
                    _bodyCol = c;
                    break;
                }
            }
            return _bodyCol;
        }
    }

    /// <summary>
    /// Phân loại vật cản phía trước bằng 2 tia: tia THẤP (sát chân) và tia CAO (đỉnh bậc cho phép).
    /// - thấp trúng + cao thông  → bậc thấp (StepUp)
    /// - cao trúng              → tường (Wall)
    /// Một tia duy nhất ở giữa thân không phân biệt được bậc 1 ô với tường 5 ô.
    ///
    /// MỌI mốc đều tính từ bounds collider THÂN (không phải pivot): tia bắt đầu ở RÌA thân và dài
    /// wallCheckDistance TỪ RÌA, nếu không thì với quái to (Slime rộng 1.7) tia chết trong chính thân nó.
    /// </summary>
    protected PathObstacle ProbePath(float dirX)
    {
        if (obstacleLayer.value == 0) return PathObstacle.None;

        Vector2 dir = new Vector2(dirX > 0 ? 1f : -1f, 0f);
        var col = BodyCollider;

        // Không có collider thân → quay về cách cũ (tính từ pivot).
        Vector2 pos = transform.position;
        float footY = col != null ? col.bounds.min.y : pos.y;
        float frontX = col != null ? (dir.x > 0 ? col.bounds.max.x : col.bounds.min.x) : pos.x;
        float headY = col != null ? col.bounds.max.y : pos.y + wallCheckHeightOffset;

        // Chừa 0.02 để tia không bắt đầu ĐÚNG trên mặt collider của chính mình.
        Vector2 front = new Vector2(frontX + dir.x * 0.02f, footY);
        float reach = wallCheckDistance;

        // Tia thấp: ngay trên mặt sàn đang đứng (0.1 để không quét trúng chính sàn đó).
        bool lowHit = Phys.Raycast(front + Vector2.up * 0.1f, dir, reach, obstacleLayer).collider != null;

        if (!lowHit)
        {
            // Chân thông nhưng thân có thể vẫn cấn (mỏm đá thấp lơ lửng) → quét thêm tia giữa thân.
            float midY = Mathf.Min(footY + Mathf.Max(0.1f, wallCheckHeightOffset), headY - 0.05f);
            bool midHit = Phys.Raycast(new Vector2(front.x, midY), dir, reach, obstacleLayer).collider != null;
            return midHit ? PathObstacle.Wall : PathObstacle.None;
        }

        // Tia cao ở NGAY TRÊN mức bậc tối đa: nếu thông → bậc leo được.
        bool highHit = Phys.Raycast(front + Vector2.up * (stepUpMaxHeight + 0.1f), dir, reach, obstacleLayer).collider != null;
        if (highHit) return PathObstacle.Wall;

        // Còn phải có CHỖ ĐỨNG phía trên bậc, không thì nhún lên rồi rơi ngược lại.
        float bodyHalfW = col != null ? col.bounds.extents.x : 0.3f;
        Vector2 landingProbe = new Vector2(front.x + dir.x * Mathf.Max(reach, bodyHalfW + 0.2f), footY + stepUpMaxHeight + 0.1f);
        bool hasFloorAtLanding = Phys.Raycast(landingProbe, Vector2.down, stepUpMaxHeight + 0.4f, obstacleLayer).collider != null;

        return hasFloorAtLanding ? PathObstacle.StepUp : PathObstacle.Wall;
    }

    /// <summary>
    /// Kiểm tra vật cản ĐƠN GIẢN (1 tia giữa thân) — dùng cho Patrol/về ngủ: chỉ cần biết "có nên
    /// quay đầu không". Chase dùng ProbePath để còn phân biệt bậc leo được.
    /// </summary>
    private bool IsPathBlocked(float dirX)
    {
        if (obstacleLayer.value == 0) return false;
        Vector2 origin = new Vector2(transform.position.x, transform.position.y + wallCheckHeightOffset);
        return Phys.Raycast(origin, new Vector2(dirX > 0 ? 1f : -1f, 0f), wallCheckDistance, obstacleLayer).collider != null;
    }

    private float PickRandomPatrolX()
    {
        float minX = startPosition.x - patrolRadius;
        float maxX = startPosition.x + patrolRadius;
        return Random.Range(minX, maxX);
    }

    // PUBLIC API — Cho các script khác gọi

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

    public virtual void NotifyRevived()
    {
        if (!HasStateAuthority) return;
        currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);
        cachedChasePlayer = default;
        playerTarget = null;
        noPlayerTimer = 0f;
        _sightMemoryTimer = 0f;
        _chaseBlockedTimer = 0f;
        _stepUpCooldownTimer = 0f;
        _isStepUpHop = false;
        IsJumping = false;

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


    /// <summary>
    /// Tìm vị trí ngủ, LẤY SPAWN LÀM CHUẨN:
    /// - Quái BAY → raycast LÊN tìm trần để treo ngủ (sleepOnCeiling). Không thấy trần → ngủ tại spawn.
    /// - Quái ĐẤT → raycast XUỐNG tìm sàn để nằm ngủ. Không thấy sàn → ngủ tại spawn.
    /// Tránh quái đất ngủ lơ lửng hoặc quái bay treo giữa không trung.
    /// </summary>
    private Vector2 FindSleepPosition()
    {
        // Quái bay theo cờ sleepOnCeiling; quái đất LUÔN tìm sàn (xuống dưới).
        bool toCeiling = isFlying && sleepOnCeiling;
        Vector2 rayDir = toCeiling ? Vector2.up : Vector2.down;

        LayerMask surfaceMask = sleepSurfaceLayer.value != 0 ? sleepSurfaceLayer : obstacleLayer;
        if (surfaceMask.value == 0) return startPosition; // không có layer hợp lệ

        RaycastHit2D hitSurface = Phys.Raycast(startPosition, rayDir, 30f, surfaceMask);
        if (hitSurface.collider != null)
        {
            float offset = toCeiling ? -0.3f : 0.5f; // treo dưới trần / nằm trên sàn
            return hitSurface.point + new Vector2(0f, offset);
        }
        return startPosition;
    }

    // GIZMOS — Debug visualization

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

        // Tia quét tường - MAGENTA (tia giữa thân)
        Gizmos.color = Color.magenta;
        Vector2 wallOrigin = new Vector2(transform.position.x, transform.position.y + wallCheckHeightOffset);
        Gizmos.DrawRay(wallOrigin, Vector2.right * wallCheckDistance);
        Gizmos.DrawRay(wallOrigin, Vector2.left * wallCheckDistance);

        // Cặp tia phân loại bậc/tường: THẤP (sát chân) + CAO (mức bậc tối đa) — xem ProbePath.
        // Trúng tia thấp mà tia cao thông = bậc leo được.
        // Gốc tia lấy từ BOUNDS collider thân (giống ProbePath) — pivot quái nằm giữa thân nên
        // vẽ theo pivot sẽ lệch so với tia thật.
        if (!isFlying)
        {
            var gcol = BodyCollider;
            float gFootY = gcol != null ? gcol.bounds.min.y : transform.position.y;
            float gRightX = gcol != null ? gcol.bounds.max.x : transform.position.x;
            float gLeftX = gcol != null ? gcol.bounds.min.x : transform.position.x;

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.9f);
            Gizmos.DrawRay(new Vector2(gRightX, gFootY + 0.1f), Vector2.right * wallCheckDistance);
            Gizmos.DrawRay(new Vector2(gLeftX, gFootY + 0.1f), Vector2.left * wallCheckDistance);

            Gizmos.color = new Color(1f, 0.35f, 0f, 0.9f);
            float gHighY = gFootY + stepUpMaxHeight + 0.1f;
            Gizmos.DrawRay(new Vector2(gRightX, gHighY), Vector2.right * wallCheckDistance);
            Gizmos.DrawRay(new Vector2(gLeftX, gHighY), Vector2.left * wallCheckDistance);
        }

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