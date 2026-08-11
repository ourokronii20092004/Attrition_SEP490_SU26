using Fusion;
using UnityEngine;
using Attrition.Controllers;
using Attrition.Gameplay.Enemy;
using Attrition.Gameplay.Enemy.Elf.States;

namespace Attrition.Gameplay.Enemy.Elf
{
    /// <summary>
    /// AI Boss ELF (Boss 3, hệ SẤM) — mirror kiến trúc <see cref="Druid.DruidBossAI"/>: kế thừa EnemyAI,
    /// ghi đè RunAILogic để chạy State Machine riêng.
    ///
    /// TÍNH CÁCH (theo yêu cầu): ÍT DI CHUYỂN, dồn skill nhanh. Cụ thể: recoveryTime ngắn hơn Druid
    /// (0.65s vs 1s), và ChaseState chỉ dùng khi player ra ngoài tầm rất xa — mọi skill đều đánh từ xa.
    ///
    /// Moveset (5 skill):
    ///  - Skill 1 ThunderArrow: 3 mũi tên sấm bay SONG SONG ngang, có khoảng cách dọc để player nhảy vào khe.
    ///  - Skill 2 ThunderBird: vận sức (animation chững) rồi bắn 1 CHIM SẤM to cao/thấp ngẫu nhiên.
    ///  - Skill 3 ThunderWave: nổ các quả cầu theo hình chữ W-W, nổ LẦN LƯỢT từ đầu tới cuối.
    ///  - Skill 4 ThunderSplash: hoá sấm → hiện ra ngay chỗ player → bắn 2 mũi tên sang 2 HƯỚNG.
    ///  - Skill 5 ThunderStrike: sét giáng khắp chiều ngang phòng, 2 lượt (lượt 2 rơi vào chỗ vừa né).
    ///
    /// Damage đến từ EnemyProjectile (đạn bay) / EnemyAoEDamage (nổ đứng yên) gắn trên chính prefab skill —
    /// AI chỉ Spawn + Init, nên "add prefab vào là chạy".
    /// </summary>
    public class ElfBossAI : EnemyAI, Attrition.Core.IBossEncounter
    {
        [Header("═══ ELF — BOSS SETTINGS ═══")]

        [Header("---- PREFABS (5 skill) ----")]
        [Tooltip("SKILL 1 — mũi tên sấm bay ngang (art: Thunder Projectile 1). Prefab có EnemyProjectile.")]
        [SerializeField] private NetworkPrefabRef thunderArrowPrefab;
        [Tooltip("SKILL 2 — chim sấm lớn (art: Projectile 2). Prefab có EnemyProjectile.")]
        [SerializeField] private NetworkPrefabRef thunderBirdPrefab;
        [Tooltip("SKILL 3 — quả cầu sấm nổ tại chỗ (art: Thunder Hit). Prefab có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef thunderHitPrefab;
        [Tooltip("SKILL 4 — vệt sấm lúc dịch chuyển (art: Thunder Splash). Prefab có EnemyAoEDamage " +
                 "(đặt damage 0 nếu chỉ muốn làm hiệu ứng).")]
        [SerializeField] private NetworkPrefabRef thunderSplashPrefab;
        [Tooltip("SKILL 5 — cột sét giáng từ trên trời (art: Thunder Strike). Prefab có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef thunderStrikePrefab;

        public const float SkillAttackWindup = 0.85f; // đúng m_StopTime của Elf_Basic_Attack.anim

        [Header("---- ANIMATION STATES ----")]
        [SerializeField] private string idleAnimState = "Elf_Idle";
        [SerializeField] private string walkAnimState = "Elf_Walk";
        [SerializeField] private string attackAnimState = "Elf_Basic_Attack";

        [Header("---- TARGETING (COOP) ----")]
        [Tooltip("Coop: bám 1 player trong khoảng này (giây) rồi mới xét lại ai gần nhất. Solo bỏ qua.")]
        [SerializeField] private float targetRetargetInterval = 4f;
        private float _retargetCooldown;

        [Header("---- MELEE (basic attack) ----")]
        [Tooltip("Phạm vi kích hoạt đòn cận chiến cơ bản.")]
        public float meleeRange = 2f;
        [Tooltip("Sát thương đòn cận chiến.")]
        public int meleeDamage = 18;
        [Tooltip("Thời gian khoá state trong lúc chơi animation Attack (giây).")]
        public float meleeDuration = 0.65f;

        [Header("---- SKILL 1: THUNDER ARROW (3 mũi song song) ----")]
        [Tooltip("Số mũi tên bắn song song.")]
        public int arrowCount = 3;
        [Tooltip("Khoảng cách DỌC (units) giữa 2 mũi — phải đủ rộng để player nhảy vào khe giữa.")]
        public float arrowGapY = 1.8f;
        [Tooltip("Thời gian giương cung trước khi bắn (giây).")]
        public float arrowChargeTime = 0.35f;
        [Tooltip("Tốc độ mũi tên. 0 = giữ tốc độ prefab.")]
        public float arrowSpeed = 16f;
        [Tooltip("Sát thương mỗi mũi tên.")]
        public int arrowDamage = 16;

        [Header("---- SKILL 2: THUNDER BIRD (vận sức rồi bắn) ----")]
        [Tooltip("Thời gian VẬN SỨC — animation chững lại ở đầu cung (giây).")]
        public float birdChargeTime = 1.1f;
        [Tooltip("Độ cao BAY CAO so với tâm boss (units) — buộc player NGỒI xuống để né.")]
        public float birdHighOffsetY = 1.4f;
        [Tooltip("Độ cao BAY THẤP so với tâm boss (units) — buộc player NHẢY qua để né.")]
        public float birdLowOffsetY = -0.9f;
        [Tooltip("Tốc độ chim sấm. 0 = giữ tốc độ prefab.")]
        public float birdSpeed = 11f;
        [Tooltip("Sát thương chim sấm.")]
        public int birdDamage = 30;

        [Header("---- SKILL 3: THUNDER WAVE (nổ hình chữ W-W) ----")]
        [Tooltip("Số quả cầu trong chuỗi W-W (8 = đúng 2 chữ W).")]
        public int waveCount = 8;
        [Tooltip("Khoảng cách NGANG (units) giữa 2 quả liên tiếp.")]
        public float waveStepX = 1.9f;
        [Tooltip("Biên độ DỌC (units) của chữ W (đỉnh cao so với đáy).")]
        public float waveAmplitudeY = 2.2f;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 quả — nổ LẦN LƯỢT từ đầu tới cuối.")]
        public float waveInterval = 0.13f;
        [Tooltip("Sát thương mỗi quả cầu.")]
        public int waveDamage = 20;

        [Header("---- SKILL 4: THUNDER SPLASH (dịch chuyển tới player) ----")]
        [Tooltip("Thời gian tan thành sấm trước khi biến mất (giây).")]
        public float splashVanishTime = 0.3f;
        [Tooltip("Thời gian hiện lại thành hình dáng ban đầu (giây).")]
        public float splashAppearTime = 0.35f;
        [Tooltip("Đứng cách player bao xa khi hiện ra (units) — 0 = ngay trên đầu player.")]
        public float splashArriveOffsetX = 0.6f;
        [Tooltip("Nghỉ một nhịp sau khi hiện hình rồi mới bắn 2 mũi (giây).")]
        public float splashShootDelay = 0.25f;
        [Tooltip("Sát thương vệt sấm lúc dịch chuyển (0 = chỉ là hiệu ứng).")]
        public int splashDamage = 12;

        [Header("---- SKILL 5: THUNDER STRIKE (2 lượt so le) ----")]
        [Tooltip("Số cột sét mỗi lượt (mặc định 5). Lượt 2 giáng vào các KHE của lượt 1 (đảo lại).")]
        public int strikeColumns = 5;
        [Tooltip("Khoảng cách (units) giữa 2 cột sét — 3-4 tile để player có khe né.")]
        public float strikeSpacing = 4.5f;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 cột sét trong cùng 1 lượt — thấy rõ cột hiện LẦN LƯỢT.")]
        public float strikeInterval = 0.22f;
        [Tooltip("Nghỉ giữa lượt 1 và lượt 2 (giây) — 1-2s để player kịp đổi chỗ né lượt đảo.")]
        public float strikeWaveGap = 1.5f;
        [Tooltip("Nửa chiều rộng dự phòng (units) khi phòng chưa đặt CameraBoundsZone.")]
        public float strikeFallbackHalfWidth = 14f;
        [Tooltip("Độ cao spawn cột sét so với boss (units) — chỉ để hình rơi từ trên; AoE tự hạ xuống đất.")]
        public float strikeSpawnHeight = 6f;
        [Tooltip("Sát thương mỗi cột sét.")]
        public int strikeDamage = 24;

        [Header("---- TIMING ----")]
        [Tooltip("Nghỉ giữa các skill (giây). Elf dồn skill nhanh → để ngắn hơn Druid.")]
        public float recoveryTime = 0.65f;
        [Tooltip("Thời gian ĐI LẠI tự do sau khi nghỉ, trước khi tung skill kế (giây). Elf ít di chuyển nên " +
                 "để ngắn hơn Druid, nhưng phải có nhịp nghỉ nếu không thành bắn liên tục.")]
        public float restTime = 1.3f;
        [Tooltip("Khoảng cách (units) Elf muốn giữ với player trong lúc đi lại nghỉ.")]
        public float preferredDistance = 7f;
        [Tooltip("Chờ ban đầu trước skill đầu tiên (giây).")]
        public float initialDelay = 1.2f;

        [Header("---- INTRO ----")]
        [Tooltip("Đợi trigger mới bắt đầu? Bật = boss đứng im tới khi StartIntroSequence được gọi.")]
        public bool waitForTrigger = true;
        [Tooltip("File thoại mở đầu (tùy chọn).")]
        public Attrition.Data.DialogueSO introDialogue;

        // ═══════════════════════════════════════════════════════════════
        // NETWORKED STATE
        // ═══════════════════════════════════════════════════════════════

        [Networked] public TickTimer SkillCooldownTimer { get; set; }
        [Networked] public NetworkBool EncounterStarted { get; set; }

        // IBossEncounter: bridge NetworkBool → bool (xem ghi chú trong SeveredFangAI).
        bool Attrition.Core.IBossEncounter.EncounterStarted => EncounterStarted;
        public bool IsWaitingForTrigger => waitForTrigger;

        // ═══════════════════════════════════════════════════════════════
        // STATE MACHINE
        // ═══════════════════════════════════════════════════════════════

        private ElfBossState _currentState;

        public static readonly E_IdleState IdleState = new E_IdleState();
        public static readonly E_ChaseState ChaseState = new E_ChaseState();
        public static readonly E_RecoveryState RecoveryState = new E_RecoveryState();
        public static readonly E_MeleeAttackState MeleeAttackState = new E_MeleeAttackState();
        public static readonly E_ThunderArrowState ThunderArrowState = new E_ThunderArrowState();
        public static readonly E_ThunderBirdState ThunderBirdState = new E_ThunderBirdState();
        public static readonly E_ThunderWaveState ThunderWaveState = new E_ThunderWaveState();
        public static readonly E_ThunderSplashState ThunderSplashState = new E_ThunderSplashState();
        public static readonly E_ThunderStrikeState ThunderStrikeState = new E_ThunderStrikeState();

        /// <summary>Bộ đếm cục bộ dùng chung cho state (không sync — chỉ host chạy AI).</summary>
        [HideInInspector] public float StateLocalTimer;

        // ═══════════════════════════════════════════════════════════════
        // ACCESSORS
        // ═══════════════════════════════════════════════════════════════

        public Rigidbody2D Rb => rb;
        public EnemyAnimation AnimComp => animationComp;
        public EnemyController Controller => controller;
        public EnemyStats StatsComp => statsComp;
        public Transform PlayerTarget => playerTarget;
        public Vector2 StartPos => startPosition;

        public NetworkPrefabRef ThunderArrowPrefab => thunderArrowPrefab;
        public NetworkPrefabRef ThunderBirdPrefab => thunderBirdPrefab;
        public NetworkPrefabRef ThunderHitPrefab => thunderHitPrefab;
        public NetworkPrefabRef ThunderSplashPrefab => thunderSplashPrefab;
        public NetworkPrefabRef ThunderStrikePrefab => thunderStrikePrefab;

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        public override void Spawned()
        {
            base.Spawned();

            if (HasStateAuthority)
                SkillCooldownTimer = TickTimer.CreateFromSeconds(Runner, initialDelay);

            if (!waitForTrigger)
            {
                if (HasStateAuthority) EncounterStarted = true;
                ChangeState(IdleState);
            }
        }

        public void StartIntroSequence()
        {
            if (!HasStateAuthority || !waitForTrigger) return;
            waitForTrigger = false;
            EncounterStarted = true;
            if (introDialogue != null) BroadcastIntroDialogue();
            else ChangeState(IdleState);
        }

        /// <summary>
        /// Player wipe (cả team chết) → trả boss về trạng thái CHỜ TRIGGER như lúc mới vào phòng.
        /// HP do EnemyController.ResetForEncounterRetry lo. Chỉ host.
        /// </summary>
        public void ResetEncounter()
        {
            if (!HasStateAuthority) return;

            ChangeState(null);
            waitForTrigger = true;
            EncounterStarted = false;

            playerTarget = null;
            _retargetCooldown = 0f;
            StateLocalTimer = 0f;
            SkillCooldownTimer = TickTimer.CreateFromSeconds(Runner, initialDelay);

            StopMovement();
            if (rb != null) rb.position = StartPos;
        }

        /// <summary>
        /// Phát thoại mở đầu trên MỌI máy. Client cũng phải thấy (bug đã sửa ở boss 1: client chỉ thấy
        /// thoại kết thúc) nên phải qua RPC chứ không gọi trực tiếp phía host.
        /// </summary>
        public void BroadcastIntroDialogue()
        {
            if (!HasStateAuthority || introDialogue == null) return;
            RPC_ShowIntroDialogue();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowIntroDialogue()
        {
            if (introDialogue == null) return;
            var openDialogue = Attrition.Data.DialogueEvents.OnOpenCustomDialogue;
            if (openDialogue == null)
            {
                if (HasStateAuthority) ChangeState(IdleState);
                return;
            }
            openDialogue.Invoke(introDialogue, () =>
            {
                if (HasStateAuthority) ChangeState(IdleState);
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // AI LOGIC
        // ═══════════════════════════════════════════════════════════════

        public override void RunAILogic()
        {
            if (controller != null && controller.IsKnockbackActive)
            {
                if (_currentState != RecoveryState && _currentState != IdleState)
                    ChangeState(RecoveryState);
                NetSpeed = Mathf.Abs(rb.linearVelocity.x);
                return;
            }

            _currentState?.Update(this);
        }

        public override void Render()
        {
            if (controller == null) return;
            if (controller.isDeadNetworked || controller.IsAwaitingRevive)
            {
                animationComp?.UpdateSpeed(0f);
                return;
            }
            animationComp?.UpdateSpeed(NetSpeed);
            if (CurrentState != EnemyState.Attacking)
                animationComp?.PlayState(NetSpeed > 0.05f ? walkAnimState : idleAnimState);
            animationComp?.FaceDirection(NetFacingDir);
        }

        public void ChangeState(ElfBossState newState)
        {
            _currentState?.Exit(this);
            _currentState = newState;
            _currentState?.Enter(this);
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER DETECTION (mirror Druid)
        // ═══════════════════════════════════════════════════════════════

        public void DetectPlayer()
        {
            if (Attrition.Persistence.GameLaunch.Mode != Attrition.Persistence.LaunchMode.Coop)
            {
                FindPlayer();
                return;
            }

            _retargetCooldown -= Runner.DeltaTime;
            bool currentValid = IsLivingPlayer(playerTarget)
                && Vector2.Distance(transform.position, playerTarget.position) <= viewRadius * 1.05f;
            if (currentValid && _retargetCooldown > 0f) return;

            Transform nearest = FindNearestPlayer();
            if (nearest != null)
            {
                playerTarget = nearest;
                _retargetCooldown = targetRetargetInterval;
            }
            else playerTarget = null;
        }

        private Transform FindNearestPlayer()
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var pr in Runner.ActivePlayers)
            {
                var pObj = Runner.GetPlayerObject(pr);
                if (pObj == null) continue;
                var pc = pObj.GetComponent<PlayerController>();
                if (pc == null || pc.IsDead) continue;
                float d = Vector2.Distance(transform.position, pObj.transform.position);
                if (d > viewRadius) continue;
                if (d < bestDist) { bestDist = d; best = pObj.transform; }
            }
            return best;
        }

        public void FaceTowardsPlayer()
        {
            if (playerTarget == null) return;
            float xDiff = playerTarget.position.x - transform.position.x;
            float bossDeadZone = Mathf.Max(facingDeadZone, 1.2f);
            if (Mathf.Abs(xDiff) > bossDeadZone)
            {
                NetFacingDir = xDiff > 0 ? 1f : -1f;
                AttackLockedFacingDir = NetFacingDir;
            }
        }

        public float DistanceToPlayer()
        {
            if (playerTarget == null) return -1f;
            return Vector2.Distance(transform.position, playerTarget.position);
        }

        /// <summary>Hướng ngang tới player (+1/-1); không có mục tiêu thì lấy hướng đang nhìn.</summary>
        public float DirToPlayerX()
        {
            if (playerTarget != null)
                return playerTarget.position.x > transform.position.x ? 1f : -1f;
            return NetFacingDir > 0 ? 1f : -1f;
        }

        // ═══════════════════════════════════════════════════════════════
        // MOVEMENT
        // ═══════════════════════════════════════════════════════════════

        public void MoveTowardsPlayer(float speed)
        {
            if (playerTarget == null) return;
            float xDiff = playerTarget.position.x - transform.position.x;
            float dirX = xDiff > 0 ? 1f : -1f;
            rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
        }

        /// <summary>Lùi xa player (giữ khoảng cách trong lúc nghỉ). Không lùi quá StartPos ±viewRadius*1.5.</summary>
        public void MoveAwayFromPlayer(float speed)
        {
            if (playerTarget == null) return;
            float xDiff = playerTarget.position.x - transform.position.x;
            float dirX = xDiff > 0 ? -1f : 1f;

            // Chặn lùi vô tận ra khỏi phòng boss: giới hạn quanh vị trí spawn.
            float offsetFromStart = transform.position.x - StartPos.x;
            if (Mathf.Abs(offsetFromStart) > viewRadius * 1.5f && Mathf.Sign(offsetFromStart) == Mathf.Sign(dirX))
            {
                StopMovement();
                return;
            }

            rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
        }

        public void StopMovement()
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
        }

        /// <summary>Dịch chuyển tức thời (skill 4). Ghi cả rb.position để NetworkTransform sync xuống client.</summary>
        public void TeleportTo(Vector2 pos)
        {
            if (!HasStateAuthority) return;
            if (rb != null) rb.position = pos;
            else transform.position = pos;
            StopMovement();
        }

        // ═══════════════════════════════════════════════════════════════
        // SPAWN HELPERS (host-only)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Spawn 1 đạn (EnemyProjectile) bay về hướng dir.</summary>
        public void SpawnProjectile(NetworkPrefabRef prefab, Vector2 pos, Vector2 dir, int damage, float speed = 0f)
        {
            if (!HasStateAuthority || !prefab.IsValid) return;
            Runner.Spawn(prefab, pos, Quaternion.identity, null, (runner, obj) =>
            {
                // Damage = AD/AP stats của boss (bỏ qua damage gốc từng skill — user chốt "AD trực tiếp").
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, dir, BossStatDamage(Attrition.Core.DamageType.Magic), speed, Attrition.Core.DamageType.Magic);
            });
        }

        /// <summary>Spawn 1 vùng nổ (EnemyAoEDamage) đứng yên tại pos. snapToGround/damageDelay lấy từ prefab.</summary>
        public void SpawnAoE(NetworkPrefabRef prefab, Vector2 pos, int damage)
        {
            if (!HasStateAuthority || !prefab.IsValid) return;
            Runner.Spawn(prefab, pos, Quaternion.identity, null, (runner, obj) =>
            {
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, Vector2.zero, BossStatDamage(Attrition.Core.DamageType.Magic),
                    Attrition.Gameplay.Combat.ProjectileInitializer.DefaultSpeed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // ANIMATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Trigger animation trên MỌI máy. Animator của Elf có thể CHƯA có param nào (controller gốc là
        /// `m_AnimatorParameters: []`) — tool `Setup Boss Skills` sẽ thêm. Thiếu param thì SetTrigger là
        /// no-op an toàn nhờ kiểm tra `HasAnimTrigger`.
        /// </summary>
        public void PlayAnim(string triggerName)
        {
            RPC_Elf_PlayAnim(triggerName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Elf_PlayAnim(string triggerName)
        {
            if (animationComp == null) return;
            if (triggerName == "Attack")
            {
                animationComp.PlayState(attackAnimState, restart: true);
                return;
            }
            if (triggerName == "Idle")
            {
                animationComp.ReturnToIdle();
                animationComp.PlayState(idleAnimState);
                return;
            }

            var anim = animationComp.GetComponentInChildren<Animator>();
            if (anim == null) return;
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
                {
                    anim.SetTrigger(triggerName);
                    return;
                }
        }

        /// <summary>
        /// Đóng băng animation tại frame hiện tại (skill 2: giữ ở đầu cung để "vận sức"). Phải qua RPC vì
        /// animator chạy ĐỘC LẬP trên mỗi máy — chỉ host đóng băng thì client vẫn thấy boss bắn luôn.
        /// </summary>
        public void FreezeAnim()
        {
            if (!HasStateAuthority) return;
            RPC_Elf_FreezeAnim(true);
        }

        /// <summary>Rã đông animation (bắn tiếp các frame còn lại).</summary>
        public void UnfreezeAnim()
        {
            if (!HasStateAuthority) return;
            RPC_Elf_FreezeAnim(false);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Elf_FreezeAnim(NetworkBool freeze)
        {
            if (animationComp == null) return;
            if (freeze) animationComp.FreezeAnimation();
            else animationComp.UnfreezeAnimation();
        }

        // ═══════════════════════════════════════════════════════════════
        // SKILL RANDOMIZER
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Bộ skill xáo trộn (shuffle bag) thay vì random thuần: đảm bảo trong 5 lượt player gặp đủ 5 skill,
        /// không bị trùng 3 lần liền như random đơn thuần (cùng cách đã sửa cho boss 1).
        /// </summary>
        private readonly System.Collections.Generic.List<int> _skillBag = new System.Collections.Generic.List<int>();

        public void PickRandomSkill()
        {
            float dist = DistanceToPlayer();

            // Player áp sát → đánh gần hoặc "nhảy" đi bằng skill 4 để giữ khoảng cách (Elf không thích cận chiến).
            if (dist >= 0 && dist <= meleeRange)
            {
                if (Random.value < 0.45f) { ChangeState(MeleeAttackState); return; }
                ChangeState(ThunderSplashState);
                return;
            }

            if (_skillBag.Count == 0)
                for (int i = 0; i < 5; i++) _skillBag.Add(i);

            int idx = Random.Range(0, _skillBag.Count);
            int pick = _skillBag[idx];
            _skillBag.RemoveAt(idx);

            switch (pick)
            {
                case 0: ChangeState(ThunderArrowState); break;
                case 1: ChangeState(ThunderBirdState); break;
                case 2: ChangeState(ThunderWaveState); break;
                case 3: ChangeState(ThunderSplashState); break;
                default: ChangeState(ThunderStrikeState); break;
            }
        }
    }
}
