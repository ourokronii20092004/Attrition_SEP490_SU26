using Fusion;
using UnityEngine;
using Attrition.Controllers;
using Attrition.Gameplay.Enemy;
using Attrition.Gameplay.Enemy.ArchDemon.States;

namespace Attrition.Gameplay.Enemy.ArchDemon
{
    /// <summary>
    /// AI Boss ARCH DEMON (Boss 5 — boss cuối, hệ NƯỚC + BÓNG TỐI) — mirror kiến trúc
    /// <see cref="Druid.DruidBossAI"/>.
    ///
    /// TÍNH CÁCH: di chuyển ÍT NHẤT trong 5 boss, skill DÀY NHẤT (recoveryTime 0.4s — so với DemonKin 0.5,
    /// Elf 0.65, Druid 1.0). Vẫn giữ được khoảng né vì mỗi skill đều có pha báo trước (water startup) hoặc
    /// charge riêng — dồn dập nhưng không phải không thể đọc.
    ///
    /// Moveset (5 skill):
    ///  - Skill 1 DarkOrb: 1 quả cầu bóng tối bay thẳng về phía trước.
    ///  - Skill 2 WaterBallTriple: 3 cầu nước — 1 từ boss, 2 từ phía đối diện xa bay NGƯỢC về boss (kẹp giữa).
    ///  - Skill 3 WaterBlast: 3 lốc xoáy lần lượt chạy tới rìa phòng rồi QUAY LẠI boss, trúng = chậm 30%.
    ///  - Skill 4 WaterSpike: 6-7 cọc nước mọc từ dưới lên, lần lượt tới cuối phòng (có startup báo trước).
    ///  - Skill 5 WaterSplash: đánh ngay dưới chân mục tiêu, 3 lần LIÊN TIẾP (lần sau chờ lần trước xong).
    /// </summary>
    public class ArchDemonBossAI : EnemyAI, Attrition.Core.IBossEncounter
    {
        [Header("═══ ARCH DEMON — BOSS SETTINGS ═══")]

        // SKILL 1 (Dark Orb) KHÔNG có ô prefab: quả cầu đã vẽ sẵn trong clip ArchDemon_BasicAttack
        // (bung ra ở frame 8). Xem AD_DarkOrbState + DarkOrbFrameTime.
        [Header("---- PREFABS (skill 2-5) ----")]
        [Tooltip("SKILL 2 — cầu nước lúc NÉM (art: WaterBall - Startup and Infinite). Có EnemyProjectile.")]
        [SerializeField] private NetworkPrefabRef waterBallPrefab;
        [Tooltip("SKILL 2 — vụ va đập khi cầu nước trúng đích/rìa map (art: WaterBall - Impact). " +
                 "Có EnemyAoEDamage. Bỏ trống = không có hiệu ứng nổ.")]
        [SerializeField] private NetworkPrefabRef waterBallImpactPrefab;
        [Tooltip("SKILL 3 — lốc xoáy nước (art: Water Blast - Startup and Infinite). Có EnemyAoEDamage; " +
                 "AI tự di chuyển nó đi-về nên đặt lifetime dài hơn thời gian đi-về.")]
        [SerializeField] private NetworkPrefabRef waterBlastPrefab;
        [Tooltip("SKILL 3 — hiệu ứng lốc TAN khi về tới boss (art: Water Blast - End). Có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef waterBlastEndPrefab;
        [Tooltip("SKILL 4 — dấu báo trước (art: Water StartUp 1). Có EnemyAoEDamage, damage 0 = chỉ báo hiệu.")]
        [SerializeField] private NetworkPrefabRef waterStartup1Prefab;
        [Tooltip("SKILL 4 — cọc nước mọc lên (art: Water spike 1). Có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef waterSpikePrefab;
        [Tooltip("SKILL 5 — dấu báo trước (art: Water StartUp 2). Có EnemyAoEDamage, damage 0 = chỉ báo hiệu.")]
        [SerializeField] private NetworkPrefabRef waterStartup2Prefab;
        [Tooltip("SKILL 5 — vụ nổ nước dưới chân mục tiêu (art: Water splash 1). Có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef waterSplashPrefab;

        [Header("---- TARGETING (COOP) ----")]
        [Tooltip("Coop: bám 1 player trong khoảng này (giây) rồi mới xét lại ai gần nhất. Solo bỏ qua.")]
        [SerializeField] private float targetRetargetInterval = 4f;
        private float _retargetCooldown;

        [Header("---- MELEE (basic attack) ----")]
        [Tooltip("Phạm vi kích hoạt đòn cận chiến cơ bản.")]
        public float meleeRange = 2.6f;
        [Tooltip("Sát thương đòn cận chiến.")]
        public int meleeDamage = 26;
        [Tooltip("Thời gian khoá state trong lúc chơi animation attack (giây).")]
        public float meleeDuration = 0.7f;

        [Header("---- SKILL 1: DARK ORB (cầu nằm trong animation, không có prefab) ----")]
        [Tooltip("Tầm sát thương (units) phía trước boss lúc cầu rời tay. Art chỉ vẽ cầu tới mép sprite " +
                 "nên để ngắn — kéo dài thành sát thương vô hình.")]
        public float orbRange = 4f;
        [Tooltip("Tổng thời gian khoá state cho skill 1 (giây). Clip attack dài 2s → để >= 2 kẻo cắt giữa động tác.")]
        public float orbTotalTime = 2f;
        [Tooltip("Sát thương cầu bóng tối.")]
        public int orbDamage = 28;

        [Header("---- SKILL 2: WATER BALL x3 (1 từ boss + 2 từ xa bay về) ----")]
        [Tooltip("Thời gian lấy đà (giây).")]
        public float ballChargeTime = 0.5f;
        [Tooltip("Khoảng cách (units) từ boss tới điểm spawn 2 quả PHÍA ĐỐI DIỆN.")]
        public float ballFarDistance = 11f;
        [Tooltip("Khoảng lệch DỌC (units) giữa 2 quả phía đối diện — tạo khe để player lách.")]
        public float ballFarGapY = 2f;
        [Tooltip("Tốc độ cầu nước (NHANH theo yêu cầu). 0 = giữ tốc độ prefab.")]
        public float ballSpeed = 18f;
        [Tooltip("Sát thương mỗi cầu nước.")]
        public int ballDamage = 24;

        [Header("---- SKILL 3: WATER BLAST (3 lốc đi rồi quay về) ----")]
        [Tooltip("Số lốc xoáy (yêu cầu: 3).")]
        public int blastCount = 3;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 lốc — chúng đi LẦN LƯỢT, không cùng lúc.")]
        public float blastInterval = 0.55f;
        [Tooltip("Khoảng lệch DỌC (units) giữa các lốc để player thấy khoảng cách nhất định.")]
        public float blastGapY = 1.5f;
        [Tooltip("Thời gian lấy đà trước lốc đầu (giây).")]
        public float blastChargeTime = 0.5f;
        [Tooltip("Tốc độ lốc di chuyển (units/giây) khi đi ra rìa và khi quay về.")]
        public float blastMoveSpeed = 9f;
        [Tooltip("Hệ số tốc độ CÒN LẠI khi player trúng lốc (0.7 = chậm 30% theo yêu cầu).")]
        [Range(0.1f, 1f)] public float blastSlowFactor = 0.7f;
        [Tooltip("Thời gian bị làm chậm (giây).")]
        public float blastSlowDuration = 2.5f;
        [Tooltip("Bán kính lốc chạm player để áp hiệu ứng chậm (units).")]
        public float blastTouchRadius = 1.2f;
        [Tooltip("Sát thương mỗi lốc.")]
        public int blastDamage = 22;

        [Header("---- SKILL 4: WATER SPIKE (cọc mọc tới cuối phòng) ----")]
        [Tooltip("Số cọc nước (6-7).")]
        public int spikeCount = 7;
        [Tooltip("Khoảng cách (units) giữa 2 cọc liên tiếp.")]
        public float spikeSpacing = 2.4f;
        [Tooltip("Khoảng cách (units) từ boss tới cọc đầu tiên.")]
        public float spikeFirstOffset = 2f;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 cọc — mọc LẦN LƯỢT chạy ra xa.")]
        public float spikeInterval = 0.16f;
        [Tooltip("Thời gian dấu báo hiện trước khi cọc mọc (giây) — cửa sổ để player rời chỗ.")]
        public float spikeStartupLead = 0.35f;
        [Tooltip("Sát thương mỗi cọc.")]
        public int spikeDamage = 24;

        [Header("---- SKILL 5: WATER SPLASH (3 lần dưới chân mục tiêu) ----")]
        [Tooltip("Số lần thi triển (yêu cầu: 3, lần lượt sau khi lần trước kết thúc).")]
        public int splashRepeats = 3;
        [Tooltip("Thời gian dấu báo hiện trước khi nổ (giây) — cửa sổ để player rời chỗ.")]
        public float splashStartupLead = 0.5f;
        [Tooltip("Nghỉ giữa 2 lần thi triển (giây).")]
        public float splashGap = 0.45f;
        [Tooltip("Sát thương mỗi lần.")]
        public int splashDamage = 26;

        [Header("---- ROOM ----")]
        [Tooltip("Nửa chiều rộng dự phòng (units) khi phòng chưa đặt CameraBoundsZone.")]
        public float roomFallbackHalfWidth = 15f;

        [Header("---- TIMING ----")]
        [Tooltip("Nghỉ giữa các skill (giây). Boss cuối → dồn dập nhất.")]
        public float recoveryTime = 0.4f;
        [Tooltip("Thời gian ĐI LẠI tự do sau khi nghỉ, trước khi tung skill kế (giây). Trước đây cooldown để " +
                 "0.1s nên skill chồng chéo, chưa xong đòn này đã tung đòn khác.")]
        public float restTime = 1.3f;
        [Tooltip("Khoảng cách (units) boss muốn giữ với player trong lúc nghỉ.")]
        public float preferredDistance = 6.5f;
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

        bool Attrition.Core.IBossEncounter.EncounterStarted => EncounterStarted;
        public bool IsWaitingForTrigger => waitForTrigger;

        // ═══════════════════════════════════════════════════════════════
        // STATE MACHINE
        // ═══════════════════════════════════════════════════════════════

        private ArchDemonBossState _currentState;

        public static readonly AD_IdleState IdleState = new AD_IdleState();
        public static readonly AD_ChaseState ChaseState = new AD_ChaseState();
        public static readonly AD_RecoveryState RecoveryState = new AD_RecoveryState();
        public static readonly AD_MeleeAttackState MeleeAttackState = new AD_MeleeAttackState();
        public static readonly AD_DarkOrbState DarkOrbState = new AD_DarkOrbState();
        public static readonly AD_WaterBallState WaterBallState = new AD_WaterBallState();
        public static readonly AD_WaterBlastState WaterBlastState = new AD_WaterBlastState();
        public static readonly AD_WaterSpikeState WaterSpikeState = new AD_WaterSpikeState();
        public static readonly AD_WaterSplashState WaterSplashState = new AD_WaterSplashState();

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

        // DarkOrbPrefab đã bỏ — skill 1 dùng quả cầu vẽ sẵn trong clip attack, không spawn prefab.
        public NetworkPrefabRef WaterBallPrefab => waterBallPrefab;
        public NetworkPrefabRef WaterBallImpactPrefab => waterBallImpactPrefab;
        public NetworkPrefabRef WaterBlastPrefab => waterBlastPrefab;
        public NetworkPrefabRef WaterBlastEndPrefab => waterBlastEndPrefab;
        public NetworkPrefabRef WaterStartup1Prefab => waterStartup1Prefab;
        public NetworkPrefabRef WaterSpikePrefab => waterSpikePrefab;
        public NetworkPrefabRef WaterStartup2Prefab => waterStartup2Prefab;
        public NetworkPrefabRef WaterSplashPrefab => waterSplashPrefab;

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

        /// <summary>Thoại mở đầu phát trên MỌI máy (client cũng phải thấy — xem bug đã sửa ở boss 1).</summary>
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
            animationComp?.FaceDirection(NetFacingDir);
        }

        public void ChangeState(ArchDemonBossState newState)
        {
            _currentState?.Exit(this);
            _currentState = newState;
            _currentState?.Enter(this);
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER DETECTION
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

        public void StopMovement()
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
        }

        // ═══════════════════════════════════════════════════════════════
        // SPAWN HELPERS (host-only)
        // ═══════════════════════════════════════════════════════════════

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

        /// <summary>
        /// Spawn AoE và TRẢ VỀ NetworkObject để state tự di chuyển nó (skill 3: lốc xoáy đi ra rồi quay về).
        /// Các skill khác dùng SpawnAoE (fire-and-forget) vì không cần giữ tham chiếu.
        /// </summary>
        public NetworkObject SpawnAoETracked(NetworkPrefabRef prefab, Vector2 pos, int damage,
                                             float lifetime = 0f)
        {
            if (!HasStateAuthority || !prefab.IsValid) return null;
            return Runner.Spawn(prefab, pos, Quaternion.identity, null, (runner, obj) =>
            {
                if (lifetime > 0f)
                {
                    var aoe = obj.GetComponent<EnemyAoEDamage>();
                    if (aoe != null) aoe.lifetime = lifetime;
                }
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, Vector2.zero, BossStatDamage(Attrition.Core.DamageType.Magic),
                    Attrition.Gameplay.Combat.ProjectileInitializer.DefaultSpeed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // STATUS EFFECT (skill 3 — làm chậm)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Làm chậm mọi player còn sống trong bán kính quanh `center`. Chỉ host.
        /// `PlayerStatusEffects` là [Networked] nên client tự đọc và tự kẹp tốc độ chạy của mình.
        /// </summary>
        public void SlowPlayersInRadius(Vector2 center, float radius, float factor, float duration)
        {
            if (!HasStateAuthority) return;

            foreach (var pr in Runner.ActivePlayers)
            {
                var pObj = Runner.GetPlayerObject(pr);
                if (pObj == null) continue;
                var pc = pObj.GetComponent<PlayerController>();
                if (pc == null || pc.IsDead) continue;
                if (Vector2.Distance(pObj.transform.position, center) > radius) continue;

                var fx = pObj.GetComponent<Attrition.Gameplay.Player.PlayerStatusEffects>();
                if (fx != null) fx.ApplySlow(factor, duration);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ANIMATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Thời điểm (giây, tính từ đầu clip `ArchDemon_BasicAttack`) mà QUẢ CẦU BÓNG TỐI bung ra khỏi tay.
        ///
        /// ĐO TỪ ART, không phải phỏng đoán: clip dài 2s / 15 frame; frame thứ 8 (sprite
        /// `ArchDemonBasicAtk001-Sheet_7`) có bounding box **48px** trong khi mọi frame khác chỉ 43-45px —
        /// đúng 3px nhô thêm là quả cầu vừa rời tay. Các frame 9-15 trở lại 43px (cầu đã bay ra khỏi khung
        /// sprite nên không còn được vẽ).
        /// </summary>
        public const float DarkOrbFrameTime = 1.1667f;

        /// <summary>
        /// Cắt animation attack về Idle tại đây (trước <see cref="DarkOrbFrameTime"/> một nhịp) cho các skill
        /// KHÔNG phải Dark Orb. Nhờ vậy 4 skill nước vẫn có động tác vung tay mà không kéo theo quả cầu.
        /// 1.05s = hết frame 7, chừa ~0.12s an toàn trước lúc cầu xuất hiện.
        /// </summary>
        public const float AttackAnimCutTime = 1.05f;

        /// <summary>
        /// Trigger animation trên mọi máy. LƯU Ý: Animator của ArchDemon dùng trigger CHỮ THƯỜNG
        /// ("attack"/"hurt"/"die") khác boss 1/2 ("Attack"). Hàm này thử cả 2 cách viết nên state chỉ cần
        /// gọi PlayAnim("Attack") giống các boss khác.
        /// </summary>
        public void PlayAnim(string triggerName)
        {
            RPC_AD_PlayAnim(triggerName);
        }

        /// <summary>
        /// Chạy animation attack CÓ quả cầu bóng tối — chỉ dùng cho skill 1 (Dark Orb). Cầu nằm sẵn trong
        /// clip nên skill 1 KHÔNG cần prefab đạn nào.
        /// </summary>
        public void PlayAttackAnimWithOrb() => RPC_AD_PlayAttack(0f);

        /// <summary>
        /// Chạy animation attack rồi CẮT về Idle trước khi cầu bóng tối bung ra — dùng cho mọi skill khác
        /// (melee + 4 skill nước). Không cắt thì mỗi skill nước đều kèm một quả cầu bóng tối bay ra, vì tất
        /// cả đều dùng chung clip `ArchDemon_BasicAttack`.
        /// </summary>
        public void PlayAttackAnimNoOrb() => RPC_AD_PlayAttack(AttackAnimCutTime);

        /// <summary>
        /// Bật trigger Attack trên mọi máy; cutAfter &gt; 0 thì hẹn giờ bật trigger Idle để cắt clip.
        ///
        /// Cắt bằng coroutine CỤC BỘ (không networked) vì đây thuần phần hình: mỗi máy tự chạy clip của
        /// mình, giống cách ShadowDashEffect vẽ afterimage. Dùng TickTimer sẽ phải nhồi thêm state networked
        /// cho một việc không ảnh hưởng gameplay.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AD_PlayAttack(float cutAfter)
        {
            if (animationComp == null) return;
            var anim = animationComp.GetComponentInChildren<Animator>();
            if (anim == null) return;

            if (!TrySetTrigger(anim, "Attack")) TrySetTrigger(anim, "attack");

            if (_attackCutRoutine != null) StopCoroutine(_attackCutRoutine);
            if (cutAfter > 0f) _attackCutRoutine = StartCoroutine(CutAttackAnim(anim, cutAfter));
        }

        private Coroutine _attackCutRoutine;

        private const string IdleAnimState = "ArchDemon_Idle";

        private System.Collections.IEnumerator CutAttackAnim(Animator anim, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            _attackCutRoutine = null;
            if (anim == null) yield break;
            anim.CrossFade(IdleAnimState, 0.05f, 0, 0f);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AD_PlayAnim(string triggerName)
        {
            if (animationComp == null) return;
            var anim = animationComp.GetComponentInChildren<Animator>();
            if (anim == null || string.IsNullOrEmpty(triggerName)) return;
            if (triggerName == "Idle")
            {
                anim.CrossFade(IdleAnimState, 0.05f, 0, 0f);
                return;
            }

            // Thử đúng tên trước, rồi bản chữ thường (animator ArchDemon dùng "attack").
            if (TrySetTrigger(anim, triggerName)) return;
            TrySetTrigger(anim, char.ToLowerInvariant(triggerName[0]) + triggerName.Substring(1));
        }

        private static bool TrySetTrigger(Animator anim, string name)
        {
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == name)
                {
                    anim.SetTrigger(name);
                    return true;
                }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        // SKILL RANDOMIZER
        // ═══════════════════════════════════════════════════════════════

        private readonly System.Collections.Generic.List<int> _skillBag = new System.Collections.Generic.List<int>();

        /// <summary>Shuffle bag 5 skill — player gặp đủ moveset thay vì trùng lặp như random thuần.</summary>
        public void PickRandomSkill()
        {
            float dist = DistanceToPlayer();

            // Áp sát → melee hoặc dập nước ngay dưới chân (skill 5 đánh đúng chỗ đứng).
            if (dist >= 0 && dist <= meleeRange)
            {
                if (Random.value < 0.4f) { ChangeState(MeleeAttackState); return; }
                ChangeState(WaterSplashState);
                return;
            }

            if (_skillBag.Count == 0)
                for (int i = 0; i < 5; i++) _skillBag.Add(i);

            int idx = Random.Range(0, _skillBag.Count);
            int pick = _skillBag[idx];
            _skillBag.RemoveAt(idx);

            switch (pick)
            {
                case 0: ChangeState(DarkOrbState); break;
                case 1: ChangeState(WaterBallState); break;
                case 2: ChangeState(WaterBlastState); break;
                case 3: ChangeState(WaterSpikeState); break;
                default: ChangeState(WaterSplashState); break;
            }
        }
    }
}
