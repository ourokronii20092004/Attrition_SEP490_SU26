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

    [Header("---- OBSTACLE DETECTION ----")]
    public LayerMask obstacleLayer;
    [Tooltip("Độ dài tia laser quét tường phía trước")]
    public float wallCheckDistance = 0.8f;
    [Tooltip("Độ cao của tia laser so với mặt đất (dời lên để không quét trúng sàn nhà)")]
    public float wallCheckHeightOffset = 0.5f;

    private Vector2 startPosition;
    private Vector2 currentTarget;
    private Transform playerTarget;
    private bool isChasing;
    private PlayerRef cachedChasePlayer;

    [Networked] public float NetSpeed { get; set; }
    [Networked] public float NetFacingDir { get; set; } = 1f;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;

        // SỬA LỖI ĐỨNG YÊN: Ép điểm tuần tra đầu tiên phải cách xa điểm spawn để quái di chuyển ngay lập tức
        float randomDir = Random.value > 0.5f ? 1f : -1f;
        float randomDist = Random.Range(1f, patrolRadius);
        currentTarget = new Vector2(startPosition.x + randomDir * randomDist, transform.position.y);

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

        if (combatComp.IsAttacking)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
            return;
        }

        bool previouslyChasing = isChasing;

        FindPlayer();

        if (previouslyChasing && !isChasing)
        {
            // Vừa mất dấu người chơi -> Điểm đến mới quay về khu vực tuần tra
            currentTarget = new Vector2(PickRandomPatrolX(), transform.position.y);
        }

        if (isChasing && playerTarget != null)
        {
            currentTarget = playerTarget.position;
            float dist = Vector2.Distance(transform.position, currentTarget);
            float dirX = currentTarget.x > transform.position.x ? 1f : -1f;

            if (dist <= combatComp.attackRange)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                NetFacingDir = currentTarget.x > transform.position.x ? 1f : -1f;
                combatComp.AttemptAttack();
            }
            else
            {
                // Đang đuổi mà vướng tường -> Dừng lại đứng nhìn, không cắm mặt húc tường nữa
                if (IsPathBlocked(dirX))
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    NetFacingDir = dirX;
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
        if (IsPathBlocked(dirX))
        {
            // Lấy 1 điểm an toàn ở hướng ngược lại
            float newTargetX = transform.position.x - (dirX * Random.Range(1f, patrolRadius));
            newTargetX = Mathf.Clamp(newTargetX, startPosition.x - patrolRadius, startPosition.x + patrolRadius);

            currentTarget = new Vector2(newTargetX, transform.position.y);
            dirX = currentTarget.x > transform.position.x ? 1f : -1f; // Cập nhật lại hướng
        }

        if (Mathf.Abs(transform.position.x - currentTarget.x) < 0.25f)
            currentTarget = new Vector2(PickRandomPatrolX(), transform.position.y);

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
        rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
        NetFacingDir = dirX;
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
        currentTarget = new Vector2(PickRandomPatrolX(), transform.position.y);
        cachedChasePlayer = default;
        isChasing = false;
        playerTarget = null;
    }
}