using Attrition.Controllers;
using Fusion;
using UnityEngine;

public class EnemyAI : NetworkBehaviour
{
    [Header("---- REFS ----")]
    [SerializeField] private EnemyAnimation animationComp;
    [SerializeField] private EnemyCombat combatComp;
    [SerializeField] private EnemyController controller;
    private Rigidbody2D rb;

    [Header("---- SETTINGS ----")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5f;
    public float viewRadius = 5f;
    [Tooltip("Tuần tra ngẫu nhiên theo trục X quanh điểm spawn.")]
    public float patrolRadius = 3f;
    [Tooltip("Đánh dấu nếu quái là loại bay (di chuyển cả trục Y khi đuổi)")]
    public bool isFlying = false;

    [Header("---- OBSTACLE DETECTION ----")]
    public LayerMask obstacleLayer;
    [Tooltip("Độ dài tia laser quét tường phía trước")]
    public float wallCheckDistance = 0.8f;
    [Tooltip("Độ cao của tia laser so với mặt đất (dời lên để không quét trúng sàn nhà)")]
    public float wallCheckHeightOffset = 0.5f;

    [Header("---- TELEPORT ----")]
    [Tooltip("Bật nếu quái có khả năng teleport (dịch chuyển ngẫu nhiên, không tấn công)")]
    public bool canTeleport = false;
    [Tooltip("Cooldown giữa 2 lần teleport (giây)")]
    public float teleportCooldown = 5f;
    [Tooltip("Khoảng cách teleport tối thiểu")]
    public float teleportMinDistance = 2f;
    [Tooltip("Khoảng cách teleport tối đa")]
    public float teleportMaxDistance = 5f;
    [Tooltip("Thời gian hoàn tất teleport (chờ animation xong rồi mới dịch chuyển - giây)")]
    public float teleportDuration = 0.4f;
    [Tooltip("Chỉ teleport khi player trong khoảng cách này (0 = luôn teleport khi chase)")]
    public float teleportTriggerRange = 0f;

    private Vector2 startPosition;
    private Vector2 currentTarget;
    private Transform playerTarget;
    private bool isChasing;
    private PlayerRef cachedChasePlayer;

    [HideInInspector][Networked] public float NetSpeed { get; set; }
    [HideInInspector][Networked] public float NetFacingDir { get; set; } = 1f;
    [HideInInspector][Networked] public NetworkBool IsTeleporting { get; set; }
    [Networked] private TickTimer teleportCooldownTimer { get; set; }
    [Networked] private TickTimer teleportActiveTimer { get; set; }
    [Networked] private Vector2 teleportTargetPos { get; set; }

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;

        // SỬA LỖI ĐỨNG YÊN: Ép điểm tuần tra đầu tiên phải cách xa điểm spawn để quái di chuyển ngay lập tức
        float randomDir = Random.value > 0.5f ? 1f : -1f;
        float randomDist = Random.Range(1f, patrolRadius);
        currentTarget = new Vector2(startPosition.x + randomDir * randomDist, startPosition.y);

        cachedChasePlayer = default;
    }

    public override void Render()
    {
        if (controller == null) return;

        if (controller.isDeadNetworked || controller.IsAwaitingRevive)
        {
            animationComp.UpdateSpeed(0f);
            return;
        }

        animationComp.UpdateSpeed(NetSpeed);
        animationComp.FaceDirection(NetFacingDir);
    }

    public void RunAILogic()
    {
        if (controller.IsKnockbackActive)
        {
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
            return;
        }

        // Khi đang tấn công:
        // - Nếu đang dash → lao về phía player
        // - Nếu không dash → đứng yên như bình thường
        if (combatComp.IsAttacking)
        {
            if (combatComp.IsDashAttacking)
            {
                // Lao về phía player với tốc độ dashSpeed
                Vector2 dashDir = combatComp.DashDirection;
                if (isFlying)
                    rb.linearVelocity = dashDir * combatComp.dashSpeed;
                else
                    rb.linearVelocity = new Vector2(dashDir.x * combatComp.dashSpeed, rb.linearVelocity.y);
                
                NetSpeed = Mathf.Abs(rb.linearVelocity.x);
            }
            else
            {
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
                NetSpeed = 0f;
            }
            return;
        }

        // Khi đang teleport → đứng yên chờ animation xong rồi dịch chuyển
        if (IsTeleporting)
        {
            rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;

            if (teleportActiveTimer.ExpiredOrNotRunning(Runner))
            {
                // Animation xong → dịch chuyển tới vị trí mới
                transform.position = new Vector3(teleportTargetPos.x, teleportTargetPos.y, transform.position.z);
                IsTeleporting = false;
                teleportActiveTimer = TickTimer.None;
                teleportCooldownTimer = TickTimer.CreateFromSeconds(Runner, teleportCooldown);
            }
            return;
        }

        bool previouslyChasing = isChasing;

        FindPlayer();

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

            if (dist <= combatComp.attackRange)
            {
                // Trong tầm đánh → dừng lại và quay mặt về phía Player
                rb.linearVelocity = isFlying ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
                if (Mathf.Abs(xDiff) > 0.05f) NetFacingDir = dirX;

                // Đánh nếu hết cooldown
                if (combatComp.CanAttack())
                {
                    if (combatComp.isDashAttack)
                    {
                        // Dash attack: lao về phía player
                        Vector2 dashDir = (playerTarget.position - transform.position).normalized;
                        combatComp.AttemptDashAttack(dashDir);
                    }
                    else
                    {
                        combatComp.AttemptAttack();
                    }
                }
                // Teleport: kiểm tra nếu đang chờ cooldown attack, có thể teleport ngẫu nhiên
                else if (canTeleport && CanTeleport(dist))
                {
                    ExecuteTeleport();
                }
            }
            else
            {
                // Ngoài tầm đánh → đuổi theo Player (kể cả đang chờ cooldown)
                // Teleport khi đang đuổi và cooldown sẵn sàng
                if (canTeleport && CanTeleport(dist))
                {
                    ExecuteTeleport();
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
        }
        else
        {
            Patrol();
        }

        NetSpeed = Mathf.Abs(rb.linearVelocity.x);
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
        if (playerTarget != null)
            NetFacingDir = playerTarget.position.x > transform.position.x ? 1f : -1f;
    }

    public void NotifyRevived()
    {
        if (!HasStateAuthority) return;
        currentTarget = new Vector2(PickRandomPatrolX(), isFlying ? startPosition.y : transform.position.y);
        cachedChasePlayer = default;
        isChasing = false;
        playerTarget = null;
        IsTeleporting = false;
    }

    // ═══════════════════════════════════════════════════════════════
    // TELEPORT
    // ═══════════════════════════════════════════════════════════════
    private bool CanTeleport(float distToPlayer)
    {
        if (!canTeleport || IsTeleporting) return false;
        if (!teleportCooldownTimer.ExpiredOrNotRunning(Runner)) return false;
        if (teleportTriggerRange > 0 && distToPlayer > teleportTriggerRange) return false;
        return true;
    }

    private void ExecuteTeleport()
    {
        if (playerTarget == null) return;

        // Tìm vị trí teleport ngẫu nhiên quanh player
        Vector2 playerPos = playerTarget.position;
        float randomAngle = Random.Range(0f, 360f);
        float randomDist = Random.Range(teleportMinDistance, teleportMaxDistance);
        Vector2 offset = new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), Mathf.Sin(randomAngle * Mathf.Deg2Rad)) * randomDist;
        Vector2 targetPos = playerPos + offset;

        // Nếu không bay, giữ nguyên Y (để không teleport lên trời)
        if (!isFlying)
        {
            targetPos.y = transform.position.y;
            // Chỉ teleport theo trục X: ngẫu nhiên trái/phải player
            float randomSide = Random.value > 0.5f ? 1f : -1f;
            targetPos = new Vector2(playerPos.x + randomSide * randomDist, transform.position.y);
        }

        // Bắt đầu teleport
        IsTeleporting = true;
        teleportTargetPos = targetPos;
        teleportActiveTimer = TickTimer.CreateFromSeconds(Runner, teleportDuration);

        // Quay mặt về hướng player trước khi teleport
        float xDiff = playerTarget.position.x - transform.position.x;
        if (Mathf.Abs(xDiff) > 0.05f) NetFacingDir = xDiff > 0 ? 1f : -1f;

        // Phát animation teleport trên tất cả clients
        RPC_PlayTeleportAnim();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTeleportAnim()
    {
        if (animationComp != null) animationComp.PlayTeleport();
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
    }
}