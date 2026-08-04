using Fusion;
using UnityEngine;
using System.Collections;
using System.Linq;
using Unity.Cinemachine;
using Attrition.Controllers;
using Attrition.Gameplay.Player;
using Attrition.Persistence;

/// <summary>
/// Đồng bộ vật lý giữa Host và Client:
/// - Input Authority (người chơi local): dùng physics prediction bình thường.
/// - Proxy (người chơi khác nhìn thấy trên màn hình bạn): nhận vị trí + velocity từ server.
/// </summary>
public class PlayerController : NetworkBehaviour, IDamageable, ITeleportable
{
    [Header("---- INJECT COMPONENTS ----")]
    [SerializeField] private PlayerCombat combatComp;
    [SerializeField] private PlayerAnimation animationComp;
    [SerializeField] private Attrition.Gameplay.Player.PlayerSkillCaster skillCaster;
    [Tooltip("Tùy chọn: nguồn chỉ số runtime. Bỏ trống = dùng maxHP serialized bên dưới.")]
    [SerializeField] private PlayerStats statsComp;
    [Tooltip("Tùy chọn: hệ thống bình HP/Mana. Bỏ trống = không có bình (prefab cũ).")]
    [SerializeField] private PotionSystem potionComp;
    [Tooltip("Tùy chọn: hub hiệu ứng accessory (lá chắn hấp thụ...). Bỏ trống = tự tìm.")]
    [SerializeField] private AccessoryEffects accessoryFx;
    [Tooltip("Tùy chọn: slow/root do skill boss gây ra. Bỏ trống = tự tìm; không có = miễn nhiễm (prefab cũ).")]
    [SerializeField] private PlayerStatusEffects statusFx;
    // Túi đồ — host quét để biết đã mở khoá double jump chưa (cache, không bắt buộc gán Inspector).
    private Attrition.Gameplay.Player.Inventory.PlayerInventory _inventory;

    // Checkpoint đang đứng trong vùng (local, không cần [Networked] — chỉ dùng để gate input R).
    private Attrition.Gameplay.World.Checkpoint _currentCheckpoint;
    private Attrition.Gameplay.NPC.NetworkNPC _currentNPC;

    [Header("---- MOVEMENT & PHYSICS ----")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask groundLayer;

    [Header("---- MOVEMENT CONFIG ----")]
    [Tooltip("SO chứa toàn bộ cấu hình vật lý và di chuyển")]
    [SerializeField] private Attrition.Data.MovementConfigSO moveConfig;

    [Header("---- COOP SMOOTHING ----")]
    [Tooltip("Child chứa Sprite + Animator (Interpolation Target). Gán để NetworkRigidbody2D nội suy " +
             "phần nhìn riêng, tránh giật do prediction trên client. Bỏ trống = dùng root (giật như cũ).")]
    [SerializeField] private Transform visualRoot;

    /// <summary>Transform chứa sprite (đã được NetworkRigidbody2D nội suy mượt). Nametag bám vào đây
    /// để trôi cùng nhịp với sprite, tránh giật tương đối (root snap 60Hz còn sprite nội suy).</summary>
    public Transform VisualRoot => visualRoot;

    // Xác đã tách Visual khỏi cây networked chưa (chống rung). Idempotent guard cho Detach/Reattach.
    private bool _corpseVisualDetached;

    // Shadow dash mở khoá bằng accessory (nhặt Shadow Cloak → HasShadowDash sync). OR config để
    // prefab/test cũ (hasShadowDash=true trong MovementConfigSO) vẫn dùng được như trước.
    private bool hasShadowDash => HasShadowDash || (moveConfig != null && moveConfig.hasShadowDash);
    private float dashDuration => moveConfig != null ? moveConfig.dashDuration : 0.2f;
    private float dashCooldownTime => moveConfig != null ? moveConfig.dashCooldownTime : 0.8f;
    private float crouchSpeedMultiplier => moveConfig != null ? moveConfig.crouchSpeedMultiplier : 0.4f;
    private float variableJumpCutMultiplier => moveConfig != null ? moveConfig.variableJumpCutMultiplier : 0.5f;
    private int maxJumps => moveConfig != null ? moveConfig.maxJumps : 2;

    private float slideDuration => moveConfig != null ? moveConfig.slideDuration : 0.5f;
    private float slideCooldownTime => moveConfig != null ? moveConfig.slideCooldownTime : 1f;

    [SerializeField] private Collider2D playerCollider;
    private Vector2 standSize => moveConfig != null ? moveConfig.standSize : new Vector2(1f, 2f);
    private Vector2 standOffset => moveConfig != null ? moveConfig.standOffset : new Vector2(0f, 0f);
    private Vector2 crouchSize => moveConfig != null ? moveConfig.crouchSize : new Vector2(1f, 1f);
    private Vector2 crouchOffset => moveConfig != null ? moveConfig.crouchOffset : new Vector2(0f, -0.5f);

    private float normalGravity => moveConfig != null ? moveConfig.normalGravity : 2f;
    private float fallGravity => moveConfig != null ? moveConfig.fallGravity : 4.5f;
    private float maxFallSpeed => moveConfig != null ? moveConfig.maxFallSpeed : -25f;

    private float knockbackForceOverride => moveConfig != null ? moveConfig.knockbackForceOverride : 6f;
    private float knockbackDuration => moveConfig != null ? moveConfig.knockbackDuration : 0.25f;
    private float invincibleDuration => moveConfig != null ? moveConfig.invincibleDuration : 0.8f;

    [Header("---- STATE ----")]
    [Networked] public int currentHP { get; set; }
    [Networked] public NetworkBool isDeadNetworked { get; set; }
    [Networked] public NetworkBool IsGrounded { get; set; }
    [Networked] public NetworkBool IsFacingRight { get; set; } = true;
    [Networked] public NetworkBool IsMoving { get; set; }
    [Networked] public float NetworkVelocityY { get; set; }

    [Networked] public NetworkBool IsCrouching { get; set; }
    [Networked] public NetworkBool IsDashing { get; set; }
    [Networked] public NetworkBool IsSliding { get; set; }
    [Networked] public int JumpCount { get; set; }

    /// <summary>Đã mở khoá double jump chưa (sở hữu accessory AbilityGrant=DoubleJump). Host set, sync xuống client.</summary>
    [Networked] public NetworkBool HasDoubleJump { get; set; }

    /// <summary>Đã mở khoá shadow dash chưa (sở hữu accessory AbilityGrant=ShadowDash). Host set, sync xuống client.</summary>
    [Networked] public NetworkBool HasShadowDash { get; set; }

    /// <summary>
    /// Chỉ player ĐANG nói chuyện nhận quest được miễn damage/knockback. Flag nằm trên từng NetworkObject
    /// player nên coop không khoá hay bảo vệ người còn lại; quest vẫn shared vì state nằm trên NetworkNPC.
    /// </summary>
    [Networked] public NetworkBool IsQuestDialogueProtected { get; set; }

    /// <summary>UI local bật/tắt bảo vệ khi mở/đóng hội thoại NPC.</summary>
    public void SetQuestDialogueProtection(bool active)
    {
        if (!HasInputAuthority) return;
        RpcSetQuestDialogueProtection(active);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSetQuestDialogueProtection(bool active)
    {
        IsQuestDialogueProtected = active;
        if (active)
        {
            _knockbackTimer = default;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>Teleport chờ áp dụng TRONG FixedUpdateNetwork (in-sim). TeleportTo có thể được gọi từ
    /// coroutine (spawn checkpoint) hoặc RPC — cả hai đều KHÔNG phải lúc an toàn để gọi
    /// NetworkRigidbody2D.Teleport() (no-op khi chưa in-sim / ném lỗi trong RPC delivery). Nên chỉ ghi
    /// ý định vào [Networked] này rồi FUN của host áp dụng đúng thời điểm → TeleportKey sync mọi peer,
    /// client SNAP đúng thay vì bị client-side prediction đè lại.</summary>
    [Networked] private Vector2 _pendingTeleportPos { get; set; }
    [Networked] private int _pendingTeleportSeq { get; set; }
    private int _appliedTeleportSeq;

    [Networked] private NetworkButtons _buttonsPrev { get; set; }
    [Networked] private TickTimer _dashTimer { get; set; }
    [Networked] private TickTimer _dashCooldown { get; set; }
    [Networked] private TickTimer _slideTimer { get; set; }
    [Networked] private TickTimer _slideCooldown { get; set; }
    [Networked] private float _slideDirection { get; set; }
    [Networked] private TickTimer _knockbackTimer { get; set; }
    [Networked] private Vector2 _lastStableGround { get; set; }

    /// <summary>
    /// Chống trúng hazard 2 lần cho CÙNG một lần rơi. `Hazard` là MonoBehaviour chạy trên MỌI peer nên
    /// host và client đều gọi RPC → trước đây 15% HP bị trừ 2 lần (30%) trong coop. Timer này [Networked]
    /// và chỉ host ghi, nên mỗi lần rơi chỉ ăn damage một lần bất kể có mấy peer báo về.
    /// </summary>
    [Networked] private TickTimer _hazardCooldown { get; set; }

    // NetworkPosition/NetworkVelocity/NetworkGravityScale ĐÃ BỎ: NetworkRigidbody2D (addon) đã sync
    // transform + velocity rồi, 3 field này host ghi mỗi tick nhưng KHÔNG ai đọc → chỉ tốn băng thông
    // và làm snapshot to hơn (client nhận/rollback nhiều dữ liệu vô ích mỗi tick).

    /// <summary>Tên hiển thị (sync mọi máy) — hiện trên đầu player + thanh máu đồng đội.</summary>
    [Networked] public NetworkString<_32> DisplayName { get; set; }

    public int maxHP = 100;
    private bool isInvincible = false;

    public bool IsDead => isDeadNetworked;

    /// <summary>True khi player đang đứng trong vùng 1 checkpoint (UI hiện gợi ý [F] OPTIONS).</summary>
    public bool IsAtCheckpoint => _currentCheckpoint != null;

    /// <summary>
    /// Đang đứng trong vùng checkpoint — bản [Networked] để HOST đọc được.
    ///
    /// VÌ SAO CẦN: `_currentCheckpoint` chỉ được set trong OnTriggerEnter2D khi `HasInputAuthority`, tức
    /// CHỈ tồn tại trên máy của chính người chơi đó. Trong coop, host là StateAuthority nhưng client giữ
    /// InputAuthority của player mình → host luôn thấy `IsAtCheckpoint == false` cho client. Gate đổi
    /// accessory chạy host-side nên phải có cờ sync này, không thì client không bao giờ đổi được.
    /// </summary>
    [Networked] public NetworkBool AtCheckpointNet { get; set; }

    /// <summary>Máy có InputAuthority báo host khi VÀO/RA vùng checkpoint (chỉ gửi lúc ĐỔI trạng thái).</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSetAtCheckpoint(NetworkBool inZone) => AtCheckpointNet = inZone;

    /// <summary>Tên checkpoint đang đứng (UI hiển thị). Rỗng nếu không ở checkpoint nào.</summary>
    public string CurrentCheckpointName => _currentCheckpoint != null ? _currentCheckpoint.DisplayName : "";

    /// <summary>True khi player đứng gần NPC (DialogueUI kiểm tra để mở hội thoại).</summary>
    public bool IsNearNPC => _currentNPC != null;

    /// <summary>Đã mở khoá shadow dash chưa (nhặt Shadow Cloak). ShadowDashEffect đọc để bật afterimage.</summary>
    public bool HasShadowDashAbility => hasShadowDash;

    /// <summary>Dash đã hồi xong chưa (cooldown hết). ShadowDashEffect đọc để báo hiệu "dash sẵn sàng".</summary>
    public bool IsDashReady => _dashCooldown.ExpiredOrNotRunning(Runner);

    /// <summary>NPC đang đứng gần (DialogueUI đọc).</summary>
    public Attrition.Gameplay.NPC.NetworkNPC CurrentNPC => _currentNPC;

    /// <summary>UI gọi khi bấm REST trong panel checkpoint: hồi đầy + hồi sinh quái (host xử lý qua RPC).</summary>
    public void RequestRestAtCheckpoint()
    {
        if (_currentCheckpoint != null) _currentCheckpoint.RequestRest();
    }

    // Nguồn HP DUY NHẤT: có statsComp → dùng PlayerStats.CurrentHP (chỗ PotionSystem hồi vào).
    // Không có → fallback currentHP riêng (tương thích prefab cũ).
    public int HP
    {
        get => statsComp != null ? statsComp.CurrentHP : currentHP;
        set { if (statsComp != null) statsComp.CurrentHP = value; else currentHP = value; }
    }

    public override void Spawned()
    {
        if (statsComp == null) statsComp = GetComponent<PlayerStats>();
        if (statsComp != null) maxHP = statsComp.MaxHP;
        if (potionComp == null) potionComp = GetComponent<PotionSystem>();
        if (accessoryFx == null) accessoryFx = GetComponent<AccessoryEffects>();
        if (statusFx == null) statusFx = GetComponent<PlayerStatusEffects>();
        if (_inventory == null) _inventory = GetComponent<Attrition.Gameplay.Player.Inventory.PlayerInventory>();
        // statsComp tự init CurrentHP=MaxHP trong Spawned của nó. Chỉ tự init khi KHÔNG có statsComp.
        if (HasStateAuthority && statsComp == null) currentHP = maxHP;

        if (combatComp == null) combatComp = GetComponent<PlayerCombat>();
        if (skillCaster == null) skillCaster = GetComponent<Attrition.Gameplay.Player.PlayerSkillCaster>();
        if (animationComp == null) animationComp = GetComponent<PlayerAnimation>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();

        // Client physics = ForwardOnly. Player của CHÍNH client (InputAuthority) cần được simulate để
        // di chuyển/nhảy PREDICT mượt — nếu không, vị trí chỉ nội suy từ snapshot host (~30Hz), camera
        // follow sẽ giật khi nhảy (vận tốc Y đổi nhanh). Đánh dấu simulate cho mọi peer điều khiển nó.
        if (Object != null && (HasInputAuthority || HasStateAuthority))
            Runner.SetIsSimulated(Object, true);

        // INTERPOLATION TARGET cho MỌI player (kể cả local). Đây là setup CHUẨN của Fusion để mượt:
        // physics chạy 60Hz trong FixedUpdateNetwork, addon nội suy Visual giữa các tick để render mọi
        // FPS đều mượt. NẾU KHÔNG gán target + object đang simulate (local player) → addon BỎ QUA nội
        // suy (xem NetworkRigidbodyBase.Render: FixedUpdate + no target → return) → root nhảy từng bước
        // 60Hz → GIẬT trên màn FPS cao (đúng lỗi "sống cũng giật nhẹ"). Local player nội suy trong
        // predicted timeframe nên vẫn bám input, không lag cảm nhận được.
        if (visualRoot != null)
        {
            var nrb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody2D>();
            if (nrb != null) nrb.SetInterpolationTarget(visualRoot);
        }

        // Tắt va chạm vật lý giữa Player và Enemy để Player đi xuyên qua được
        // CHỈ dùng Collider-based (không dùng IgnoreLayerCollision vì nó chặn cả trigger → ContactDamage không hoạt động)
        IgnoreAllEnemyColliders();

        // Camera follow VISUAL ROOT (= interpolation target), KHÔNG follow root.
        // Root chỉ nhảy theo tick 60Hz; visualRoot được addon nội suy mỗi frame. Nếu camera bám root mà
        // sprite bám visualRoot thì hai cái LỆCH NHỊP → ở 144fps sprite rung tại chỗ so với camera:
        // đúng hiện tượng "chỉ nhân vật của mình rung, quái vẫn mượt" (quái là proxy, camera không bám).
        // Xác chết detach visualRoot → lúc đó chuyển Follow về root (xem DetachCorpseVisual).
        if (HasInputAuthority)
        {
            SetCameraFollow(visualRoot != null ? visualRoot : transform);
            RpcRequestWorldMapDiscovery();

            // Đặt tên hiển thị. Solo/host (có StateAuthority) set THẲNG; coop CLIENT mới gửi RPC.
            // KHÔNG gọi RPC khi đang có StateAuthority trong Spawned() → tránh exception làm hỏng init (player rơi).
            string myName = Attrition.Persistence.GameLaunch.CharacterName;
            if (string.IsNullOrEmpty(myName)) myName = "Player";
            if (HasStateAuthority) DisplayName = myName;
            else RpcSetDisplayName(myName);
        }

        // Nhãn TÊN + thanh máu trên đầu mỗi player. Bọc try/catch để KHÔNG làm hỏng Spawned nếu lỗi.
        try { PlayerNameTag.Attach(this, HasInputAuthority); }
        catch (System.Exception e) { Debug.LogWarning($"[PlayerController] NameTag lỗi: {e.Message}"); }

        GameSettings.OnChanged += ApplyLocalVisibility;
        ApplyLocalVisibility();

        // Player sống sót qua LoadScene (Fusion không huỷ NetworkObject khi đổi scene) → phải bám lại
        // camera của scene mới, vì Spawned() không chạy lần hai.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedRebindCamera;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        GameSettings.OnChanged -= ApplyLocalVisibility;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedRebindCamera;
    }

    /// <summary>
    /// Sang scene mới, NetworkObject của player SỐNG SÓT nên Spawned() KHÔNG chạy lại → camera của
    /// scene mới không có Follow target ("no display camera"). Bám lại camera mỗi lần scene load.
    /// Chỉ máy sở hữu nhân vật này (InputAuthority) mới đặt camera.
    /// </summary>
    private void OnSceneLoadedRebindCamera(UnityEngine.SceneManagement.Scene scene,
                                           UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!HasInputAuthority) return;
        StartCoroutine(RebindCameraNextFrame());
    }

    private IEnumerator RebindCameraNextFrame()
    {
        // Chờ 1 frame cho camera của scene mới kịp Awake.
        yield return null;
        var cam = FindAnyObjectByType<CinemachineCamera>();
        if (cam == null) yield break;

        cam.Follow = transform;

        // Xoá confiner của map CŨ (bounding shape thuộc scene cũ) — nếu không camera bị kẹt trong
        // vùng giới hạn không còn tồn tại.
        var confiner = cam.GetComponent<CinemachineConfiner2D>();
        if (confiner != null)
        {
            confiner.BoundingShape2D = null;
            confiner.InvalidateBoundingShapeCache();
        }

        cam.ForceCameraPosition(
            new Vector3(transform.position.x, transform.position.y, cam.transform.position.z),
            cam.transform.rotation);
    }

    private void ApplyLocalVisibility()
    {
        if (HasInputAuthority || visualRoot == null) return;
        visualRoot.gameObject.SetActive(GameSettings.ShowOtherPlayers);
    }

    private CinemachineCamera _cam;

    /// <summary>Gán Follow cho camera local (cache camera để không FindAnyObjectByType lại mỗi lần).</summary>
    private void SetCameraFollow(Transform target)
    {
        if (!HasInputAuthority || target == null) return;
        if (_cam == null) _cam = FindAnyObjectByType<CinemachineCamera>();
        if (_cam != null) _cam.Follow = target;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSetDisplayName(string n) => DisplayName = n;

    public override void FixedUpdateNetwork()
    {
        // Áp dụng teleport đang chờ TRƯỚC mọi thứ (trên MỌI peer kể cả proxy return sớm bên dưới) để
        // TeleportKey của NetworkRigidbody2D được set trong tick → client SNAP đúng, không bị prediction đè.
        ApplyPendingTeleport();

        // SOLO pause: đóng băng player (Fusion bỏ qua Time.timeScale).
        // Phải zero CẢ gravityScale — chỉ zero velocity thì trọng lực vẫn áp giữa các physics step
        // khiến nhân vật rơi từ từ khi mở Inventory/Menu lúc đang trên không. Unpause: logic thường
        // tự set lại gravityScale mỗi tick (fast-fall/normal) nên không cần lưu giá trị cũ.
        if (Attrition.Persistence.GamePause.IsPaused)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }
            return;
        }

        CheckGround();
        NetworkVelocityY = rb.linearVelocity.y;

        if (HasStateAuthority)
        {
            // Hồi stamina theo thời gian (concept: 10/s). Null-safe nếu chưa gán statsComp.
            if (statsComp != null) statsComp.RegenStamina(Runner.DeltaTime);

            // Cập nhật cờ mở khoá double jump theo túi đồ (sở hữu Feather Charm). Host tính, sync proxy.
            if (_inventory != null)
            {
                HasDoubleJump = _inventory.HasAbility(Attrition.Data.GrantedAbility.DoubleJump);
                HasShadowDash = _inventory.HasAbility(Attrition.Data.GrantedAbility.ShadowDash);
            }
        }
        else if (!HasInputAuthority)
        {
            // Proxy (player của peer khác): addon NetworkRigidbody2D tự nội suy chuyển động.
            // Xác chết: chỉ TÁCH Visual khi đã CHẠM ĐẤT (IsGrounded, networked). Trong lúc xác còn RƠI,
            // giữ Visual attached để addon nội suy theo body rơi (mượt như player sống). Chạm đất =
            // hết di chuyển → detach để đứng im tuyệt đối, không rung. Sống lại → reattach.
            if (isDeadNetworked && IsGrounded) DetachCorpseVisual();
            else ReattachCorpseVisual();
            return;
        }

        if (IsGrounded)
        {
            JumpCount = 0;

            // SỬA LỖI GÓC ĐẤT: Nếu đang đứng trên mặt đất mà velocity Y > 0 (bị đẩy lên bởi góc cạnh)
            // → ép velocity Y = 0 để không bị bật nhảy bất ngờ
            if (rb.linearVelocity.y > 0.1f && !IsDashing)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
        }

        if (isDeadNetworked)
        {
            // XÁC CHẾT (host/local — authoritative). Cho xác RƠI xuống đất rồi mới nằm im:
            if (IsGrounded)
            {
                // Chạm đất → đóng băng Kinematic + tách Visual (đứng im tuyệt đối, không rung). 1 lần.
                if (rb.bodyType != RigidbodyType2D.Kinematic)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.gravityScale = 0f;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    if (playerCollider != null) playerCollider.enabled = false;
                }
                DetachCorpseVisual();
            }
            else
            {
                // Còn trên không → RƠI tự nhiên: dừng ngang, giữ trọng lực rơi, kẹp tốc độ rơi tối đa.
                // Visual vẫn attached (chưa detach) để addon nội suy theo body rơi → mượt trên client.
                rb.gravityScale = fallGravity;
                rb.linearVelocity = new Vector2(0f, Mathf.Max(rb.linearVelocity.y, maxFallSpeed));
                NetworkVelocityY = rb.linearVelocity.y;
            }
            return;
        }

        // --- KNOCKBACK: Khóa toàn bộ input khi đang bị đẩy lùi ---
        if (!_knockbackTimer.ExpiredOrNotRunning(Runner))
        {
            NetworkVelocityY = rb.linearVelocity.y;
            return;
        }

        // --- DIALOGUE & TRANSITION LOCK: khóa di chuyển ---
        if (HasInputAuthority && (Attrition.Persistence.DialogueState.IsActive
                                  || Attrition.Gameplay.Environment.SceneFader.IsTransitioning
                                  || Attrition.Gameplay.Environment.WorldMapController.IsOpen))
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            IsMoving = false;
            return;
        }

        // --- HOLLOW KNIGHT FAST FALL ---
        // Khi đang rơi xuống (velocity Y < 0) và không đang dash -> tăng trọng lực
        if (!IsDashing)
        {
            if (rb.linearVelocity.y < 0)
            {
                rb.gravityScale = fallGravity;
                // Giới hạn tốc độ rơi tối đa
                if (rb.linearVelocity.y < maxFallSpeed)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);
                }
            }
            else
            {
                rb.gravityScale = normalGravity;
            }
        }

        // --- DASH LOGIC ---
        if (IsDashing)
        {
            if (_dashTimer.Expired(Runner))
            {
                IsDashing = false;
                rb.gravityScale = normalGravity;
            }
            else
            {
                float dashDir = IsFacingRight ? 1f : -1f;
                float dSpeed = statsComp != null ? statsComp.DashSpeed : 25f;
                rb.linearVelocity = new Vector2(dashDir * dSpeed, 0);
                return;
            }
        }

        // --- SLIDE EXECUTION LOGIC ---
        if (IsSliding)
        {
            if (_slideTimer.Expired(Runner))
            {
                IsSliding = false;
            }
            else
            {
                // Ép hướng mặt theo hướng slide
                IsFacingRight = _slideDirection > 0;
                float sSpeed = statsComp != null ? statsComp.SlideSpeed : 20f;
                rb.linearVelocity = new Vector2(_slideDirection * sSpeed, rb.linearVelocity.y);
                
                // Khóa input di chuyển bình thường, nhảy vào đoạn cuối của hàm để lấy update
                // Nhưng cần phải check hitbox size liên tục!
            }
        }

        if (GetInput(out NetworkInputData data))
        {
            bool inputCrouch = data.buttons.IsSet(MyButtons.Crouch) && IsGrounded;
            bool wantToCrouch = inputCrouch;

            // --- CEILING CHECK ---
            // Nếu nhả phím ngồi nhưng đang vướng trần nhà thì ÉP phải ngồi tiếp
            if (!wantToCrouch && (IsCrouching || IsSliding))
            {
                if (CheckCeiling())
                {
                    wantToCrouch = true;
                }
            }

            IsCrouching = wantToCrouch;

            // --- HITBOX RESIZING ---
            if (IsCrouching || IsSliding)
            {
                SetColliderSize(crouchSize, crouchOffset);
            }
            else
            {
                SetColliderSize(standSize, standOffset);
            }

            var pressed = data.buttons.GetPressed(_buttonsPrev);
            var released = _buttonsPrev.GetPressed(data.buttons);

            // Bỏ qua MOVEMENT bình thường nếu đang Slide
            if (!IsSliding)
            {
                // --- MOVEMENT ---
                if (skillCaster != null && skillCaster.IsCasting && IsGrounded)
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    IsMoving = false;
                }
                else if ((combatComp.IsHoldingAttack || combatComp.IsAttacking) && IsGrounded)
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    IsMoving = false;
                }
                else if (statusFx != null && statusFx.Rooted)
                {
                    // ROOT (đất bọc của DemonKin): giam tại chỗ. Vẫn để trọng lực kéo xuống (không đổi
                    // velocity.y) để player không treo lơ lửng nếu bị dính giữa không trung.
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    IsMoving = false;
                }
                else
                {
                    float mSpeed = statsComp != null ? statsComp.MoveSpeed : 10f;
                    // SLOW (lốc nước của ArchDemon): nhân hệ số còn lại; = 1 khi không bị gì.
                    if (statusFx != null) mSpeed *= statusFx.MoveSpeedMultiplier;
                    float speed = IsCrouching ? mSpeed * crouchSpeedMultiplier : mSpeed;
                    rb.linearVelocity = new Vector2(data.horizontalInput * speed, rb.linearVelocity.y);
                    IsMoving = Mathf.Abs(data.horizontalInput) > 0.1f;
                }

                // --- JUMP LOGIC ---
                // ROOT chặn nhảy: bị đất bọc thì không thoát được bằng cách nhảy (đúng ý nghĩa khống chế).
                bool rooted = statusFx != null && statusFx.Rooted;
                if (pressed.IsSet(MyButtons.Jump) && !IsCrouching && !combatComp.IsHoldingAttack && !rooted)
                {
                    // Số lần nhảy tối đa: luôn 1 (nhảy đất); +1 nếu đã mở khoá double jump (nhặt Feather Charm).
                    // Vẫn kẹp theo maxJumps cấu hình trên prefab (mặc định 2).
                    int effectiveMaxJumps = Mathf.Min(maxJumps, HasDoubleJump ? 2 : 1);
                    if (IsGrounded || JumpCount < effectiveMaxJumps)
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                        rb.position = new Vector2(rb.position.x, rb.position.y + 0.05f);
                        float jForce = statsComp != null ? statsComp.JumpForce : 15f;
                        float djForce = statsComp != null ? statsComp.DoubleJumpForce : 12f;
                        float currentJumpForce = (JumpCount > 0) ? djForce : jForce;
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentJumpForce);
                        JumpCount++;
                    }
                }

                bool wasHoldingJump = _buttonsPrev.IsSet(MyButtons.JumpHeld);
                bool isHoldingJump = data.buttons.IsSet(MyButtons.JumpHeld);
                if (wasHoldingJump && !isHoldingJump && rb.linearVelocity.y > 0 && !IsGrounded && JumpCount <= 1)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * variableJumpCutMultiplier);
                }

                // --- FACING ---
                if (!combatComp.IsAttacking && !combatComp.IsHoldingAttack)
                {
                    if (data.horizontalInput > 0) IsFacingRight = true;
                    else if (data.horizontalInput < 0) IsFacingRight = false;
                }
            }

            // --- DASH / SLIDE LOGIC ---
            // ROOT chặn cả dash và slide — nếu không, dash là đường thoát khống chế miễn phí (dash còn
            // bất tử khi có shadow dash), làm skill "đất bọc" của DemonKin vô nghĩa.
            if (pressed.IsSet(MyButtons.Dash) && !combatComp.IsHoldingAttack
                && (statusFx == null || !statusFx.Rooted))
            {
                if (wantToCrouch)
                {
                    // Đang ngồi + phím Dash → SLIDE
                    if (_slideCooldown.ExpiredOrNotRunning(Runner))
                    {
                        // Gate stamina giống dash. Không có statsComp → slide free (tương thích prefab cũ).
                        bool canAfford = statsComp == null || statsComp.TryConsumeStamina(statsComp.DashStaminaCost);
                        if (canAfford)
                        {
                            IsSliding = true;
                            // Xác định hướng Slide: Ưu tiên A/D đang bấm, nếu không thì dùng hướng nhìn
                            _slideDirection = data.horizontalInput != 0 ? Mathf.Sign(data.horizontalInput) : (IsFacingRight ? 1f : -1f);
                            _slideTimer = TickTimer.CreateFromSeconds(Runner, slideDuration);
                            _slideCooldown = TickTimer.CreateFromSeconds(Runner, slideCooldownTime);

                            // Nếu đang bấm Slide thì hủy Crouching animation để chạy Slide animation
                            IsCrouching = false;
                        }
                    }
                }
                else
                {
                    // Đang đứng + phím Dash → DASH
                    if (!IsCrouching && _dashCooldown.ExpiredOrNotRunning(Runner))
                    {
                        // Gate stamina: chỉ dash khi đủ. Không có statsComp → dash free (tương thích prefab cũ).
                        bool canAfford = statsComp == null || statsComp.TryConsumeStamina(statsComp.DashStaminaCost);
                        if (canAfford)
                        {
                            IsDashing = true;
                            _dashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
                            _dashCooldown = TickTimer.CreateFromSeconds(Runner, dashCooldownTime);
                            rb.gravityScale = 0;
                        }
                    }
                }
            }

            // --- BÌNH HP / MANA (Q / E) ---
            if (potionComp != null)
            {
                if (pressed.IsSet(MyButtons.HealthPotion)) potionComp.TryUseHealthPotion();
                if (pressed.IsSet(MyButtons.ManaPotion)) potionComp.TryUseManaPotion();
            }

            // --- REST/CHECKPOINT UI (F): mở UI lựa chọn (rest/teleport) được xử lý ở GameUIController (local).
            // Nút REST trong UI sẽ gọi RequestRestAtCheckpoint() → checkpoint.RequestRest().

            _buttonsPrev = data.buttons;

            // --- COMBAT ---
            combatComp.HandleCombat(data, IsFacingRight, IsCrouching);

            // --- SKILL (K) ---
            if (skillCaster != null) skillCaster.HandleSkill(data, IsFacingRight);
        }
    }

    public override void Render()
    {
        // Vị trí proxy do NetworkRigidbody2D (addon) tự nội suy — KHÔNG tự set rb.position ở đây nữa
        // (từng gây giật vì đánh nhau với addon). Render chỉ còn lo animation.
        animationComp.UpdateAnimations(
            IsMoving, IsGrounded, isDeadNetworked, NetworkVelocityY, IsFacingRight,
            IsCrouching, IsDashing, combatComp.IsChargingAttack,
            combatComp.IsAttacking, IsSliding
        );

        UpdateMovementSfx();
    }

    private bool _sfxWasGrounded = true;
    private int _sfxPrevJumpCount;
    private float _sfxNextStep;
    private bool _sfxWasDashing;
    private int _sfxPrevHealthCharges;
    private int _sfxPrevManaCharges;
    private float _sfxDashLock, _sfxJumpLock, _sfxLandLock;

    // CLIENT: các cờ dưới đây là state PREDICT. Khi server snapshot về lệch dự đoán, Fusion rollback +
    // resimulate → cờ có thể nhấp nháy false→true LẦN NỮA trong cùng một hành động, khiến Render bắt 2
    // sườn lên và phát SFX 2 lần (đúng lỗi "tiếng dash tách thành 2"). Lockout ngắn cho mỗi loại âm
    // chặn lần lặp đó nhưng vẫn giữ phản hồi tức thì của state predict.
    // ponytail: lockout theo thời gian (đủ vì dash/nhảy đều > 0.15s); nếu sau này cần chính xác tuyệt
    // đối thì đổi sang GetChangeDetector(ChangeDetector.Source.SnapshotFrom) cho proxy và giữ predict
    // cho local player.
    private const float SfxRetriggerLockout = 0.15f;

    private void UpdateMovementSfx()
    {
        if (isDeadNetworked) return;
        var sfx = Attrition.Systems.GameSfx.Instance;

        // JUMP: JumpCount tăng lên = vừa bật nhảy (gồm cả double jump).
        if (JumpCount > _sfxPrevJumpCount && Time.time >= _sfxJumpLock)
        {
            sfx.PlayJump();
            _sfxJumpLock = Time.time + SfxRetriggerLockout;
        }
        _sfxPrevJumpCount = JumpCount;

        // LAND: chuyển từ trên-không sang chạm-đất.
        if (IsGrounded && !_sfxWasGrounded && Time.time >= _sfxLandLock)
        {
            sfx.PlayLand();
            _sfxLandLock = Time.time + SfxRetriggerLockout;
        }
        _sfxWasGrounded = IsGrounded;

        // DASH: cờ IsDashing bật lên = vừa bắt đầu lướt.
        if (IsDashing && !_sfxWasDashing && Time.time >= _sfxDashLock)
        {
            sfx.PlayDash();
            _sfxDashLock = Time.time + SfxRetriggerLockout;
        }
        _sfxWasDashing = IsDashing;

        // POTION: số bình giảm = vừa uống (HP hoặc Mana). Networked nên nghe được trên mọi máy.
        if (potionComp != null)
        {
            if (potionComp.HealthCharges < _sfxPrevHealthCharges || potionComp.ManaCharges < _sfxPrevManaCharges)
                sfx.PlayPotion();
            _sfxPrevHealthCharges = potionComp.HealthCharges;
            _sfxPrevManaCharges = potionComp.ManaCharges;
        }

        // FOOTSTEP: đang đi trên đất → phát nhịp đều (không áp cho dash/slide/crouch).
        if (IsGrounded && IsMoving && !IsDashing && !IsSliding && !IsCrouching)
        {
            if (Time.time >= _sfxNextStep)
            {
                sfx.PlayStep();
                _sfxNextStep = Time.time + 0.32f; // ~nhịp chạy
            }
        }
        else
        {
            _sfxNextStep = 0f; // dừng đi → bước kế phát ngay khi đi lại
        }
    }

    // Buffer + filter dùng lại cho ground cast. Trước đây cấp phát mới MỖI tick; client còn resimulate
    // nhiều tick/frame nên rác GC dồn lên gấp nhiều lần host → GC spike = giật dù FPS cao.
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[1];
    private ContactFilter2D _groundFilter;
    private bool _groundFilterReady;

    private void CheckGround()
    {
        // Dùng rb.Cast vì nó tự động dùng đúng physics scene của Fusion
        // Fix góc đất được xử lý bằng cách clamp velocity Y ở trên
        if (!_groundFilterReady)
        {
            _groundFilter = new ContactFilter2D { layerMask = groundLayer, useLayerMask = true };
            _groundFilterReady = true;
        }
        IsGrounded = rb.Cast(Vector2.down, _groundFilter, _groundHits, 0.05f) > 0;

        // BR-39: ghi nhớ điểm đất an toàn cuối (đứng yên trên đất) để hồi sinh khi rơi bẫy.
        if (HasStateAuthority && IsGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.1f)
            _lastStableGround = rb.position;
    }

    private bool CheckCeiling()
    {
        // Tính toán vùng không gian mà đầu nhân vật sẽ chiếm chỗ khi Đứng Lên
        float crouchTop = rb.position.y + crouchOffset.y + (crouchSize.y / 2f);
        float standTop = rb.position.y + standOffset.y + (standSize.y / 2f);
        
        float diff = standTop - crouchTop;
        if (diff <= 0) return false; // Không có sự thay đổi chiều cao hoặc ngồi cao hơn đứng

        // Tạo một cái hộp kiểm tra đúng bằng phần bù đắp giữa Ngồi và Đứng
        Vector2 checkSize = new Vector2(standSize.x * 0.9f, diff); // bóp chiều ngang lại 10% để không bị vướng tường 2 bên
        Vector2 checkCenter = new Vector2(rb.position.x + standOffset.x, crouchTop + (diff / 2f));

        // BẮT BUỘC dùng Runner.GetPhysicsScene2D() thay vì Physics2D tĩnh để tương thích với mạng của Photon Fusion
        Collider2D hit = Runner.GetPhysicsScene2D().OverlapBox(checkCenter, checkSize, 0f, groundLayer);
        return hit != null;
    }

    private void SetColliderSize(Vector2 size, Vector2 offset)
    {
        if (playerCollider == null) return;
        
        if (playerCollider is BoxCollider2D box)
        {
            box.size = size;
            box.offset = offset;
        }
        else if (playerCollider is CapsuleCollider2D cap)
        {
            cap.size = size;
            cap.offset = offset;
        }
    }

    public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, Attrition.Core.DamageType type = Attrition.Core.DamageType.Physical)
    {
        if (IsQuestDialogueProtected || isInvincible || isDeadNetworked || (hasShadowDash && IsDashing)) return;
        RPC_TakeDamage(damage, knockbackDir, knockbackForce, (int)type);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, int type)
    {
        // Kiểm tra LẠI ở authority: request damage có thể đã nằm trên wire trước khi player mở dialogue.
        if (IsQuestDialogueProtected || isDeadNetworked || (hasShadowDash && IsDashing)) return;

        // damage = chỉ số tấn công GỐC; defender tự áp DEF (Physical) hoặc RES (Magic).
        int def = statsComp != null ? statsComp.DEF : 0;
        int res = statsComp != null ? statsComp.RES : 0;
        int taken = Attrition.Core.DamageCalculator.Compute((Attrition.Core.DamageType)type, damage, def, res);

        // Accessory DamageShield: khiên hấp thụ trước, chỉ phần dư mới trừ HP.
        if (accessoryFx != null) taken = accessoryFx.AbsorbWithShield(taken);

        HP -= taken;

        // Luôn dùng knockbackForceOverride từ Inspector để điều chỉnh lực đẩy lùi
        RPC_ApplyKnockback(knockbackDir, knockbackForceOverride);
        if (HP <= 0) Die();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyKnockback(Vector2 dir, float force)
    {
        if (IsQuestDialogueProtected) return;
        if (HasInputAuthority) Attrition.Systems.GameSfx.Instance.PlayHurt();
        if (force <= 0) 
        {
            // Không knockback, chỉ chớp sáng
            animationComp.PlayHit();
            StartCoroutine(InvincibleCoroutine());
            return;
        }
        
        rb.linearVelocity = dir * force;

        // Khóa input trong thời gian knockbackDuration để velocity không bị ghi đè
        if (HasStateAuthority)
        {
            _knockbackTimer = TickTimer.CreateFromSeconds(Runner, knockbackDuration);
        }

        animationComp.PlayHit();
        StartCoroutine(InvincibleCoroutine());
    }

    /// <summary>
    /// Hồi sinh đầy đủ player tại 1 vị trí: clear cờ chết → bật lại physics/collider → teleport →
    /// hồi đầy HP/Mana/Stamina + refill bình → reset thanh EXP → bất tử tạm. Chỉ host.
    /// Gom mọi bước theo đúng thứ tự để tránh bug respawn (xuyên đất/bay, âm HP, không uống bình).
    /// </summary>
    public void ReviveAndRestore(Vector3 spawn)
    {
        if (!HasStateAuthority) return;

        isDeadNetworked = false;
        // Xoá slow/root: chết trong lúc bị đất bọc mà không clear thì sống lại vẫn đứng cứng tại chỗ.
        if (statusFx != null) statusFx.ClearAll();
        RPC_RestorePhysicsAfterRevive(spawn, doWarp: true);
        TeleportTo(spawn);

        if (statsComp != null) statsComp.ReviveFull();
        if (potionComp != null) potionComp.RefillAll();

        // Chết mất thanh EXP đang tích (progress tới cấp kế), KHÔNG mất level đã lên.
        var prog = GetComponent<PlayerProgression>();
        if (prog != null) prog.ResetExpProgressOnDeath();

        GrantReviveInvincibility(3.0f); // BR-18
    }

    /// <summary>
    /// Hồi sinh tại chỗ (đồng đội cứu bằng bình): clear cờ chết + bật lại physics/collider + set HP.
    /// Không teleport, không refill bình. Chỉ host. Khôi phục physics để tránh xác Kinematic/collider-off
    /// gây rơi xuyên đất hoặc đứng cứng giữa trời sau khi sống lại.
    /// </summary>
    public void ReviveInPlace(int hp)
    {
        if (!HasStateAuthority) return;
        isDeadNetworked = false;
        if (statusFx != null) statusFx.ClearAll();   // xem ghi chú trong ReviveAndRestore
        RPC_RestorePhysicsAfterRevive(rb != null ? (Vector3)rb.position : transform.position, doWarp: false);
        if (statsComp != null) statsComp.CurrentHP = Mathf.Max(1, hp);
        else HP = Mathf.Max(1, hp);
        GrantReviveInvincibility(3.0f); // BR-18
    }

    private void Die()
    {
        isDeadNetworked = true;

        // Đếm số lần chết để đẩy lên web. Chạy ở StateAuthority (cả 2 call site của Die() đều nằm
        // trong RPC RpcTargets.StateAuthority) nên host ghi được cả cho client. Cả 2 call site cũng
        // early-return khi isDeadNetworked → không cộng trùng cho cùng 1 lần chết.
        if (statsComp != null) statsComp.DeathCount += 1;
    }

    /// <summary>
    /// Hồi sinh: bật lại physics/collider đã tắt khi chết (xác nằm đất set Kinematic + disable collider).
    /// Phải chạy trên MỌI peer (bodyType/collider là state local, không [Networked]) nên gọi qua RPC.
    /// Không khôi phục → bug "rơi xuyên đất / bay đứng giữa trời" sau respawn.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RestorePhysicsAfterRevive(Vector3 warpTarget, NetworkBool doWarp)
    {
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = normalGravity;
        }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        // Gắn lại Visual vào root + khôi phục interpolation target (lúc chết đã tách ra chống rung).
        ReattachCorpseVisual();

        // Set LẠI camera follow cho local player khi có TELEPORT (respawn checkpoint). Teleport là
        // DEFERRED (áp trong FUN tick sau), nên KHÔNG warp ngay ở đây (transform.position còn ở chỗ chết
        // → sai). Dùng coroutine đợi teleport áp xong rồi SNAP camera thẳng về player.
        if (doWarp && HasInputAuthority)
        {
            StartCoroutine(SnapCameraAfterRespawn(warpTarget));
        }

        // Reset animator: chết bật state "Player_Death" + có thể còn anim.speed=0 (charge attack dở).
        // Không reset → sprite kẹt ở tư thế nằm/xác dù logic đã sống lại. IsDead=false được Render đẩy
        // mỗi frame, nhưng anim.speed phải tự tay bật lại.
        if (animationComp != null) animationComp.ResetForRevive();
    }

    /// <summary>Sau respawn (teleport deferred): đợi player thực sự tới gần vị trí spawn rồi SET Follow
    /// + SNAP camera thẳng về đó. Không dựa vào OnTargetObjectWarped (chạy trước teleport → sai delta).
    /// Đợi vài frame cho FUN áp teleport, rồi ForceCameraPosition để camera nhảy tức thì, không kẹt/lết.</summary>
    private System.Collections.IEnumerator SnapCameraAfterRespawn(Vector3 target)
    {
        if (_cam == null) _cam = FindAnyObjectByType<CinemachineCamera>();
        var cam = _cam;
        if (cam == null) yield break;
        // Bám root trong lúc chờ teleport (visualRoot vừa reattach có thể còn lệch), sau khi snap xong
        // mới trả Follow về visualRoot để lấy lại nội suy mượt.
        cam.Follow = transform;

        // Đợi tối đa ~30 frame tới khi root đã teleport về gần target (teleport deferred sang FUN).
        for (int i = 0; i < 30; i++)
        {
            if (Vector2.Distance(transform.position, target) < 0.5f) break;
            yield return null;
        }

        // Snap camera thẳng về player — giữ z của camera. ForceCameraPosition = API Cinemachine 3.
        Vector3 camTarget = new Vector3(transform.position.x, transform.position.y, cam.transform.position.z);
        cam.ForceCameraPosition(camTarget, cam.transform.rotation);

        if (!_corpseVisualDetached && visualRoot != null) SetCameraFollow(visualRoot);
    }

    /// <summary>Chống rung XÁC CHẾT trên client: tách Visual khỏi cây networked. Chạy trên MỌI peer
    /// (local + proxy). Visual sau khi SetParent(null) không còn cha, không phải NetworkObject, đứng
    /// yên tại world pos → không addon nội suy / prediction nào chạm tới → tuyệt đối không rung.
    /// Idempotent: chỉ tách 1 lần.</summary>
    private void DetachCorpseVisual()
    {
        if (_corpseVisualDetached || visualRoot == null) return;
        _corpseVisualDetached = true;
        var nrb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody2D>();
        if (nrb != null) nrb.SetInterpolationTarget(null); // addon thôi chase Visual
        visualRoot.SetParent(null, worldPositionStays: true); // tách khỏi root networked

        // Camera đang bám visualRoot (xem Spawned) — xác đã tách khỏi cây networked nên phải chuyển
        // Follow về root, nếu không respawn sẽ kẹt camera tại chỗ chết.
        SetCameraFollow(transform);
    }

    /// <summary>Gắn lại Visual vào root sau khi sống lại (đảo ngược DetachCorpseVisual). Reset local
    /// transform về mặc định (detach worldPositionStays đã đổi local pos/scale) + gán lại interpolation
    /// target. Idempotent. Chạy trên MỌI peer.</summary>
    private void ReattachCorpseVisual()
    {
        if (!_corpseVisualDetached) return; // chưa tách thì thôi (tránh chạy mỗi tick cho player sống)
        _corpseVisualDetached = false;
        if (visualRoot != null)
        {
            visualRoot.SetParent(transform, worldPositionStays: false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }
        var nrb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody2D>();
        if (nrb != null) nrb.SetInterpolationTarget(visualRoot);

        // Visual đã về cây → camera bám lại target nội suy (giữ mượt như lúc mới spawn).
        if (visualRoot != null) SetCameraFollow(visualRoot);
    }

    /// <summary>Dịch chuyển player về vị trí (điểm rest / spawn checkpoint). Chỉ host. Ghi ý định vào
    /// [Networked] rồi áp dụng trong FixedUpdateNetwork (xem ApplyPendingTeleport) để TeleportKey của
    /// NetworkRigidbody2D sync đúng mọi peer — client SNAP đúng thay vì bị prediction đè.</summary>
    public void TeleportTo(Vector3 position)
    {
        if (!HasStateAuthority) return;

        // Set rb.position ngay để host và trường hợp chưa in-sim (spawn từ coroutine) vào đúng chỗ.
        rb.position = position;
        rb.linearVelocity = Vector2.zero;

        // Ghi ý định teleport (tăng seq) → FUN sẽ gọi nrb.Teleport() đúng thời điểm in-sim trên mọi
        // peer, kể cả CLIENT đang prediction. Không gọi Teleport() trực tiếp ở đây vì có thể đang ở
        // coroutine / RPC delivery (Teleport no-op hoặc ném lỗi).
        _pendingTeleportPos = position;
        _pendingTeleportSeq++;
    }

    /// <summary>Áp dụng teleport đang chờ trong FUN (in-sim). Chạy trên MỌI peer: seq đổi = có teleport
    /// mới → gọi nrb.Teleport() (TeleportKey sync, bypass prediction).</summary>
    private void ApplyPendingTeleport()
    {
        if (_appliedTeleportSeq == _pendingTeleportSeq) return;
        _appliedTeleportSeq = _pendingTeleportSeq;

        if (Object != null && Object.IsInSimulation)
        {
            var nrb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody2D>();
            if (nrb != null) { nrb.Teleport(_pendingTeleportPos); return; }
        }
        // Fallback (chưa in-sim): set thẳng.
        rb.position = _pendingTeleportPos;
        rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// BR-38/39: rơi vào bẫy môi trường. Trừ 15% Max HP rồi đưa về điểm đất an toàn cuối.
    /// Gọi từ Hazard (trigger).
    ///
    /// KHÔNG chặn theo `isInvincible`: hazard là RANH GIỚI MAP, không phải damage thường. Trước đây player
    /// rơi xuống trong lúc đang bất tử (vừa trúng đòn / vừa hồi sinh 3s) thì hazard bị bỏ qua HOÀN TOÀN
    /// nên không ai kéo player lên → kẹt dưới map. Giờ vẫn luôn được kéo lên; phần damage mới xét bất tử.
    /// </summary>
    public void HazardHit()
    {
        if (IsQuestDialogueProtected || isDeadNetworked) return;
        RPC_HazardHit();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_HazardHit()
    {
        if (IsQuestDialogueProtected || isDeadNetworked) return;

        // Mọi peer đều gọi RPC này cho cùng một lần rơi → chốt ở host để chỉ tính một lần.
        if (!_hazardCooldown.ExpiredOrNotRunning(Runner)) return;
        _hazardCooldown = TickTimer.CreateFromSeconds(Runner, 1f);

        // BR-39: KÉO LÊN TRƯỚC khi xét damage. Nếu teleport nằm sau `Die()` thì cú hazard chí mạng để lại
        // xác DƯỚI map, mà collider tắt khi chết nên đồng đội không tới đủ gần để hồi sinh → mất tiến trình.
        if (_lastStableGround != Vector2.zero) TeleportTo(_lastStableGround);
        else if (Attrition.Gameplay.World.Checkpoint.MostRecentlyActivated != null)
            TeleportTo(Attrition.Gameplay.World.Checkpoint.MostRecentlyActivated.RespawnPosition);

        // Bất tử từ combat/hồi sinh vẫn miễn damage — chỉ không còn ngăn việc được kéo lên.
        if (isInvincible) return;

        int max = statsComp != null ? statsComp.MaxHP : maxHP;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(max * 0.15f)); // BR-38
        HP -= dmg;

        if (HP <= 0) Die();

        // ponytail: chỉ cứu được khi hazard bắt được player. Trần: rơi lọt quá mép hazard thì KHÔNG hệ
        // thống nào phát hiện. Nâng cấp: thêm fall-plane (kill-Y) cho mỗi map.
    }

    /// <summary>Client/host yêu cầu chuyển tới checkpoint ở scene khác; Host tự xác thực registry/discovery.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestCrossMapFastTravel(string targetScene, string checkpointName)
    {
        var registry = Attrition.Gameplay.Environment.MapRegistrySO.Load();
        var map = registry != null ? registry.GetByScene(targetScene) : null;
        bool validCheckpoint = map != null && map.checkpoints.Exists(cp => cp.checkpointId == checkpointName);
        if (!validCheckpoint
            || !Attrition.Gameplay.Environment.WorldMapState.IsCheckpointDiscovered(checkpointName)
            || !Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogWarning($"[FastTravel] Từ chối đích không hợp lệ/chưa khám phá: '{targetScene}' / '{checkpointName}'.");
            return;
        }

        Attrition.Gameplay.Environment.WorldMapState.PendingTravelScene = targetScene;
        Attrition.Gameplay.Environment.WorldMapState.PendingTravelCheckpointId = checkpointName;
        RpcTravelLoading();

        var launcher = Attrition.Networking.NetworkLauncher.Instance;
        if (launcher != null) launcher.BeginGameplay(targetScene);
        else Debug.LogWarning("[FastTravel] Không tìm thấy NetworkLauncher.");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcRequestWorldMapDiscovery()
    {
        foreach (var checkpoint in Attrition.Gameplay.Environment.WorldMapState.AllDiscoveredCheckpoints)
            RpcSyncDiscoveredCheckpoint(checkpoint);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSyncDiscoveredCheckpoint(string checkpointName)
    {
        Attrition.Gameplay.Environment.WorldMapState.MarkCheckpointDiscovered(checkpointName);
    }

    /// <summary>Client/host yêu cầu Fast Travel (không save — dùng cho room transition).</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestFastTravel(Vector3 destination)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            // Player chết → hồi sinh đầy đủ tại đích (giống rest); còn sống → chỉ teleport.
            if (p.IsDead) p.ReviveAndRestore(destination);
            else p.TeleportTo(destination);
        }
        // Báo CẢ HAI máy hiện thanh load (người không bấm cũng bị teleport → tránh giật, không loading).
        RpcTravelLoading();
    }

    /// <summary>
    /// Client/host yêu cầu Fast Travel ĐẾN checkpoint CỤ THỂ. Sau khi teleport xong, host LƯU lại
    /// checkpoint đó (solo + coop) để khi out ra vào lại → spawn đúng chỗ đã teleport gần nhất.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestFastTravelToCheckpoint(Vector3 destination, string checkpointName)
    {
        // Fast-travel đến checkpoint = ĐÚNG NHƯ REST (yêu cầu user): hồi đầy HP/Mana/Stamina + refill
        // bình cho mọi player, người đang gục thì hồi sinh tại đích.
        Attrition.Gameplay.World.Checkpoint.RestoreAllPlayersAt(destination);

        // Quái cũng reset y như rest: quái đã chết sống lại, quái đang aggro về vị trí gốc. Thiếu bước
        // này thì teleport về checkpoint xong thế giới vẫn giữ nguyên trạng — đúng lỗi user báo.
        Attrition.Gameplay.World.Checkpoint.ResetEnemiesExceptBoss();

        RpcTravelLoading();

        // Đặt checkpoint đích làm lastCheckpoint (RespawnPosition + HasBeenActivated + MostRecently).
        // Thiếu 2 cờ đầu thì chết sau khi teleport sẽ hồi sinh về checkpoint khác trong scene.
        var checkpoints = FindObjectsByType<Attrition.Gameplay.World.Checkpoint>(FindObjectsSortMode.None);
        foreach (var cp in checkpoints)
        {
            if (cp != null && cp.DisplayName == checkpointName)
            {
                cp.MarkAsLastCheckpoint();
                break;
            }
        }

        // LƯU tiến trình: solo → local JSON, coop → server. Ghi đè checkpoint đã lưu bằng
        // checkpoint mới teleport để lần sau vào game spawn đúng chỗ này.
        var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
        saver.Save(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Rest,
                   checkpointName, destination);
    }

    /// <summary>Host báo mọi peer hiện thanh load fast-travel đồng bộ.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcTravelLoading()
    {
        Attrition.Controllers.CoopFeedbackEvents.RaiseTravelLoading("Travelling...");
    }

    /// <summary>Resume sau Game Over: host hồi sinh mọi player tại checkpoint đã kích hoạt + reset quái.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestRespawnAll()
    {
        // Checkpoint chỉ hợp lệ khi object đó còn thuộc scene hiện tại. Qua map mới mà chưa rest thì
        // dùng spawnPoint của map mới, không kéo người chơi về checkpoint map cũ hoặc Vector3.zero.
        var recent = Attrition.Gameplay.World.Checkpoint.MostRecentlyActivated;
        bool hasCheckpoint = recent != null
                             && recent.gameObject.scene.name == Attrition.Persistence.GameLaunch.GameplayScene
                             && recent.HasBeenActivated;
        if (!hasCheckpoint)
        {
            recent = FindObjectsByType<Attrition.Gameplay.World.Checkpoint>(FindObjectsSortMode.None)
                .FirstOrDefault(cp => cp != null && cp.HasBeenActivated);
            hasCheckpoint = recent != null;
        }

        var spawner = FindFirstObjectByType<NetworkSpawner>();
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            Vector3 spawn = hasCheckpoint ? recent.RespawnPosition : p.transform.position;
            if (!hasCheckpoint && spawner != null && p.Object != null
                && !spawner.TryGetDefaultSpawn(p.Object.InputAuthority, out spawn))
            {
                Debug.LogError("[Respawn] Map hiện tại chưa cấu hình spawn point; giữ vị trí player thay vì dịch về (0,0).");
            }
            p.ReviveAndRestore(spawn);
        }

        // Bắn loading về CẢ HAI máy (giống Rest/fast-travel) — màn loading che đúng lúc camera snap về
        // checkpoint nên không thấy cảnh camera kẹt/underground trong lúc chuyển. Camera follow lại đúng
        // sau khi loading tắt (ReviveAndRestore đã set cam.Follow + warp).
        RpcTravelLoading();

        // Despawn quái còn sống (trừ boss) rồi spawn lại — dùng chung với rest/fast-travel.
        Attrition.Gameplay.World.Checkpoint.ResetEnemiesExceptBoss();

        // BOSS: đặt sẵn trong scene nên KHÔNG despawn/respawn như quái thường → phải reset tay.
        // Hồi đầy HP + trả AI về chờ trigger (ẩn thanh máu) + MỞ LẠI cửa phòng boss, nếu không player
        // hồi sinh sẽ gặp boss máu dở, không thanh máu, cửa khoá.
        ResetLivingBossesAfterWipe();
    }

    /// <summary>
    /// Reset mọi boss CÒN SỐNG sau khi cả team chết. Ưu tiên BossGateController (nó quản cả cửa vào);
    /// boss không có gate thì reset trực tiếp. Boss đã bị hạ giữ nguyên (không hồi sinh). Chỉ host.
    /// </summary>
    private static void ResetLivingBossesAfterWipe()
    {
        // 1) Boss có cổng: gate lo cả mở cửa + reset boss + reset trigger.
        var gates = FindObjectsByType<Attrition.Gameplay.Environment.BossGateController>(FindObjectsSortMode.None);
        foreach (var gate in gates)
            if (gate != null) gate.ResetEncounterAfterWipe();

        // 2) Boss KHÔNG có gate (vd Druid đặt trần trong scene): reset trực tiếp.
        foreach (var enemy in FindObjectsByType<Attrition.Controllers.EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead) continue;
            var es = enemy.GetComponent<Attrition.Gameplay.Enemy.EnemyStats>();
            if (es == null || es.Tier != Attrition.Data.EnemyTier.Boss) continue;

            // Mọi AI boss implement IBossEncounter (SF/Druid/Elf/DemonKin/ArchDemon) — một đường chung
            // thay cho if-else từng loại.
            var bossAI = enemy.GetComponent<Attrition.Core.IBossEncounter>();

            // Đã được gate xử lý ở bước 1 → bỏ qua để không reset hai lần.
            bool handledByGate = false;
            foreach (var gate in gates)
                if (gate != null && gate.Boss == enemy) { handledByGate = true; break; }
            if (handledByGate) continue;

            enemy.ResetForEncounterRetry();
            var bc = enemy.GetComponent<Attrition.Controllers.BossController>();
            if (bc != null) bc.ResetPhases();
            bossAI?.ResetEncounter();

            // Cho phép kích hoạt lại trigger vào phòng. So sánh qua interface nên khớp được MỌI loại boss
            // (trước chỉ so với sfAI → boss Druid trở đi không bao giờ được reset trigger).
            foreach (var trig in FindObjectsByType<Attrition.Gameplay.Environment.BossEncounterTrigger>(FindObjectsSortMode.None))
                if (trig != null && bossAI != null && trig.BossEncounter == bossAI) trig.ResetTrigger();
        }
    }

    IEnumerator InvincibleCoroutine()
    {
        isInvincible = true;
        StartCoroutine(animationComp.BlinkRoutine(invincibleDuration));
        yield return new WaitForSeconds(invincibleDuration);
        isInvincible = false;
    }

    /// <summary>BR-18: 3s bất tử khi hồi sinh/respawn tại checkpoint. Gọi từ revive/respawn.</summary>
    public void GrantReviveInvincibility(float duration = 3.0f)
    {
        StartCoroutine(TimedInvincibility(duration));
    }

    private IEnumerator TimedInvincibility(float duration)
    {
        isInvincible = true;
        if (animationComp != null) StartCoroutine(animationComp.BlinkRoutine(duration));
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }

    // IGNORE ENEMY COLLIDERS — Đảm bảo Player đi xuyên qua quái

    /// <summary>
    /// Tìm tất cả Enemy trong scene và ignore collision với collider của chúng.
    /// Gọi khi Spawned() để xử lý Enemy đã có sẵn.
    /// </summary>
    private void IgnoreAllEnemyColliders()
    {
        Collider2D myCol = GetComponent<Collider2D>();
        if (myCol == null) return;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            IgnoreCollidersWithObject(myCol, enemy.gameObject);
        }
    }

    /// <summary>
    /// Khi Player va chạm vật lý với bất kỳ object nào có EnemyController → ignore collision ngay.
    /// Xử lý Enemy spawn sau Player.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        EnemyController enemy = collision.gameObject.GetComponentInParent<EnemyController>();
        if (enemy == null) enemy = collision.gameObject.GetComponent<EnemyController>();
        if (enemy != null)
        {
            Collider2D myCol = GetComponent<Collider2D>();
            if (myCol != null)
            {
                IgnoreCollidersWithObject(myCol, enemy.gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!HasInputAuthority) return;
        var cp = other.GetComponentInParent<Attrition.Gameplay.World.Checkpoint>();
        if (cp != null)
        {
            _currentCheckpoint = cp;
            RpcSetAtCheckpoint(true);   // báo host: được phép đổi accessory
        }

        var npc = other.GetComponentInParent<Attrition.Gameplay.NPC.NetworkNPC>();
        if (npc != null) _currentNPC = npc;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!HasInputAuthority) return;
        var cp = other.GetComponentInParent<Attrition.Gameplay.World.Checkpoint>();
        if (cp != null && cp == _currentCheckpoint)
        {
            _currentCheckpoint = null;
            RpcSetAtCheckpoint(false);   // rời checkpoint → khoá đổi accessory lại
        }

        var npc = other.GetComponentInParent<Attrition.Gameplay.NPC.NetworkNPC>();
        if (npc != null && npc == _currentNPC) _currentNPC = null;
    }

    /// <summary>
    /// Ignore tất cả non-trigger collider trên 1 GameObject (bao gồm children).
    /// </summary>
    private void IgnoreCollidersWithObject(Collider2D myCol, GameObject target)
    {
        Collider2D[] cols = target.GetComponentsInChildren<Collider2D>();
        foreach (var col in cols)
        {
            // Chỉ ignore non-trigger collider (trigger dùng cho ContactDamage, cần giữ)
            if (!col.isTrigger)
            {
                Physics2D.IgnoreCollision(myCol, col, true);
            }
        }
    }

    // GIZMOS — Debug Visualization

    void OnDrawGizmosSelected()
    {
        // 1. Vẽ Stand Size (Màu Xanh Lá) - Biểu diễn kích thước lúc Đứng
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Xanh lá mờ
        Vector3 standCenter = transform.position + (Vector3)standOffset;
        Gizmos.DrawWireCube(standCenter, standSize);
        // Tô mờ ở trong
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Gizmos.DrawCube(standCenter, standSize);

        // 2. Vẽ Crouch Size (Màu Vàng) - Biểu diễn kích thước lúc Ngồi/Trượt
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f); // Vàng đậm
        Vector3 crouchCenter = transform.position + (Vector3)crouchOffset;
        Gizmos.DrawWireCube(crouchCenter, crouchSize);
        // Tô mờ ở trong
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.15f);
        Gizmos.DrawCube(crouchCenter, crouchSize);

        // 3. Vẽ Ceiling Check (Màu Đỏ) - Khu vực kiểm tra trần nhà chống kẹt
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        float crouchTop = transform.position.y + crouchOffset.y + (crouchSize.y / 2f);
        float standTop = transform.position.y + standOffset.y + (standSize.y / 2f);
        float diff = standTop - crouchTop;
        if (diff > 0)
        {
            Vector2 checkSize = new Vector2(standSize.x * 0.9f, diff);
            Vector2 checkCenter = new Vector2(transform.position.x + standOffset.x, crouchTop + (diff / 2f));
            Gizmos.DrawWireCube(checkCenter, checkSize);
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawCube(checkCenter, checkSize);
        }
    }
}