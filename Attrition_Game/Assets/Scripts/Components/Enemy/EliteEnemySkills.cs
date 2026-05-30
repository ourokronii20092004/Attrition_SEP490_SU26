using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Kỹ năng đặc biệt dành cho quái tinh anh (Cultist, NightBorne, Gollux, Undead).
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
    // SKILL — Kỹ năng gây sát thương đặc biệt (Undead)
    // ═══════════════════════════════════════════════════════════════
    [System.Serializable]
    public class SkillConfig
    {
        [Tooltip("Tên skill (để debug)")] public string skillName = "Skill";
        [Tooltip("Sát thương gây ra")] public int damage = 2;
        [Tooltip("Phạm vi skill (bán kính)")] public float range = 2f;
        [Tooltip("Hình dạng hitbox")] public EnemyCombat.HitboxShape hitboxShape = EnemyCombat.HitboxShape.Circle;
        [Tooltip("Góc đánh (chỉ dùng cho Cone)")] [Range(0, 360)] public float angle = 180f;
        [Tooltip("Kích thước hình chữ nhật (chỉ dùng khi Rectangle)")] public Vector2 rectSize = new Vector2(2f, 1.5f);
        [Tooltip("Thời gian animation skill (giây)")] public float duration = 1.0f;
        [Tooltip("Cooldown riêng cho skill này (giây)")] public float cooldown = 5f;

        [Header("── Vị trí Hitbox ──")]
        [Tooltip("Bật để dùng offset tùy chỉnh thay vì attackPoint mặc định")]
        public bool useCustomOffset = false;
        [Tooltip("Offset vị trí hitbox so với quái (X tự lật theo hướng nhìn, Y giữ nguyên).\n" +
                 "Ví dụ: (0, 2) = phía trên, (2, 0) = phía trước, (-1, 1) = phía sau+trên")]
        public Vector2 hitboxOffset = Vector2.zero;
    }

    [Header("---- SKILL (Undead) ----")]
    [Tooltip("Bật nếu quái có khả năng dùng Skill gây damage")]
    public bool canUseSkills = false;
    [Tooltip("Danh sách skill (mỗi skill có damage, range, hitbox riêng)")] 
    public SkillConfig[] skills = new SkillConfig[0];
    [Tooltip("Tỉ lệ % dùng Skill thay vì Attack thường mỗi lần tấn công (0~1)")] 
    [Range(0f, 1f)] public float skillChance = 0.3f;

    // ═══════════════════════════════════════════════════════════════
    // SUMMON — Triệu hồi quái phụ (Undead)
    // ═══════════════════════════════════════════════════════════════
    [Header("---- SUMMON (Undead) ----")]
    [Tooltip("Bật nếu quái có khả năng triệu hồi quái phụ")]
    public bool canSummon = false;
    [Tooltip("Prefab quái được triệu hồi (Summon Of Undead)")]
    public NetworkPrefabRef summonPrefab;
    [Tooltip("Số lượng quái triệu hồi mỗi lần")] [Range(1, 10)]
    public int summonCount = 3;
    [Tooltip("Bán kính spawn xung quanh Undead (units)")]
    public float summonRadius = 2f;
    [Tooltip("Tỉ lệ % kích hoạt summon mỗi giây khi đủ điều kiện (0~1)")]
    [Range(0f, 1f)] public float summonChance = 0.15f;
    [Tooltip("Cooldown giữa 2 lần summon (giây)")]
    public float summonCooldown = 10f;
    [Tooltip("Thời gian animation summon (giây)")]
    public float summonDuration = 1.2f;
    [Tooltip("Số summon tối đa cùng tồn tại")] [Range(1, 20)]
    public int maxActiveSummons = 6;

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME (ẩn khỏi Inspector)
    // ═══════════════════════════════════════════════════════════════
    [HideInInspector][Networked] public NetworkBool IsTeleporting { get; set; }
    [HideInInspector][Networked] public NetworkBool IsHealing { get; set; }
    [HideInInspector][Networked] public NetworkBool IsUsingSkill { get; set; }
    [HideInInspector][Networked] public NetworkBool IsSummoning { get; set; }

    [Networked] private TickTimer teleportCooldownTimer { get; set; }
    [Networked] private TickTimer teleportActiveTimer { get; set; }
    [Networked] private Vector2 teleportTargetPos { get; set; }
    [Networked] private TickTimer healActiveTimer { get; set; }
    [Networked] private TickTimer skillActiveTimer { get; set; }
    [Networked] private TickTimer skillCooldownTimer { get; set; }
    [Networked] private int currentSkillIndex { get; set; }
    [Networked] private TickTimer summonActiveTimer { get; set; }
    [Networked] private TickTimer summonCooldownTimer { get; set; }

    // Cache reference tới EnemyController để cộng HP
    private System.Action<int> healCallback;
    private bool isFlying;

    // Skill damage references
    private Transform attackPoint;
    private LayerMask playerLayer;

    // Track active summons
    private List<NetworkObject> activeSummons = new List<NetworkObject>();

    // ═══════════════════════════════════════════════════════════════
    // INIT
    // ═══════════════════════════════════════════════════════════════
    public override void Spawned()
    {
        if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
    }

    /// <summary>
    /// Gọi bởi EnemyController.Spawned() để truyền callback hồi máu, flag bay,
    /// và references cho skill damage.
    /// </summary>
    public void Init(System.Action<int> onHeal, bool flying, Transform atkPoint = null, LayerMask playerMask = default)
    {
        healCallback = onHeal;
        isFlying = flying;
        attackPoint = atkPoint;
        playerLayer = playerMask;
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

    // ═══════════════════════════════════════════════════════════════
    // SKILL — Kỹ năng gây sát thương
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// AI gọi khi trong tầm đánh — roll xem có dùng Skill thay vì Attack thường không.
    /// Trả về true nếu bắt đầu dùng skill (AI sẽ chuyển sang UsingSkill state).
    /// </summary>
    public bool TryUseSkill()
    {
        if (!canUseSkills || IsUsingSkill || IsSummoning || IsHealing || IsTeleporting) return false;
        if (skills == null || skills.Length == 0) return false;
        if (!skillCooldownTimer.ExpiredOrNotRunning(Runner)) return false;

        // Roll tỉ lệ
        if (Random.value > skillChance) return false;

        // Random chọn 1 skill
        int idx = Random.Range(0, skills.Length);
        currentSkillIndex = idx;

        SkillConfig cfg = skills[idx];
        IsUsingSkill = true;
        skillActiveTimer = TickTimer.CreateFromSeconds(Runner, cfg.duration);

        RPC_PlaySkillAnim(idx);
        return true;
    }

    /// <summary>
    /// AI gọi mỗi tick khi đang dùng skill.
    /// </summary>
    public bool UpdateSkill()
    {
        if (!IsUsingSkill) return false;

        if (skillActiveTimer.ExpiredOrNotRunning(Runner))
        {
            IsUsingSkill = false;
            skillActiveTimer = TickTimer.None;

            // Set cooldown = max(skill's own cooldown, global)
            SkillConfig cfg = GetSkillConfig(currentSkillIndex);
            skillCooldownTimer = TickTimer.CreateFromSeconds(Runner, cfg.cooldown);
        }
        return IsUsingSkill;
    }

    /// <summary>
    /// [ANIMATION EVENT] Gây sát thương skill. Gọi từ animation clip ở frame gây damage.
    /// </summary>
    public void TriggerSkillDamage()
    {
        if (!HasStateAuthority) return;
        if (!IsUsingSkill) return;

        SkillConfig cfg = GetSkillConfig(currentSkillIndex);

        // Tính vị trí gốc của hitbox
        Vector2 skillOrigin = GetSkillOrigin(cfg);

        // Lấy hướng nhìn
        Vector2 facingDir = GetFacingDir();

        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = playerLayer;
        filter.useTriggers = false;
        int count = 0;

        switch (cfg.hitboxShape)
        {
            case EnemyCombat.HitboxShape.Circle:
                count = Runner.GetPhysicsScene2D().OverlapCircle(skillOrigin, cfg.range, filter, results);
                for (int i = 0; i < count; i++)
                    DealSkillDamage(results[i], cfg.damage);
                break;

            case EnemyCombat.HitboxShape.Rectangle:
                float facingAngleRad = Mathf.Atan2(facingDir.y, facingDir.x);
                Vector2 boxCenter = skillOrigin + facingDir * (cfg.rectSize.x / 2f);
                count = Runner.GetPhysicsScene2D().OverlapBox(boxCenter, cfg.rectSize, facingAngleRad * Mathf.Rad2Deg, filter, results);
                for (int i = 0; i < count; i++)
                    DealSkillDamage(results[i], cfg.damage);
                break;

            case EnemyCombat.HitboxShape.Cone:
            default:
                count = Runner.GetPhysicsScene2D().OverlapCircle(skillOrigin, cfg.range, filter, results);
                for (int i = 0; i < count; i++)
                {
                    Vector2 dirToPlayer = (results[i].transform.position - (Vector3)skillOrigin).normalized;
                    if (Vector2.Angle(facingDir, dirToPlayer) < cfg.angle / 2f)
                        DealSkillDamage(results[i], cfg.damage);
                }
                break;
        }
    }

    private void DealSkillDamage(Collider2D player, int damage)
    {
        Vector2 dirToPlayer = (player.transform.position - transform.position).normalized;
        IDamageable dmg = player.GetComponentInParent<IDamageable>();
        if (dmg != null && !dmg.IsDead)
        {
            Vector2 pushDir = new Vector2(dirToPlayer.x, 0.5f).normalized;
            dmg.TakeDamage(damage, pushDir, 0f);
        }
    }

    /// <summary>
    /// Bị đánh → ngắt skill.
    /// </summary>
    public void InterruptSkill()
    {
        if (!IsUsingSkill) return;
        IsUsingSkill = false;
        skillActiveTimer = TickTimer.None;
    }

    private SkillConfig GetSkillConfig(int index)
    {
        if (skills != null && index >= 0 && index < skills.Length && skills[index] != null)
            return skills[index];
        return new SkillConfig();
    }

    /// <summary>
    /// Tính vị trí gốc hitbox của skill, có tính offset tùy chỉnh và hướng nhìn.
    /// </summary>
    private Vector2 GetSkillOrigin(SkillConfig cfg)
    {
        if (cfg.useCustomOffset)
        {
            // Lật offset.x theo hướng nhìn
            Vector2 facingDir = GetFacingDir();
            float flipX = facingDir.x >= 0 ? 1f : -1f;
            Vector2 flippedOffset = new Vector2(cfg.hitboxOffset.x * flipX, cfg.hitboxOffset.y);
            return (Vector2)transform.position + flippedOffset;
        }

        // Mặc định: dùng attackPoint
        Transform dmgPoint = attackPoint != null ? attackPoint : transform;
        return dmgPoint.position;
    }

    /// <summary>
    /// Lấy hướng nhìn hiện tại dựa trên localScale và defaultFacingLeft.
    /// </summary>
    private Vector2 GetFacingDir()
    {
        bool defaultLeft = animationComp != null && animationComp.defaultFacingLeft;
        float scaleX = transform.localScale.x;
        if (defaultLeft)
            return scaleX > 0 ? Vector2.left : Vector2.right;
        else
            return scaleX > 0 ? Vector2.right : Vector2.left;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlaySkillAnim(int skillIndex)
    {
        if (animationComp != null) animationComp.PlaySkill(skillIndex);
    }

    // ═══════════════════════════════════════════════════════════════
    // SUMMON — Triệu hồi quái phụ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// AI gọi mỗi tick để roll xem có summon không. Trả về true nếu bắt đầu summon.
    /// </summary>
    public bool TryUseSummon()
    {
        if (!canSummon || IsSummoning || IsUsingSkill || IsHealing || IsTeleporting) return false;
        if (!summonPrefab.IsValid) return false;
        if (!summonCooldownTimer.ExpiredOrNotRunning(Runner)) return false;

        // Kiểm tra số summon đang hoạt động
        CleanupDeadSummons();
        if (activeSummons.Count >= maxActiveSummons) return false;

        // Roll tỉ lệ
        float chance = summonChance * Runner.DeltaTime;
        if (Random.value > chance) return false;

        IsSummoning = true;
        summonActiveTimer = TickTimer.CreateFromSeconds(Runner, summonDuration);
        RPC_PlaySummonAnim();
        return true;
    }

    /// <summary>
    /// AI gọi mỗi tick khi đang summon.
    /// </summary>
    public bool UpdateSummon()
    {
        if (!IsSummoning) return false;

        if (summonActiveTimer.ExpiredOrNotRunning(Runner))
        {
            // Animation xong → spawn quái
            SpawnSummons();
            IsSummoning = false;
            summonActiveTimer = TickTimer.None;
            summonCooldownTimer = TickTimer.CreateFromSeconds(Runner, summonCooldown);
        }
        return IsSummoning;
    }

    private void SpawnSummons()
    {
        if (!HasStateAuthority) return;
        CleanupDeadSummons();

        int spawnCount = Mathf.Min(summonCount, maxActiveSummons - activeSummons.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            // Tính vị trí spawn xung quanh Undead
            float angle = (360f / spawnCount) * i + Random.Range(-15f, 15f);
            float dist = Random.Range(summonRadius * 0.5f, summonRadius);
            Vector2 offset = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ) * dist;
            Vector3 spawnPos = transform.position + (Vector3)offset;

            NetworkObject summonObj = Runner.Spawn(summonPrefab, spawnPos, Quaternion.identity, null, (runner, obj) =>
            {
                SummonOfUndeadAI summonAI = obj.GetComponent<SummonOfUndeadAI>();
                if (summonAI != null)
                {
                    summonAI.InitOwner(Object);
                }
            });

            if (summonObj != null)
                activeSummons.Add(summonObj);
        }
    }

    /// <summary>
    /// Xoá summon đã chết/despawn khỏi danh sách theo dõi.
    /// </summary>
    private void CleanupDeadSummons()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            if (activeSummons[i] == null)
                activeSummons.RemoveAt(i);
        }
    }

    /// <summary>
    /// Despawn tất cả summon — gọi khi Undead chết.
    /// </summary>
    public void DespawnAllSummons()
    {
        if (!HasStateAuthority) return;
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            if (activeSummons[i] != null)
            {
                Runner.Despawn(activeSummons[i]);
            }
        }
        activeSummons.Clear();
    }

    /// <summary>
    /// Bị đánh → ngắt summon.
    /// </summary>
    public void InterruptSummon()
    {
        if (!IsSummoning) return;
        IsSummoning = false;
        summonActiveTimer = TickTimer.None;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlaySummonAnim()
    {
        if (animationComp != null) animationComp.PlaySummon();
    }

    // ═══════════════════════════════════════════════════════════════
    // GIZMOS — Hiển thị bán kính Skill, Summon, Teleport trong Scene
    // ═══════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Lấy attackPoint (nếu có) hoặc dùng transform
        Transform dmgPoint = attackPoint != null ? attackPoint : transform;

        // Lấy hướng nhìn
        EnemyAnimation anim = animationComp != null ? animationComp : GetComponent<EnemyAnimation>();
        bool defaultLeft = anim != null && anim.defaultFacingLeft;
        float scaleX = transform.localScale.x;
        Vector3 facingDirection;
        if (defaultLeft)
            facingDirection = scaleX > 0 ? Vector3.left : Vector3.right;
        else
            facingDirection = scaleX > 0 ? Vector3.right : Vector3.left;

        // ─── SKILL HITBOXES ───
        if (canUseSkills && skills != null)
        {
            Color[] skillColors = {
                new Color(1f, 0.2f, 0.8f),   // Hồng đậm — Skill 1
                new Color(0.6f, 0.2f, 1f),   // Tím — Skill 2
                new Color(1f, 0.4f, 0.1f),   // Cam đỏ
                new Color(0.1f, 1f, 0.6f),   // Xanh ngọc
                new Color(1f, 1f, 0.2f),     // Vàng
            };

            for (int i = 0; i < skills.Length && i < 5; i++)
            {
                SkillConfig cfg = skills[i];
                if (cfg == null) continue;

                Color col = skillColors[i % skillColors.Length];

                // Tính vị trí gốc hitbox (có offset nếu bật)
                Vector3 skillPos;
                if (cfg.useCustomOffset)
                {
                    float flipX = facingDirection.x >= 0 ? 1f : -1f;
                    Vector2 flippedOffset = new Vector2(cfg.hitboxOffset.x * flipX, cfg.hitboxOffset.y);
                    skillPos = transform.position + (Vector3)flippedOffset;

                    // Vẽ đường nối từ quái đến vị trí hitbox
                    Gizmos.color = new Color(col.r, col.g, col.b, 0.5f);
                    Gizmos.DrawLine(transform.position, skillPos);
                    Gizmos.DrawWireSphere(skillPos, 0.1f); // Chấm nhỏ đánh dấu gốc
                }
                else
                {
                    skillPos = dmgPoint.position;
                }

                switch (cfg.hitboxShape)
                {
                    case EnemyCombat.HitboxShape.Circle:
                        // Vòng tròn đầy + wireframe
                        Gizmos.color = new Color(col.r, col.g, col.b, 0.12f);
                        Gizmos.DrawSphere(skillPos, cfg.range);
                        Gizmos.color = new Color(col.r, col.g, col.b, 0.7f);
                        Gizmos.DrawWireSphere(skillPos, cfg.range);
                        break;

                    case EnemyCombat.HitboxShape.Cone:
                        // Wireframe + 2 tia biên
                        Gizmos.color = new Color(col.r, col.g, col.b, 0.4f);
                        Gizmos.DrawWireSphere(skillPos, cfg.range);
                        Gizmos.color = new Color(col.r, col.g, col.b, 0.8f);
                        Vector3 upper = Quaternion.Euler(0, 0, cfg.angle / 2f) * facingDirection;
                        Vector3 lower = Quaternion.Euler(0, 0, -cfg.angle / 2f) * facingDirection;
                        Gizmos.DrawRay(skillPos, upper * cfg.range);
                        Gizmos.DrawRay(skillPos, lower * cfg.range);
                        // Tia trung tâm
                        Gizmos.color = new Color(col.r, col.g, col.b, 0.4f);
                        Gizmos.DrawRay(skillPos, facingDirection * cfg.range);
                        break;

                    case EnemyCombat.HitboxShape.Rectangle:
                        float facingAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
                        Matrix4x4 oldMatrix = Gizmos.matrix;
                        Gizmos.matrix = Matrix4x4.TRS(skillPos, Quaternion.Euler(0, 0, facingAngle), Vector3.one);
                        Gizmos.color = new Color(col.r, col.g, col.b, 0.6f);
                        Gizmos.DrawWireCube(new Vector3(cfg.rectSize.x / 2f, 0, 0), new Vector3(cfg.rectSize.x, cfg.rectSize.y, 0));
                        Gizmos.color = new Color(col.r, col.g, col.b, 0.1f);
                        Gizmos.DrawCube(new Vector3(cfg.rectSize.x / 2f, 0, 0), new Vector3(cfg.rectSize.x, cfg.rectSize.y, 0));
                        Gizmos.matrix = oldMatrix;
                        break;
                }

                // Label
#if UNITY_EDITOR
                UnityEditor.Handles.color = col;
                string offsetInfo = cfg.useCustomOffset ? $" Pos=({cfg.hitboxOffset.x:F1},{cfg.hitboxOffset.y:F1})" : "";
                Vector3 labelPos = skillPos + Vector3.up * (cfg.range + 0.2f + i * 0.35f);
                UnityEditor.Handles.Label(labelPos, $"Skill {i}: {cfg.skillName} (R={cfg.range}, D={cfg.damage}{offsetInfo})");
#endif
            }
        }

        // ─── SUMMON RADIUS ───
        if (canSummon)
        {
            Color summonColor = new Color(0.3f, 1f, 0.3f); // Xanh lá sáng

            // Vòng tròn bán kính summon
            Gizmos.color = new Color(summonColor.r, summonColor.g, summonColor.b, 0.25f);
            Gizmos.DrawWireSphere(transform.position, summonRadius);
            Gizmos.color = new Color(summonColor.r, summonColor.g, summonColor.b, 0.06f);
            Gizmos.DrawSphere(transform.position, summonRadius);

            // Vẽ các điểm spawn dự kiến
            Gizmos.color = new Color(summonColor.r, summonColor.g, summonColor.b, 0.8f);
            for (int i = 0; i < summonCount; i++)
            {
                float angle = (360f / summonCount) * i;
                float dist = summonRadius * 0.75f;
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                ) * dist;
                Vector3 spawnPos = transform.position + (Vector3)offset;
                Gizmos.DrawWireSphere(spawnPos, 0.15f);
                Gizmos.DrawLine(transform.position, spawnPos);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.color = summonColor;
            Vector3 summonLabelPos = transform.position + Vector3.down * (summonRadius + 0.4f);
            UnityEditor.Handles.Label(summonLabelPos, $"Summon (x{summonCount}, R={summonRadius}, Max={maxActiveSummons})");
#endif
        }

        // ─── TELEPORT RANGE ───
        if (canTeleport && teleportTriggerRange > 0)
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, teleportTriggerRange);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.04f);
            Gizmos.DrawSphere(transform.position, teleportTriggerRange);

            // Min/Max distance
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, teleportMinDistance);
            Gizmos.DrawWireSphere(transform.position, teleportMaxDistance);
        }
    }
}
