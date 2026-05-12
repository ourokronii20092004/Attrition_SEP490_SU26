using Fusion;
using UnityEngine;

public class EnemyAI : NetworkBehaviour
{
    [Header("---- REFS ----")]
    [SerializeField] private EnemyAnimation animationComp;
    [SerializeField] private EnemyCombat combatComp;
    [SerializeField] private AxeDemonController controller;
    private Rigidbody2D rb;

    [Header("---- SETTINGS ----")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5f;
    public float viewRadius = 5f;
    public LayerMask obstacleLayer;

    private Vector2 startPosition;
    private Vector2 currentTarget;
    private Transform playerTarget;
    private bool isChasing = false;

    [Networked] public float NetSpeed { get; set; }
    [Networked] public float NetFacingDir { get; set; } = 1f;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
        currentTarget = startPosition + Vector2.right * 2f;
    }

    public override void Render()
    {
        animationComp.UpdateSpeed(NetSpeed);
        animationComp.FaceDirection(NetFacingDir);
    }

    public void RunAILogic()
    {
        if (combatComp.IsAttacking || controller.IsKnockbackActive)
        {
            rb.linearVelocity = Vector2.zero;
            NetSpeed = 0; // Cập nhật biến Networked
            return;
        }

        FindPlayer();

        if (isChasing && playerTarget != null)
        {
            currentTarget = playerTarget.position;
            float dist = Vector2.Distance(transform.position, currentTarget);

            if (dist <= combatComp.attackRange)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                NetFacingDir = (currentTarget.x > transform.position.x) ? 1f : -1f; // Quay mặt về player
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

        // Cập nhật tốc độ để Client thấy
        NetSpeed = Mathf.Abs(rb.linearVelocity.x);
    }

    private void FindPlayer()
    {
        playerTarget = null;
        isChasing = false;

        foreach (var player in Runner.ActivePlayers)
        {
            NetworkObject pObj = Runner.GetPlayerObject(player);
            if (pObj != null)
            {
                PlayerController pController = pObj.GetComponent<PlayerController>();
                if (pController != null && !pController.IsDead)
                {
                    float dst = Vector2.Distance(transform.position, pObj.transform.position);
                    if (dst <= viewRadius)
                    {
                        playerTarget = pObj.transform;
                        isChasing = true;
                        break;
                    }
                }
            }
        }
    }

    private void Patrol()
    {
        if (Mathf.Abs(transform.position.x - currentTarget.x) < 0.2f)
        {
            currentTarget = startPosition + (currentTarget.x > startPosition.x ? Vector2.left : Vector2.right) * 2f;
        }
        MoveTowards(currentTarget, patrolSpeed);
    }

    private void MoveTowards(Vector2 target, float speed)
    {
        float dirX = (target.x > transform.position.x) ? 1f : -1f;
        rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
        NetFacingDir = dirX; // Cập nhật hướng cho Client biết
    }

    public void ForceFacePlayer()
    {
        if (playerTarget != null)
        {
            NetFacingDir = (playerTarget.position.x > transform.position.x) ? 1f : -1f;
        }
    }
}