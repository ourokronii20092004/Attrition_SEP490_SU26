using Attrition.Controllers;
using Fusion;
using UnityEngine;

public class EnemyAI : NetworkBehaviour
{
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
    [Tooltip("Bật nếu quái thức dậy khi Player chạm collider (Mimic), thay vì khi Player vào tầm nhìn (Bat)")]
    public bool sleepWakeOnTouch = false;
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

    private Vector2 startPosition;
    private Vector2 sleepPosition; // Vị trí ngủ (có thể khác startPosition nếu sleepOnCeiling)
    private Vector2 currentTarget;
    private Transform playerTarget;
    private bool isChasing;
    private PlayerRef cachedChasePlayer;

    // ─── Sleep state ───
    [HideInInspector][Networked] public NetworkBool IsSleeping { get; set; }
    [HideInInspector][Networked] public NetworkBool IsReturningToSleep { get; set; }
    private bool isWakingUp; // local flag chờ animation wakeup xong
    private float noPlayerTimer; // đếm thời gian không thấy player
    private bool localSleepHandled; // tránh gọi anim lặp
    private bool localWakeHandled;

    // ─── Fly Melee retreat state ───
    private bool wasAttackingLastTick;
    [HideInInspector][Networked] public NetworkBool IsRetreatingUp { get; set; }

    [HideInInspector][Networked] public float NetSpeed { get; set; }
    [HideInInspector][Networked] public float NetFacingDir { get; set; } = 1f;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;

        // Tính vị trí ngủ
        if (enableSleep)
        {
            sleepPosition = FindSleepPosition();
            if (HasStateAuthority)
            {
                IsSleeping = true;
                IsReturningToSleep = false;
            }
            noPlayerTimer = 0f;
            isWakingUp = false;
            localSleepHandled = false;
            localWakeHandled = false;
        }
        else
        {
            sleepPosition = startPosition;
        }

        // SỬA LỖI ĐỨNG YÊN: Ép điểm tuần tra đầu tiên phải cách xa điểm spawn để quái di chuyển ngay lập tức
        float randomDir = Random.value > 0.5f ? 1f : -1f;
        float randomDist = Random.Range(1f, patrolRadius);
        currentTarget = new Vector2(startPosition.x + randomDir * randomDist, startPosition.y);

        cachedChasePlayer = default;

        wasAttackingLastTick = false;
        if (HasStateAuthority) IsRetreatingUp = false;
    }

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
            if (IsSleeping && !localSleepHandled)
            {
                animationComp.PlaySleep();
                localSleepHandled = true;
                localWakeHandled = false;
            }
            else if (!IsSleeping && !localWakeHandled && localSleepHandled)
            {
                animationComp.PlayWakeUp();
                localWakeHandled = true;
                localSleepHandled = false;
            }
        }

        animationComp.UpdateSpeed(NetSpeed);
        animationComp.FaceDirection(NetFacingDir);
    }

    public void RunAILogic()
    {
        if (controller.IsKnockbackActive)
        {
            // Reset retreat state khi bị stun
            if (IsRetreatingUp) IsRetreatingUp = false;

            // Bị đánh → tỉnh dậy ngay
            if (enableSleep && IsSleeping)
            {
                IsSleeping = false;
                IsReturningToSleep = false;
                isWakingUp = false;
                noPlayerTimer = 0f;
            }
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
            return;
        }

        // ─── SLEEP LOGIC ───
        if (enableSleep)
        {
            // Đang ngủ → đứng yên
            if (IsSleeping)
            {
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
                NetSpeed = 0f;

                if (sleepWakeOnTouch)
                {
                    // Mimic: Chờ WakeUpFromTouch() được gọi từ MimicSleepTrigger
                    // Không dùng FindPlayer() khi đang ngủ
                }
                else
                {
                    // Bat: Kiểm tra player có trong tầm nhìn không
                    FindPlayer();
                    if (isChasing && playerTarget != null)
                    {
                        // Thức dậy!
                        IsSleeping = false;
                        IsReturningToSleep = false;
                        isWakingUp = true;
                        noPlayerTimer = 0f;
                    }
                }
                return;
            }

            // Đang bay về vị trí ngủ
            if (IsReturningToSleep)
            {
                float distToSleep = Vector2.Distance(transform.position, sleepPosition);
                if (distToSleep < 0.3f)
                {
                    // Đã về đến nơi → ngủ
                    transform.position = new Vector3(sleepPosition.x, sleepPosition.y, transform.position.z);
                    rb.linearVelocity = Vector2.zero;
                    IsSleeping = true;
                    IsReturningToSleep = false;
                    NetSpeed = 0f;
                    cachedChasePlayer = default;
                    playerTarget = null;
                    isChasing = false;
                }
                else
                {
                    // Bay về vị trí ngủ
                    Vector2 dir = (sleepPosition - (Vector2)transform.position).normalized;
                    rb.linearVelocity = dir * returnToSleepSpeed;
                    NetFacingDir = dir.x > 0 ? 1f : -1f;
                    NetSpeed = returnToSleepSpeed;

                    // Nếu bất ngờ thấy player khi đang bay về → tỉnh dậy đuổi
                    FindPlayer();
                    if (isChasing && playerTarget != null)
                    {
                        IsReturningToSleep = false;
                        noPlayerTimer = 0f;
                    }
                }
                return;
            }

            // WakeUp animation delay (chờ ~0.4s cho animation wakeup)
            if (isWakingUp)
            {
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
                NetSpeed = 0f;
                noPlayerTimer += Runner.DeltaTime;
                if (noPlayerTimer >= 0.4f)
                {
                    isWakingUp = false;
                    noPlayerTimer = 0f;
                    // SỬA: Reset cache để FindPlayer() chạy lại fresh trong AI logic bên dưới
                    cachedChasePlayer = default;
                    playerTarget = null;
                    isChasing = false;
                }
                return;
            }

            // Đang thức → kiểm tra player và đếm thời gian không thấy player
            // SỬA: Phải gọi FindPlayer() ở đây để cập nhật isChasing trước khi check timer
            FindPlayer();
            if (!isChasing)
            {
                noPlayerTimer += Runner.DeltaTime;
                if (noPlayerTimer >= sleepReturnDelay)
                {
                    // Hết thời gian chờ → bay về ngủ
                    IsReturningToSleep = true;
                    noPlayerTimer = 0f;
                    return;
                }
            }
            else
            {
                noPlayerTimer = 0f;
            }
            // Không return ở đây — cho phép rơi xuống AI logic bình thường bên dưới
        }

        // ─── Khi đang heal (elite) → đứng yên ───
        if (eliteSkills != null && eliteSkills.IsHealing)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;

            // Cập nhật healing timer
            eliteSkills.UpdateHealing();
            return;
        }

        // ─── Khi đang teleport (elite) → đứng yên chờ ───
        if (eliteSkills != null && eliteSkills.IsTeleporting)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;

            eliteSkills.UpdateTeleport();
            return;
        }

        // Khi đang tấn công:
        if (combatComp.IsAttacking)
        {
            wasAttackingLastTick = true;
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
            return;
        }

        // ─── FLY MELEE RETREAT: Vừa đánh xong → bay lên cao ───
        if (flyMeleeRetreat && isFlying && wasAttackingLastTick)
        {
            wasAttackingLastTick = false;
            IsRetreatingUp = true;
        }

        if (IsRetreatingUp && flyMeleeRetreat && isFlying)
        {
            // Vị trí retreat: bay lên cao hợp lý, tránh là đà dưới đất
            float retreatY;
            float retreatX = transform.position.x;

            if (playerTarget != null)
            {
                // Lấy điểm cao nhất giữa player và spawn, rồi cộng altitude
                float baseY = Mathf.Max(playerTarget.position.y, startPosition.y);
                retreatY = baseY + flyMeleeRetreatAltitude;
                retreatX = playerTarget.position.x;
            }
            else
            {
                retreatY = startPosition.y + flyMeleeRetreatAltitude;
            }

            // Clamp tối thiểu: luôn bay cao hơn điểm spawn ít nhất 1 unit
            retreatY = Mathf.Max(retreatY, startPosition.y + 1f);

            // Chỉ cần bay đủ cao → xong
            if (transform.position.y >= retreatY - 0.3f)
            {
                // Đã lên cao đủ → tiếp tục AI bình thường (sẽ lao xuống đánh tiếp)
                IsRetreatingUp = false;
            }
            else
            {
                // Bay lên vị trí retreat
                Vector2 retreatTarget = new Vector2(retreatX, retreatY);
                Vector2 dir = (retreatTarget - (Vector2)transform.position).normalized;
                rb.linearVelocity = dir * flyMeleeRetreatSpeed;
                NetFacingDir = playerTarget != null
                    ? (playerTarget.position.x > transform.position.x ? 1f : -1f)
                    : NetFacingDir;
                NetSpeed = flyMeleeRetreatSpeed;
                return;
            }
        }

        bool previouslyChasing = isChasing;

        // SỬA: Nếu enableSleep thì FindPlayer() đã được gọi ở sleep block rồi, không cần gọi lại
        if (!enableSleep)
        {
            FindPlayer();
        }
        if (previouslyChasing && !isChasing)
        {
            currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);
        }

        if (isChasing && playerTarget != null)
        {
            currentTarget = playerTarget.position;
            float dist = Vector2.Distance(transform.position, currentTarget);
            float xDiff = currentTarget.x - transform.position.x;
            float dirX = xDiff > 0 ? 1f : -1f;

            if (dist <= combatComp.MaxAttackRange)
            {
                // Trong tầm đánh → dừng lại và quay mặt về phía Player
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
                if (Mathf.Abs(xDiff) > 0.05f) NetFacingDir = dirX;

                // Đánh nếu hết cooldown
                if (combatComp.CanAttack())
                {
                    ExecuteAttack(dirX);
                }
                // Elite: teleport khi đang chờ cooldown attack
                else if (eliteSkills != null)
                {
                    eliteSkills.TryTeleport(dist, playerTarget);
                }
            }
            else
            {
                // Ngoài tầm đánh → đuổi theo Player
                // Elite: teleport khi đang đuổi
                if (eliteSkills != null && eliteSkills.TryTeleport(dist, playerTarget))
                {
                    // Đã bắt đầu teleport
                }
                else if (!isFlying && IsPathBlocked(dirX))
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    if (Mathf.Abs(xDiff) > 0.05f) NetFacingDir = dirX;
                }
                else
                {
                    MoveTowards(currentTarget, chaseSpeed);
                }
            }

            // Elite: roll heal ngẫu nhiên khi đang chase (nhưng không trong tầm đánh)
            if (eliteSkills != null && !combatComp.IsAttacking && dist > combatComp.MaxAttackRange)
            {
                eliteSkills.TryRandomHeal(controller.CurrentHealth, controller.maxHealth);
            }
        }
        else
        {
            // SỬA: Quái enableSleep không patrol, đứng yên chờ (sleep timer sẽ xử lý)
            if (enableSleep)
            {
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            }
            else
            {
                Patrol();
            }

            // Elite: roll heal ngẫu nhiên khi patrol
            if (eliteSkills != null)
            {
                eliteSkills.TryRandomHeal(controller.CurrentHealth, controller.maxHealth);
            }
        }

        NetSpeed = Mathf.Abs(rb.linearVelocity.x);
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

    private void Patrol()
    {
        if (patrolRadius <= 0.05f)
        {
            MoveTowards(startPosition, patrolSpeed);
            return;
        }

        float dirX = currentTarget.x > transform.position.x ? 1f : -1f;

        // SỬA LỖI CẮM MẶT VÀO TƯỜNG: Nếu đụng tường, lập tức quay đầu
        if (!isFlying && IsPathBlocked(dirX))
        {
            // Lấy 1 điểm an toàn ở hướng ngược lại
            float newTargetX = transform.position.x - (dirX * Random.Range(1f, patrolRadius));
            newTargetX = Mathf.Clamp(newTargetX, startPosition.x - patrolRadius, startPosition.x + patrolRadius);

            currentTarget = new Vector2(newTargetX, isFlying ? startPosition.y : transform.position.y);
            dirX = currentTarget.x > transform.position.x ? 1f : -1f; // Cập nhật lại hướng
        }

        if (Mathf.Abs(transform.position.x - currentTarget.x) < 0.25f)
            currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);

        MoveTowards(currentTarget, patrolSpeed);
    }

    private bool IsPathBlocked(float dirX)
    {
        // Bắn tia raycast ngang từ ngực/bụng quái ra phía trước
        Vector2 origin = new Vector2(transform.position.x, transform.position.y + wallCheckHeightOffset);
        Vector2 direction = new Vector2(dirX, 0);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, wallCheckDistance, obstacleLayer);

        // Vẽ tia đỏ trong tab Scene để bạn dễ debug
        Debug.DrawRay(origin, direction * wallCheckDistance, Color.red);

        return hit.collider != null;
    }

    private float PickRandomPatrolX()
    {
        float minX = startPosition.x - patrolRadius;
        float maxX = startPosition.x + patrolRadius;
        return Random.Range(minX, maxX);
    }

    private void MoveTowards(Vector2 target, float speed)
    {
        float dirX = target.x > transform.position.x ? 1f : -1f;
        NetFacingDir = dirX;

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

    private void FindPlayer()
    {
        if (TryUseCachedChaseTarget()) return;

        cachedChasePlayer = default;
        playerTarget = null;
        isChasing = false;

        foreach (var player in Runner.ActivePlayers)
        {
            NetworkObject pObj = Runner.GetPlayerObject(player);
            if (pObj == null) continue;

            PlayerController pController = pObj.GetComponent<PlayerController>();
            if (pController == null || pController.IsDead) continue;

            float dst = Vector2.Distance(transform.position, pObj.transform.position);
            if (dst > viewRadius) continue;

            playerTarget = pObj.transform;
            isChasing = true;
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
        isChasing = true;
        return true;
    }

    public void ForceFacePlayer()
    {
        // SỬA: Không quay mặt khi đang tấn công (giữ hướng đánh)
        if (combatComp != null && combatComp.IsAttacking) return;
        if (playerTarget != null)
            NetFacingDir = playerTarget.position.x > transform.position.x ? 1f : -1f;
    }

    /// <summary>
    /// Gọi bởi MimicSleepTrigger khi Player chạm collider của Mimic.
    /// Chỉ hoạt động khi sleepWakeOnTouch = true.
    /// </summary>
    public void WakeUpFromTouch()
    {
        if (!enableSleep || !sleepWakeOnTouch) return;
        if (!IsSleeping) return;

        IsSleeping = false;
        IsReturningToSleep = false;
        isWakingUp = true;
        noPlayerTimer = 0f;
    }

    public void NotifyRevived()
    {
        if (!HasStateAuthority) return;
        currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);
        cachedChasePlayer = default;
        isChasing = false;
        playerTarget = null;

        // Reset sleep state khi hồi sinh
        if (enableSleep)
        {
            IsSleeping = false;
            IsReturningToSleep = false;
            isWakingUp = false;
            noPlayerTimer = 0f;
        }
    }

    /// <summary>
    /// Tìm vị trí ngủ: Raycast lên trần (hoặc xuống sàn) từ điểm spawn.
    /// Mimic (sleepWakeOnTouch) luôn ngủ dưới đất, raycast xuống sàn.
    /// Nếu không tìm được surface → dùng startPosition.
    /// </summary>
    private Vector2 FindSleepPosition()
    {
        // Mimic: luôn ngủ dưới đất, bỏ qua sleepOnCeiling
        if (sleepWakeOnTouch)
        {
            if (sleepSurfaceLayer == 0)
                return startPosition;

            // Raycast xuống sàn từ điểm spawn
            RaycastHit2D hit = Physics2D.Raycast(startPosition, Vector2.down, 20f, sleepSurfaceLayer);
            if (hit.collider != null)
            {
                return hit.point + new Vector2(0f, 0.3f); // Dời lên 1 chút khỏi sàn
            }
            return startPosition;
        }

        if (sleepSurfaceLayer == 0)
            return startPosition;

        Vector2 rayDir = sleepOnCeiling ? Vector2.up : Vector2.down;
        RaycastHit2D hitSurface = Physics2D.Raycast(startPosition, rayDir, 20f, sleepSurfaceLayer);
        if (hitSurface.collider != null)
        {
            // Dời ra 1 chút khỏi bề mặt để không chìm vào
            float offset = sleepOnCeiling ? -0.3f : 0.3f;
            return hitSurface.point + new Vector2(0f, offset);
        }
        return startPosition;
    }

    void OnDrawGizmosSelected()
    {
        // Vòng tròn tầm nhìn (View Radius) - CYAN
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        // Tô mờ bên trong
        Gizmos.color = new Color(0f, 1f, 1f, 0.05f);
        Gizmos.DrawSphere(transform.position, viewRadius);

        // Vùng tuần tra (Patrol Radius) - GREEN
        Vector2 spawnPos = Application.isPlaying ? startPosition : (Vector2)transform.position;
        if (patrolRadius > 0.05f)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(spawnPos, patrolRadius);
        }

        // Đường đến mục tiêu hiện tại - RED (chỉ khi đang chạy)
        if (Application.isPlaying && isChasing && playerTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, playerTarget.position);
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