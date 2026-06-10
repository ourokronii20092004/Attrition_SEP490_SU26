using Fusion;
using UnityEngine;
using System.Collections;
using System.Linq;
using Unity.Cinemachine;
using Attrition.Controllers;
using Attrition.Gameplay.Player;

/// <summary>
/// Đồng bộ vật lý giữa Host và Client:
/// - Input Authority (người chơi local): dùng physics prediction bình thường.
/// - Proxy (người chơi khác nhìn thấy trên màn hình bạn): nhận vị trí + velocity từ server.
/// </summary>
public class PlayerController : NetworkBehaviour, IDamageable
{
    [Header("---- INJECT COMPONENTS ----")]
    [SerializeField] private PlayerCombat combatComp;
    [SerializeField] private PlayerAnimation animationComp;
    [SerializeField] private Attrition.Gameplay.Player.PlayerSkillCaster skillCaster;
    [Tooltip("Tùy chọn: nguồn chỉ số runtime. Bỏ trống = dùng maxHP serialized bên dưới.")]
    [SerializeField] private PlayerStats statsComp;
    [Tooltip("Tùy chọn: hệ thống bình HP/Mana. Bỏ trống = không có bình (prefab cũ).")]
    [SerializeField] private PotionSystem potionComp;

    // Checkpoint đang đứng trong vùng (local, không cần [Networked] — chỉ dùng để gate input R).
    private Attrition.Gameplay.World.Checkpoint _currentCheckpoint;

    [Header("---- MOVEMENT & PHYSICS ----")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask groundLayer;

    [Header("---- ADVANCED MOVEMENT ----")]
    [Tooltip("Bật để Dash có I-Frames (không nhận sát thương khi đang lướt)")]
    public bool hasShadowDash = false;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldownTime = 0.8f;
    [SerializeField] private float crouchSpeedMultiplier = 0.4f;
    [SerializeField] private float variableJumpCutMultiplier = 0.5f;
    [SerializeField] private int maxJumps = 2;

    [Header("---- SLIDE ----")]
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float slideCooldownTime = 1f;

    [Header("---- HITBOX RESIZING (CROUCH/SLIDE) ----")]
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Vector2 standSize = new Vector2(1f, 2f);
    [SerializeField] private Vector2 standOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 crouchSize = new Vector2(1f, 1f);
    [SerializeField] private Vector2 crouchOffset = new Vector2(0f, -0.5f);

    [Header("---- HOLLOW KNIGHT GRAVITY ----")]
    [Tooltip("Trọng lực mặc định khi đi trên mặt đất hoặc bay lên")]
    [SerializeField] private float normalGravity = 2f;
    [Tooltip("Trọng lực khi rơi xuống (càng cao rơi càng nhanh, Hollow Knight ~4-5)")]
    [SerializeField] private float fallGravity = 4.5f;
    [Tooltip("Tốc độ rơi tối đa (giới hạn để không rơi quá nhanh)")]
    [SerializeField] private float maxFallSpeed = -25f;

    [Header("---- KNOCKBACK (KHI PLAYER BỊ ĐÁNH) ----")]
    [Tooltip("Lực đẩy lùi khi bị quái đánh (set 0 để không bị knockback)")]
    [SerializeField] private float knockbackForceOverride = 6f;
    [Tooltip("Thời gian bị khựng không điều khiển được sau khi bị đánh (giây)")]
    [SerializeField] private float knockbackDuration = 0.25f;
    [Tooltip("Thời gian bất tử sau khi bị đánh (giây)")]
    [SerializeField] private float invincibleDuration = 0.8f;

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

    [Networked] private NetworkButtons _buttonsPrev { get; set; }
    [Networked] private TickTimer _dashTimer { get; set; }
    [Networked] private TickTimer _dashCooldown { get; set; }
    [Networked] private TickTimer _slideTimer { get; set; }
    [Networked] private TickTimer _slideCooldown { get; set; }
    [Networked] private float _slideDirection { get; set; }
    [Networked] private TickTimer _knockbackTimer { get; set; }
    [Networked] private Vector2 _lastStableGround { get; set; }

    // ─── Đồng bộ vị trí cho proxy ───
    [Networked] public Vector2 NetworkPosition { get; set; }
    [Networked] public Vector2 NetworkVelocity { get; set; }
    [Networked] public float NetworkGravityScale { get; set; }

    public int maxHP = 100;
    private bool isInvincible = false;

    public bool IsDead => isDeadNetworked;

    /// <summary>True khi player đang đứng trong vùng 1 checkpoint (UI hiện gợi ý [F] OPTIONS).</summary>
    public bool IsAtCheckpoint => _currentCheckpoint != null;

    /// <summary>Tên checkpoint đang đứng (UI hiển thị). Rỗng nếu không ở checkpoint nào.</summary>
    public string CurrentCheckpointName => _currentCheckpoint != null ? _currentCheckpoint.DisplayName : "";

    /// <summary>UI gọi khi bấm REST trong panel checkpoint: hồi đầy + lưu (host xử lý qua RPC).</summary>
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
        // statsComp tự init CurrentHP=MaxHP trong Spawned của nó. Chỉ tự init khi KHÔNG có statsComp.
        if (HasStateAuthority && statsComp == null) currentHP = maxHP;

        if (combatComp == null) combatComp = GetComponent<PlayerCombat>();
        if (skillCaster == null) skillCaster = GetComponent<Attrition.Gameplay.Player.PlayerSkillCaster>();
        if (animationComp == null) animationComp = GetComponent<PlayerAnimation>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();

        // Tắt va chạm vật lý giữa Player và Enemy để Player đi xuyên qua được
        // CHỈ dùng Collider-based (không dùng IgnoreLayerCollision vì nó chặn cả trigger → ContactDamage không hoạt động)
        IgnoreAllEnemyColliders();

        // Set camera to follow local player
        if (HasInputAuthority)
        {
            var cam = FindAnyObjectByType<CinemachineCamera>();
            if (cam != null)
            {
                cam.Follow = transform;
            }
        }

        // Mũi tên P1/P2 trên đầu — chỉ hiện khi coop (>1 người). Local = P1 (xanh), remote = P2 (cam).
        if (Runner != null && Runner.ActivePlayers.Count() > 1)
        {
            bool isLocal = HasInputAuthority;
            PlayerMarker.Attach(transform, isLocal ? "P1" : "P2",
                isLocal ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.95f, 0.6f, 0.25f));
        }
    }

    public override void FixedUpdateNetwork()
    {
        // SOLO pause: đóng băng player (Fusion bỏ qua Time.timeScale).
        if (Attrition.Persistence.GamePause.IsPaused)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        CheckGround();
        NetworkVelocityY = rb.linearVelocity.y;

        // ─── Đồng bộ vị trí/velocity cho proxy ───
        if (HasStateAuthority)
        {
            NetworkPosition = rb.position;
            NetworkVelocity = rb.linearVelocity;
            NetworkGravityScale = rb.gravityScale;

            // Hồi stamina theo thời gian (concept: 10/s). Null-safe nếu chưa gán statsComp.
            if (statsComp != null) statsComp.RegenStamina(Runner.DeltaTime);
        }
        else if (!HasInputAuthority)
        {
            // Proxy: ép vị trí và velocity từ server, bỏ qua toàn bộ physics logic
            rb.position = NetworkPosition;
            rb.linearVelocity = NetworkVelocity;
            rb.gravityScale = NetworkGravityScale;
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
            // SỬA LỖI XÁC BAY: Khi chết, dừng di chuyển ngang nhưng vẫn để trọng lực kéo xuống
            // Chỉ đóng băng hoàn toàn khi đã chạm đất
            if (IsGrounded)
            {
                if (rb.bodyType != RigidbodyType2D.Kinematic)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    Collider2D col = GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                }
            }
            else
            {
                // Đang rơi xuống: dừng ngang, giữ trọng lực rơi tự nhiên
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                rb.gravityScale = fallGravity;
                // Giới hạn tốc độ rơi
                if (rb.linearVelocity.y < maxFallSpeed)
                {
                    rb.linearVelocity = new Vector2(0f, maxFallSpeed);
                }
            }
            return;
        }

        // --- KNOCKBACK: Khóa toàn bộ input khi đang bị đẩy lùi ---
        if (!_knockbackTimer.ExpiredOrNotRunning(Runner))
        {
            NetworkVelocityY = rb.linearVelocity.y;
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
                else
                {
                    float mSpeed = statsComp != null ? statsComp.MoveSpeed : 10f;
                    float speed = IsCrouching ? mSpeed * crouchSpeedMultiplier : mSpeed;
                    rb.linearVelocity = new Vector2(data.horizontalInput * speed, rb.linearVelocity.y);
                    IsMoving = Mathf.Abs(data.horizontalInput) > 0.1f;
                }

                // --- JUMP LOGIC ---
                if (pressed.IsSet(MyButtons.Jump) && !IsCrouching && !combatComp.IsHoldingAttack)
                {
                    if (IsGrounded || JumpCount < maxJumps)
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
            if (pressed.IsSet(MyButtons.Dash) && !combatComp.IsHoldingAttack)
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
        animationComp.UpdateAnimations(
            IsMoving, IsGrounded, isDeadNetworked, NetworkVelocityY, IsFacingRight,
            IsCrouching, IsDashing, combatComp.IsChargingAttack,
            combatComp.IsAttacking, IsSliding
        );
    }

    private void CheckGround()
    {
        // Dùng rb.Cast vì nó tự động dùng đúng physics scene của Fusion
        // Fix góc đất được xử lý bằng cách clamp velocity Y ở trên
        IsGrounded = rb.Cast(Vector2.down, new ContactFilter2D { layerMask = groundLayer, useLayerMask = true }, new RaycastHit2D[1], 0.05f) > 0;

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
        if (isInvincible || isDeadNetworked || (hasShadowDash && IsDashing)) return;
        RPC_TakeDamage(damage, knockbackDir, knockbackForce, (int)type);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, int type)
    {
        if (isDeadNetworked || (hasShadowDash && IsDashing)) return;

        // damage = chỉ số tấn công GỐC; defender tự áp DEF (Physical) hoặc RES (Magic).
        int def = statsComp != null ? statsComp.DEF : 0;
        int res = statsComp != null ? statsComp.RES : 0;
        int taken = Attrition.Core.DamageCalculator.Compute((Attrition.Core.DamageType)type, damage, def, res);
        HP -= taken;

        // Luôn dùng knockbackForceOverride từ Inspector để điều chỉnh lực đẩy lùi
        RPC_ApplyKnockback(knockbackDir, knockbackForceOverride);
        if (HP <= 0) Die();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyKnockback(Vector2 dir, float force)
    {
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

    private void Die()
    {
        isDeadNetworked = true;
    }

    /// <summary>Dịch chuyển player về vị trí (vd điểm rest). Set cả rb lẫn NetworkPosition để sync. Chỉ host.</summary>
    public void TeleportTo(Vector3 position)
    {
        if (!HasStateAuthority) return;
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        NetworkPosition = position;
        NetworkVelocity = Vector2.zero;
    }

    /// <summary>
    /// BR-38/39: rơi vào bẫy môi trường. Trừ 15% Max HP rồi đưa về điểm đất an toàn cuối.
    /// Gọi từ Hazard (trigger). Bỏ qua nếu đang bất tử/đã chết.
    /// </summary>
    public void HazardHit()
    {
        if (isInvincible || isDeadNetworked) return;
        RPC_HazardHit();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_HazardHit()
    {
        if (isDeadNetworked) return;

        int max = statsComp != null ? statsComp.MaxHP : maxHP;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(max * 0.15f)); // BR-38
        HP -= dmg;

        if (HP <= 0) { Die(); return; }

        // BR-39: đưa về điểm đất an toàn cuối (nếu có).
        if (_lastStableGround != Vector2.zero)
            TeleportTo(_lastStableGround);

        StartCoroutine(InvincibleCoroutine());
    }

    /// <summary>Client/host yêu cầu Fast Travel. Host dịch chuyển TẤT CẢ player (giữ chung khung camera coop).</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestFastTravel(Vector3 destination)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players) p.TeleportTo(destination);
    }

    /// <summary>Resume sau Game Over: host hồi sinh mọi player tại checkpoint đã kích hoạt + reset quái.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestRespawnAll()
    {
        var checkpoints = FindObjectsByType<Attrition.Gameplay.World.Checkpoint>(FindObjectsSortMode.None);
        var active = checkpoints.FirstOrDefault(cp => cp.HasBeenActivated);
        Vector3 spawn = active != null ? active.RespawnPosition : Vector3.zero;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            p.isDeadNetworked = false;
            p.TeleportTo(spawn);
            var st = p.GetComponent<PlayerStats>();
            if (st != null) st.RestoreFull();
            var pot = p.GetComponent<PotionSystem>();
            if (pot != null) pot.RefillAll();
            p.GrantReviveInvincibility(3.0f); // BR-18
        }

        var spawner = FindFirstObjectByType<NetworkSpawner>();
        if (spawner != null) spawner.RespawnConfiguredEnemies();
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

    // ═══════════════════════════════════════════════════════════════
    // IGNORE ENEMY COLLIDERS — Đảm bảo Player đi xuyên qua quái
    // ═══════════════════════════════════════════════════════════════

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

    // ─── CHECKPOINT: track vùng đang đứng để gate phím R ───
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!HasInputAuthority) return;
        var cp = other.GetComponentInParent<Attrition.Gameplay.World.Checkpoint>();
        if (cp != null) _currentCheckpoint = cp;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!HasInputAuthority) return;
        var cp = other.GetComponentInParent<Attrition.Gameplay.World.Checkpoint>();
        if (cp != null && cp == _currentCheckpoint) _currentCheckpoint = null;
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

    // ═══════════════════════════════════════════════════════════════
    // GIZMOS — Debug Visualization
    // ═══════════════════════════════════════════════════════════════

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