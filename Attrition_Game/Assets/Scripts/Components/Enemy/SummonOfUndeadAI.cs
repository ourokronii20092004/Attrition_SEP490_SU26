using Attrition.Controllers;
using Fusion;
using UnityEngine;

/// <summary>
/// AI đơn giản cho Summon Of Undead — quái bay lơ lửng được triệu hồi bởi Undead.
/// Lifecycle: Appear → Chase Player → Contact Damage → Chết khi Undead chết hoặc bị giết.
/// 
/// Components cần gắn trên prefab:
/// - SummonOfUndeadAI (script này)
/// - EnemyAnimation (reuse)
/// - EnemyContactDamage (gắn trên child object có Collider2D trigger)
/// - Rigidbody2D (gravityScale = 0)
/// - NetworkObject + NetworkRigidbody2D
/// - Collider2D (physics body)
/// </summary>
public class SummonOfUndeadAI : NetworkBehaviour, IDamageable
{
    // ═══════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ═══════════════════════════════════════════════════════════════

    [Header("---- REFS ----")]
    [SerializeField] private EnemyAnimation animationComp;

    [Header("---- SETTINGS ----")]
    [Tooltip("Tốc độ bay đuổi theo player")]
    public float chaseSpeed = 4f;
    [Tooltip("Tầm nhìn phát hiện player")]
    public float viewRadius = 12f;
    [Tooltip("Thời gian animation Appear (giây)")]
    public float appearDuration = 0.5f;
    [Tooltip("HP của Summon (0 = chết ngay khi bị đánh 1 lần)")]
    public int maxHealth = 1;

    [Header("---- FLOATING ----")]
    [Tooltip("Biên độ lơ lửng lên xuống (units)")]
    public float floatAmplitude = 0.15f;
    [Tooltip("Tốc độ lơ lửng")]
    public float floatFrequency = 2f;

    [Header("---- PASS-THROUGH (Bay xuyên) ----")]
    [Tooltip("Layers mà Summon bay xuyên qua (tường, sàn, chướng ngại vật...)")]
    public LayerMask passThroughLayers;

    // ═══════════════════════════════════════════════════════════════
    // NETWORKED STATE
    // ═══════════════════════════════════════════════════════════════

    [HideInInspector][Networked] public int Health { get; set; }
    [HideInInspector][Networked] public NetworkBool IsDeadNetworked { get; set; }
    [HideInInspector][Networked] public float NetFacingDir { get; set; } = 1f;
    [Networked] private TickTimer appearTimer { get; set; }
    [Networked] private TickTimer despawnTimer { get; set; }
    [Networked] private NetworkId ownerNetId { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // LOCAL STATE
    // ═══════════════════════════════════════════════════════════════

    private Rigidbody2D rb;
    private Transform playerTarget;
    private float floatBaseY;
    private float floatTimer;
    private bool _localDeathHandled;
    private bool _appearAnimPlayed;

    // IDamageable
    public bool IsDead => IsDeadNetworked;

    // ═══════════════════════════════════════════════════════════════
    // INIT
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Gọi bởi EliteEnemySkills.SpawnSummons() khi spawn.
    /// Truyền reference tới con Undead chủ.
    /// </summary>
    public void InitOwner(NetworkObject owner)
    {
        if (owner != null)
            ownerNetId = owner.Id;
    }

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();

        if (HasStateAuthority)
        {
            Health = maxHealth;
            IsDeadNetworked = false;
            appearTimer = TickTimer.CreateFromSeconds(Runner, appearDuration);
        }

        floatBaseY = transform.position.y;
        floatTimer = 0f;
        _localDeathHandled = false;
        _appearAnimPlayed = false;
        playerTarget = null;

        // Bay xuyên tường + xuyên người chơi (vẫn gây Contact Damage qua child trigger)
        SetupPassThrough();
    }

    /// <summary>
    /// Cấu hình collision: bay xuyên tường, xuyên người chơi.
    /// Main collider giữ non-trigger để player attacks vẫn phát hiện được.
    /// Contact Damage hoạt động qua child trigger collider (EnemyContactDamage).
    /// </summary>
    private void SetupPassThrough()
    {
        Collider2D[] allCols = GetComponentsInChildren<Collider2D>();

        // 1. Bay xuyên tường: exclude obstacle layers khỏi tất cả collider
        foreach (var col in allCols)
        {
            col.excludeLayers |= passThroughLayers;
        }

        // 2. Bay xuyên người chơi: ignore physics collision với player
        //    (child trigger vẫn detect player cho Contact Damage vì OnTriggerStay2D)
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            if (playerCol == null) continue;

            foreach (var myCol in allCols)
            {
                if (!myCol.isTrigger && !playerCol.isTrigger)
                {
                    Physics2D.IgnoreCollision(myCol, playerCol, true);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // RENDER — Animation (tất cả clients)
    // ═══════════════════════════════════════════════════════════════

    public override void Render()
    {
        if (IsDeadNetworked)
        {
            if (!_localDeathHandled)
            {
                HandleDeathVisuals();
                _localDeathHandled = true;
            }
            return;
        }

        // Appear animation (chỉ play 1 lần)
        if (!_appearAnimPlayed)
        {
            if (animationComp != null) animationComp.PlayAppear();
            _appearAnimPlayed = true;
        }

        // Speed & facing
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (animationComp != null)
        {
            animationComp.UpdateSpeed(speed);
            animationComp.FaceDirection(NetFacingDir);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // AI LOGIC — FixedUpdateNetwork (chỉ host)
    // ═══════════════════════════════════════════════════════════════

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Đã chết → chờ despawn
        if (IsDeadNetworked)
        {
            if (despawnTimer.Expired(Runner))
            {
                despawnTimer = TickTimer.None;
                Runner.Despawn(Object);
            }
            return;
        }

        // Kiểm tra owner Undead còn sống không
        if (IsOwnerDead())
        {
            Die();
            return;
        }

        // Appear phase: đứng yên, chờ animation
        if (!appearTimer.ExpiredOrNotRunning(Runner))
        {
            rb.linearVelocity = Vector2.zero;
            // Floating nhẹ khi appear
            ApplyFloating();
            return;
        }

        // Chase phase
        FindPlayer();
        if (playerTarget != null)
        {
            Vector2 dir = ((Vector2)playerTarget.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * chaseSpeed;

            // Update facing
            float xDiff = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > 0.1f)
                NetFacingDir = xDiff > 0 ? 1f : -1f;
        }
        else
        {
            // Không thấy player → lơ lửng tại chỗ
            rb.linearVelocity = Vector2.zero;
        }

        ApplyFloating();
    }

    // ═══════════════════════════════════════════════════════════════
    // DAMAGE — IDamageable
    // ═══════════════════════════════════════════════════════════════

    public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
    {
        if (IsDeadNetworked) return;
        RPC_TakeDamage(damage, knockbackDir, knockbackForce);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
    {
        if (IsDeadNetworked) return;
        Health -= damage;

        if (Health <= 0)
        {
            Die();
        }
        else
        {
            // Hit animation
            RPC_PlayHit();

            // Knockback nhẹ
            if (rb != null)
            {
                knockbackDir.y = 0; // Quái bay → không đẩy dọc
                rb.linearVelocity = knockbackDir.normalized * knockbackForce;
            }
        }
    }

    private void Die()
    {
        IsDeadNetworked = true;
        rb.linearVelocity = Vector2.zero;
        despawnTimer = TickTimer.CreateFromSeconds(Runner, 1.0f);
        RPC_PlayDeath();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHit()
    {
        if (animationComp != null) animationComp.PlayHit();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDeath()
    {
        if (animationComp != null) animationComp.PlayDeath();
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private bool IsOwnerDead()
    {
        if (!ownerNetId.IsValid) return false;

        if (Runner.TryFindObject(ownerNetId, out NetworkObject ownerObj))
        {
            if (ownerObj == null) return true;
            EnemyController ownerCtrl = ownerObj.GetComponent<EnemyController>();
            if (ownerCtrl != null && ownerCtrl.IsDead) return true;
            return false;
        }

        // Owner không tìm thấy → đã bị despawn → tự chết
        return true;
    }

    private void FindPlayer()
    {
        playerTarget = null;
        float closestDist = viewRadius;

        foreach (var player in Runner.ActivePlayers)
        {
            NetworkObject pObj = Runner.GetPlayerObject(player);
            if (pObj == null) continue;

            PlayerController pController = pObj.GetComponent<PlayerController>();
            if (pController == null || pController.IsDead) continue;

            float dst = Vector2.Distance(transform.position, pObj.transform.position);
            if (dst <= closestDist)
            {
                closestDist = dst;
                playerTarget = pObj.transform;
            }
        }
    }

    private void ApplyFloating()
    {
        floatTimer += Runner.DeltaTime;
        float floatOffset = Mathf.Sin(floatTimer * floatFrequency * Mathf.PI * 2f) * floatAmplitude;

        // Chỉ offset Y nhẹ, không thay đổi velocity
        Vector3 pos = transform.position;
        pos.y += floatOffset * Runner.DeltaTime; // Smooth per-tick
        transform.position = pos;
    }

    private void HandleDeathVisuals()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (animationComp != null) animationComp.PlayDeath();
    }

    private void IgnoreAllPlayerColliders()
    {
        Collider2D[] myCols = GetComponentsInChildren<Collider2D>();
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            if (playerCol == null) continue;

            foreach (var myCol in myCols)
            {
                if (!myCol.isTrigger && !playerCol.isTrigger)
                {
                    Physics2D.IgnoreCollision(myCol, playerCol, true);
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Player mới join sau khi Summon đã spawn → ignore physics collision
        PlayerController player = collision.gameObject.GetComponentInParent<PlayerController>();
        if (player == null) player = collision.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            if (playerCol == null) return;

            Collider2D[] myCols = GetComponentsInChildren<Collider2D>();
            foreach (var myCol in myCols)
            {
                if (!myCol.isTrigger)
                {
                    Physics2D.IgnoreCollision(myCol, playerCol, true);
                }
            }
        }
    }
}
