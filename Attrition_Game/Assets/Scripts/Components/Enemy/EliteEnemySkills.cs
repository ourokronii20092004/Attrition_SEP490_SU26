using Fusion;
using UnityEngine;

/// <summary>
/// Kỹ năng đặc biệt dành cho quái tinh anh (Cultist, NightBorne, Gollux).
/// Gắn script này BÊN CẠNH EnemyAI + EnemyCombat trên những con quái tinh anh.
/// Quái thường KHÔNG cần script này.
/// </summary>
public class EliteEnemySkills : NetworkBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    // REFERENCES
    // ═══════════════════════════════════════════════════════════════
    [Header("---- REFS ----")]
    [SerializeField] private EnemyAnimation animationComp;

    // ═══════════════════════════════════════════════════════════════
    // TELEPORT — Dịch chuyển ngẫu nhiên quanh player
    // ═══════════════════════════════════════════════════════════════
    [Header("---- TELEPORT ----")]
    [Tooltip("Bật nếu quái có khả năng teleport")]
    public bool canTeleport = true;
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

    // ═══════════════════════════════════════════════════════════════
    // HEALING — Tự hồi máu ngẫu nhiên
    // ═══════════════════════════════════════════════════════════════
    [Header("---- HEALING ----")]
    [Tooltip("Bật nếu quái có khả năng tự hồi máu")]
    public bool canHeal = true;
    [Tooltip("Lượng máu hồi mỗi lần")]
    public int healAmount = 1;
    [Tooltip("Thời gian chơi animation heal (giây) — quái đứng yên trong khoảng này")]
    public float healDuration = 1.0f;
    [Tooltip("Ngưỡng % HP để bắt đầu roll heal (vd: 0.7 = dưới 70% HP mới có thể heal)")]
    [Range(0f, 1f)] public float healThreshold = 0.7f;
    [Tooltip("Xác suất heal mỗi giây khi đủ điều kiện (0~1). Cao = hay heal hơn")]
    [Range(0f, 1f)] public float healChancePerSecond = 0.15f;

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME (ẩn khỏi Inspector)
    // ═══════════════════════════════════════════════════════════════
    [HideInInspector][Networked] public NetworkBool IsTeleporting { get; set; }
    [HideInInspector][Networked] public NetworkBool IsHealing { get; set; }

    [Networked] private TickTimer teleportCooldownTimer { get; set; }
    [Networked] private TickTimer teleportActiveTimer { get; set; }
    [Networked] private Vector2 teleportTargetPos { get; set; }
    [Networked] private TickTimer healActiveTimer { get; set; }

    // Cache reference tới EnemyController để cộng HP
    private System.Action<int> healCallback;
    private bool isFlying;

    // ═══════════════════════════════════════════════════════════════
    // INIT
    // ═══════════════════════════════════════════════════════════════
    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
    }

    /// <summary>
    /// Gọi bởi EnemyController.Spawned() để truyền callback hồi máu và flag bay.
    /// </summary>
    public void Init(System.Action<int> onHeal, bool flying)
    {
        healCallback = onHeal;
        isFlying = flying;
    }

    // ═══════════════════════════════════════════════════════════════
    // TELEPORT
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// AI gọi mỗi tick khi muốn thử teleport. Trả về true nếu đã bắt đầu teleport.
    /// </summary>
    public bool TryTeleport(float distToPlayer, Transform playerTarget)
    {
        if (!canTeleport || IsTeleporting || IsHealing) return false;
        if (!teleportCooldownTimer.ExpiredOrNotRunning(Runner)) return false;
        if (teleportTriggerRange > 0 && distToPlayer > teleportTriggerRange) return false;
        if (playerTarget == null) return false;

        ExecuteTeleport(playerTarget);
        return true;
    }

    private void ExecuteTeleport(Transform playerTarget)
    {
        Vector2 playerPos = playerTarget.position;
        float randomAngle = Random.Range(0f, 360f);
        float randomDist = Random.Range(teleportMinDistance, teleportMaxDistance);
        Vector2 offset = new Vector2(
            Mathf.Cos(randomAngle * Mathf.Deg2Rad),
            Mathf.Sin(randomAngle * Mathf.Deg2Rad)
        ) * randomDist;
        Vector2 targetPos = playerPos + offset;

        // Nếu không bay, giữ nguyên Y
        if (!isFlying)
        {
            float randomSide = Random.value > 0.5f ? 1f : -1f;
            targetPos = new Vector2(playerPos.x + randomSide * randomDist, transform.position.y);
        }

        IsTeleporting = true;
        teleportTargetPos = targetPos;
        teleportActiveTimer = TickTimer.CreateFromSeconds(Runner, teleportDuration);

        // Quay mặt về hướng player
        float xDiff = playerTarget.position.x - transform.position.x;
        if (Mathf.Abs(xDiff) > 0.05f)
        {
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null) ai.NetFacingDir = xDiff > 0 ? 1f : -1f;
        }

        RPC_PlayTeleportAnim();
    }

    /// <summary>
    /// AI gọi mỗi tick khi đang teleport. Trả về true nếu vẫn đang teleport.
    /// </summary>
    public bool UpdateTeleport()
    {
        if (!IsTeleporting) return false;

        if (teleportActiveTimer.ExpiredOrNotRunning(Runner))
        {
            transform.position = new Vector3(teleportTargetPos.x, teleportTargetPos.y, transform.position.z);
            IsTeleporting = false;
            teleportActiveTimer = TickTimer.None;
            teleportCooldownTimer = TickTimer.CreateFromSeconds(Runner, teleportCooldown);
        }
        return IsTeleporting;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTeleportAnim()
    {
        if (animationComp != null) animationComp.PlayTeleport();
    }

    // ═══════════════════════════════════════════════════════════════
    // HEALING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// AI gọi mỗi tick để roll xem có heal không. Trả về true nếu bắt đầu heal.
    /// </summary>
    public bool TryRandomHeal(int currentHP, int maxHP)
    {
        if (!canHeal || IsHealing || IsTeleporting) return false;
        if (currentHP >= maxHP) return false;

        // Chỉ heal khi HP dưới ngưỡng
        float hpPercent = (float)currentHP / maxHP;
        if (hpPercent >= healThreshold) return false;

        // Roll xác suất
        float chance = healChancePerSecond * Runner.DeltaTime;
        if (Random.value > chance) return false;

        // Bắt đầu heal (animation sẽ gọi TriggerHeal khi tới frame hồi máu)
        IsHealing = true;
        healActiveTimer = TickTimer.CreateFromSeconds(Runner, healDuration);
        RPC_PlayHealingAnim();
        return true;
    }

    /// <summary>
    /// AI gọi mỗi tick khi đang heal. Chỉ quản lý STATE, KHÔNG tự hồi máu.
    /// Hồi máu thực sự xảy ra khi Animation Event gọi TriggerHeal().
    /// </summary>
    public bool UpdateHealing()
    {
        if (!IsHealing) return false;

        if (healActiveTimer.ExpiredOrNotRunning(Runner))
        {
            // Hết thời gian heal → kết thúc state (dù có heal được hay không)
            IsHealing = false;
            healActiveTimer = TickTimer.None;
            RPC_StopHealingAnim();
        }
        return IsHealing;
    }

    /// <summary>
    /// [ANIMATION EVENT] Hồi máu thực sự. Thêm event này vào animation heal
    /// ở frame mà quái thực hiện hành động hồi máu (vd: frame ánh sáng bao quanh).
    /// </summary>
    public void TriggerHeal()
    {
        // Chỉ host mới được thay đổi HP
        if (!HasStateAuthority) return;
        if (!IsHealing) return;
        healCallback?.Invoke(healAmount);
    }

    /// <summary>
    /// Bị đánh → ngắt heal ngay lập tức. Gọi bởi EnemyController.TakeDamage().
    /// </summary>
    public void InterruptHealing()
    {
        if (!IsHealing) return;
        IsHealing = false;
        healActiveTimer = TickTimer.None;
        RPC_StopHealingAnim();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHealingAnim()
    {
        if (animationComp != null) animationComp.PlayHealing();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StopHealingAnim()
    {
        if (animationComp != null) animationComp.StopHealing();
    }
}
