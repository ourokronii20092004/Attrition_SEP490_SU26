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
    public LayerMask obstacleLayer;

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
        currentTarget = new Vector2(PickRandomPatrolX(), transform.position.y);
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
        if (combatComp.IsAttacking || controller.IsKnockbackActive)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
            return;
        }

        FindPlayer();

        if (isChasing && playerTarget != null)
        {
            currentTarget = playerTarget.position;
            float dist = Vector2.Distance(transform.position, currentTarget);

            if (dist <= combatComp.attackRange)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                NetFacingDir = currentTarget.x > transform.position.x ? 1f : -1f;
                combatComp.AttemptAttack();
            }
            else
            {
                MoveTowards(currentTarget, chaseSpeed);
            }
        }
        else
        {
            Patrol();
        }

        NetSpeed = Mathf.Abs(rb.linearVelocity.x);
    }

    private void FindPlayer()
    {
        if (TryUseCachedChaseTarget())
            return;

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
        if (!cachedChasePlayer.IsRealPlayer)
            return false;

        NetworkObject pObj = Runner.GetPlayerObject(cachedChasePlayer);
        if (pObj == null)
            return false;

        PlayerController pController = pObj.GetComponent<PlayerController>();
        if (pController == null || pController.IsDead)
            return false;

        float dst = Vector2.Distance(transform.position, pObj.transform.position);
        if (dst > viewRadius * 1.05f)
            return false;

        playerTarget = pObj.transform;
        isChasing = true;
        return true;
    }

    private void Patrol()
    {
        if (patrolRadius <= 0.05f)
        {
            MoveTowards(startPosition, patrolSpeed);
            return;
        }

        if (Mathf.Abs(transform.position.x - currentTarget.x) < 0.25f)
            currentTarget = new Vector2(PickRandomPatrolX(), transform.position.y);

        MoveTowards(currentTarget, patrolSpeed);
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

    public void ForceFacePlayer()
    {
        if (playerTarget != null)
            NetFacingDir = playerTarget.position.x > transform.position.x ? 1f : -1f;
    }

    /// <summary>Gọi từ StateAuthority sau khi hồi sinh để tránh đứng yên tại chỗ chết.</summary>
    public void NotifyRevived()
    {
        if (!HasStateAuthority) return;
        currentTarget = new Vector2(PickRandomPatrolX(), transform.position.y);
        cachedChasePlayer = default;
        isChasing = false;
        playerTarget = null;
    }
}
