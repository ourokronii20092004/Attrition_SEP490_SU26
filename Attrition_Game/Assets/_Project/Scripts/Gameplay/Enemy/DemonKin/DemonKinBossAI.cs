using Fusion;
using UnityEngine;
using Attrition.Controllers;
using Attrition.Gameplay.Enemy;
using Attrition.Gameplay.Enemy.DemonKin.States;

namespace Attrition.Gameplay.Enemy.DemonKin
{
    /// <summary>
    /// AI Boss DEMONKIN (Boss 4, hệ ĐẤT) — mirror kiến trúc <see cref="Druid.DruidBossAI"/>.
    ///
    /// TÍNH CÁCH: di chuyển ÍT HƠN Elf, tần suất skill DÀY HƠN (recoveryTime 0.5s vs Elf 0.65s) — theo
    /// yêu cầu "boss sau càng ít di chuyển, skill càng dồn dập, nhưng vẫn có khoảng để player né và đánh".
    /// Khoảng né nằm trong từng skill (telegraph/charge) chứ không nằm ở thời gian nghỉ.
    ///
    /// Moveset (4 skill):
    ///  - Skill 1 EarthBarrage: 4 đòn liên tiếp, mỗi đòn 1 viên đất bay tới hết chiều ngang phòng.
    ///  - Skill 2 IrregularRock: đất bọc quanh mục tiêu (6 frame) → KHỐNG CHẾ nếu player không ra khỏi
    ///    vùng kịp → nổ gây damage (4 frame cuối) rồi thả ra.
    ///  - Skill 3 EarthBump: nâng địa hình 2 viên MỖI HƯỚNG, tăng dần kích thước, đẩy player ra xa.
    ///  - Skill 4 EarthWall: triệu 4-5 bức tường trồi lên hướng về player, CHẶN ĐƯỢC đạn của player.
    /// </summary>
    public class DemonKinBossAI : EnemyAI, Attrition.Core.IBossEncounter
    {
        [Header("═══ DEMONKIN — BOSS SETTINGS ═══")]

        [Header("---- PREFABS (4 skill) ----")]
        [Tooltip("SKILL 1 — viên đất bay (art: Earth projectile). Prefab có EnemyProjectile.")]
        [SerializeField] private NetworkPrefabRef earthProjectilePrefab;
        [Tooltip("SKILL 2 — đất bọc mục tiêu (art: Irregular rock). Prefab có EnemyAoEDamage; " +
                 "đặt damageDelay ≈ rockEncloseTime để nổ đúng lúc đất gộp lại.")]
        [SerializeField] private NetworkPrefabRef irregularRockPrefab;
        [Tooltip("SKILL 3 — cục đất nâng địa hình (art: Earth Bump). Prefab có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef earthBumpPrefab;
        [Tooltip("SKILL 4 — tường đất (art: Earth Wall). Prefab có EnemyBlockingWall + EnemyAoEDamage, " +
                 "collider ở layer Ground để chặn đạn player.")]
        [SerializeField] private NetworkPrefabRef earthWallPrefab;

        [Header("---- TARGETING (COOP) ----")]
        [Tooltip("Coop: bám 1 player trong khoảng này (giây) rồi mới xét lại ai gần nhất. Solo bỏ qua.")]
        [SerializeField] private float targetRetargetInterval = 4f;
        private float _retargetCooldown;

        [Header("---- MELEE (basic attack) ----")]
        [Tooltip("Phạm vi kích hoạt đòn cận chiến cơ bản.")]
        public float meleeRange = 2.4f;
        [Tooltip("Sát thương đòn cận chiến.")]
        public int meleeDamage = 22;
        [Tooltip("Thời gian khoá state trong lúc chơi animation Attack (giây).")]
        public float meleeDuration = 0.7f;

        [Header("---- SKILL 1: EARTH BARRAGE (4 đòn liên tiếp) ----")]
        [Tooltip("Số đòn đánh liên tiếp (mỗi đòn 1 viên đất).")]
        public int barrageCount = 4;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 đòn — đủ để player thấy và né từng viên.")]
        public float barrageInterval = 0.45f;
        [Tooltip("Thời gian lấy đà trước đòn đầu (giây).")]
        public float barrageChargeTime = 0.35f;
        [Tooltip("Tốc độ viên đất. 0 = giữ tốc độ prefab. Đủ nhanh để bay hết phòng trong lifetime prefab.")]
        public float barrageSpeed = 13f;
        [Tooltip("Sát thương mỗi viên đất.")]
        public int barrageDamage = 20;

        [Header("---- SKILL 2: IRREGULAR ROCK (đất bọc → khống chế → nổ) ----")]
        [Tooltip("Thời gian đất KHÉP LẠI quanh mục tiêu (giây) — player phải ra khỏi vùng trước mốc này. " +
                 "Khớp 6 frame đầu của animation.")]
        public float rockEncloseTime = 0.85f;
        [Tooltip("Bán kính vùng bọc (units) — ra khỏi bán kính này trước khi khép là thoát.")]
        public float rockRadius = 2.2f;
        [Tooltip("Thời gian bị KHỐNG CHẾ sau khi đất khép lại (giây).")]
        public float rockRootDuration = 1.1f;
        [Tooltip("Thời gian từ lúc khép tới lúc NỔ (giây) — khớp 4 frame cuối của animation.")]
        public float rockExplodeDelay = 0.9f;
        [Tooltip("Sát thương lúc đất nổ.")]
        public int rockDamage = 26;

        [Header("---- SKILL 3: EARTH BUMP (nâng địa hình 2 bên) ----")]
        [Tooltip("Số cục đất MỖI HƯỚNG (yêu cầu: 2).")]
        public int bumpPerSide = 2;
        [Tooltip("Khoảng cách (units) từ boss tới cục đất đầu tiên mỗi bên.")]
        public float bumpFirstOffset = 1.8f;
        [Tooltip("Khoảng cách (units) giữa 2 cục cùng bên.")]
        public float bumpStepX = 1.9f;
        [Tooltip("Tỉ lệ phóng to của cục ngoài so với cục trong (tăng kích thước dần ra xa).")]
        public float bumpScaleStep = 1.45f;
        [Tooltip("Giãn cách thời gian (giây) giữa cục trong và cục ngoài — cảm giác đất nâng lan ra.")]
        public float bumpInterval = 0.15f;
        [Tooltip("Thời gian lấy đà trước khi nâng đất (giây).")]
        public float bumpChargeTime = 0.4f;
        [Tooltip("Sát thương mỗi cục đất.")]
        public int bumpDamage = 22;

        [Header("---- SKILL 4: EARTH WALL (tường chặn đạn) ----")]
        [Tooltip("Số bức tường triệu hồi (4-5).")]
        public int wallCount = 5;
        [Tooltip("Khoảng cách (units) giữa 2 bức tường liên tiếp.")]
        public float wallSpacing = 2.8f;
        [Tooltip("Khoảng cách (units) từ boss tới bức tường đầu tiên.")]
        public float wallFirstOffset = 2.5f;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 bức — tường trồi lần lượt về phía player.")]
        public float wallInterval = 0.16f;
        [Tooltip("Thời gian lấy đà trước khi triệu tường (giây).")]
        public float wallChargeTime = 0.45f;
        [Tooltip("Sát thương khi tường trồi lên trúng player.")]
        public int wallDamage = 18;
        [Tooltip("Nửa chiều rộng dự phòng (units) khi phòng chưa đặt CameraBoundsZone.")]
        public float roomFallbackHalfWidth = 14f;

        [Header("---- TIMING ----")]
        [Tooltip("Nghỉ giữa các skill (giây). DemonKin dồn dày hơn Elf.")]
        public float recoveryTime = 0.5f;
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

        private DemonKinBossState _currentState;

        public static readonly DK_IdleState IdleState = new DK_IdleState();
        public static readonly DK_ChaseState ChaseState = new DK_ChaseState();
        public static readonly DK_RecoveryState RecoveryState = new DK_RecoveryState();
        public static readonly DK_MeleeAttackState MeleeAttackState = new DK_MeleeAttackState();
        public static readonly DK_EarthBarrageState EarthBarrageState = new DK_EarthBarrageState();
        public static readonly DK_IrregularRockState IrregularRockState = new DK_IrregularRockState();
        public static readonly DK_EarthBumpState EarthBumpState = new DK_EarthBumpState();
        public static readonly DK_EarthWallState EarthWallState = new DK_EarthWallState();

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

        public NetworkPrefabRef EarthProjectilePrefab => earthProjectilePrefab;
        public NetworkPrefabRef IrregularRockPrefab => irregularRockPrefab;
        public NetworkPrefabRef EarthBumpPrefab => earthBumpPrefab;
        public NetworkPrefabRef EarthWallPrefab => earthWallPrefab;

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
            BroadcastIntroDialogue();
            ChangeState(IdleState);
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
            Attrition.Data.DialogueEvents.OnOpenCustomDialogue?.Invoke(introDialogue, null);
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

        public void ChangeState(DemonKinBossState newState)
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
            bool currentValid = playerTarget != null
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
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, dir, damage, speed, Attrition.Core.DamageType.Magic);
            });
        }

        public void SpawnAoE(NetworkPrefabRef prefab, Vector2 pos, int damage)
        {
            if (!HasStateAuthority || !prefab.IsValid) return;
            Runner.Spawn(prefab, pos, Quaternion.identity, null, (runner, obj) =>
            {
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, Vector2.zero, damage,
                    Attrition.Gameplay.Combat.ProjectileInitializer.DefaultSpeed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        /// <summary>
        /// Spawn đạn và ĐẶT LIFETIME theo quãng đường cần bay (skill 1: "đi đến hết chiều ngang room").
        ///
        /// VÌ SAO: `EnemyProjectile.lifetime` là hằng số trên prefab (mặc định 3s). Phòng rộng thì đạn tan
        /// giữa đường; phòng hẹp thì đạn đã ra ngoài tường vẫn còn sống. Ở đây tính
        /// `lifetime = distance / speed` (+ đệm) nên đạn luôn tới đúng biên phòng rồi mới tan.
        ///
        /// Ghi trong callback OnBeforeSpawned để client nhận đúng giá trị từ frame đầu.
        /// </summary>
        public void SpawnProjectileRanged(NetworkPrefabRef prefab, Vector2 pos, Vector2 dir,
                                          int damage, float speed, float distance)
        {
            if (!HasStateAuthority || !prefab.IsValid) return;
            Runner.Spawn(prefab, pos, Quaternion.identity, null, (runner, obj) =>
            {
                var proj = obj.GetComponent<EnemyProjectile>();
                if (proj != null)
                {
                    // speed = 0 nghĩa là "giữ tốc độ prefab" → phải đọc lại tốc độ thật để tính thời gian.
                    float actualSpeed = speed > 0f ? speed : proj.speed;
                    if (actualSpeed > 0.01f)
                        proj.lifetime = distance / actualSpeed + 0.2f;
                }
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, dir, damage, speed, Attrition.Core.DamageType.Magic);
            });
        }

        /// <summary>
        /// Spawn AoE có PHÓNG TO (skill 3: cục đất càng ra xa càng lớn). Đặt scale trong callback
        /// OnBeforeSpawned để client nhận đúng kích thước ngay từ frame đầu — set sau Spawn thì host và
        /// client lệch nhau một nhịp.
        /// </summary>
        public void SpawnAoEScaled(NetworkPrefabRef prefab, Vector2 pos, int damage, float scale)
        {
            if (!HasStateAuthority || !prefab.IsValid) return;
            Runner.Spawn(prefab, pos, Quaternion.identity, null, (runner, obj) =>
            {
                obj.transform.localScale = obj.transform.localScale * scale;
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, Vector2.zero, damage,
                    Attrition.Gameplay.Combat.ProjectileInitializer.DefaultSpeed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // STATUS EFFECT (skill 2 — khống chế)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Giam mọi player còn sống trong bán kính `radius` quanh `center`. Chỉ host — `PlayerStatusEffects`
        /// là host-authoritative, client tự đọc cờ [Networked] để tự khoá input di chuyển.
        /// Trả về true nếu bắt được ít nhất 1 người (state dùng để biết có ai bị dính không).
        /// </summary>
        public bool RootPlayersInRadius(Vector2 center, float radius, float duration)
        {
            if (!HasStateAuthority) return false;

            bool caught = false;
            foreach (var pr in Runner.ActivePlayers)
            {
                var pObj = Runner.GetPlayerObject(pr);
                if (pObj == null) continue;
                var pc = pObj.GetComponent<PlayerController>();
                if (pc == null || pc.IsDead) continue;
                if (Vector2.Distance(pObj.transform.position, center) > radius) continue;

                var fx = pObj.GetComponent<Attrition.Gameplay.Player.PlayerStatusEffects>();
                if (fx == null) continue;   // prefab player chưa gắn component → bỏ qua, không crash
                fx.ApplyRoot(duration);
                caught = true;
            }
            return caught;
        }

        /// <summary>
        /// Nhả khống chế cho player quanh `center` (đất nổ xong thì thả ra). Chỉ host.
        ///
        /// Không kiểm tra khoảng cách khi nhả sẽ an toàn hơn về mặt gameplay, nhưng ở đây vẫn giới hạn
        /// bán kính (nới rộng 1.5x) để không vô tình nhả người bị skill KHÁC giam ở nơi khác trong phòng.
        /// </summary>
        public void ClearRootPlayersInRadius(Vector2 center, float radius)
        {
            if (!HasStateAuthority) return;

            foreach (var pr in Runner.ActivePlayers)
            {
                var pObj = Runner.GetPlayerObject(pr);
                if (pObj == null) continue;
                if (Vector2.Distance(pObj.transform.position, center) > radius * 1.5f) continue;

                var fx = pObj.GetComponent<Attrition.Gameplay.Player.PlayerStatusEffects>();
                if (fx != null) fx.ClearRoot();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ANIMATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Trigger animation trên mọi máy; bỏ qua an toàn nếu Animator chưa có trigger đó.</summary>
        public void PlayAnim(string triggerName)
        {
            RPC_DK_PlayAnim(triggerName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_DK_PlayAnim(string triggerName)
        {
            if (animationComp == null) return;
            var anim = animationComp.GetComponentInChildren<Animator>();
            if (anim == null) return;
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
                {
                    anim.SetTrigger(triggerName);
                    return;
                }
        }

        // ═══════════════════════════════════════════════════════════════
        // SKILL RANDOMIZER
        // ═══════════════════════════════════════════════════════════════

        private readonly System.Collections.Generic.List<int> _skillBag = new System.Collections.Generic.List<int>();

        /// <summary>Shuffle bag 4 skill — đảm bảo player gặp đủ moveset thay vì trùng lặp như random thuần.</summary>
        public void PickRandomSkill()
        {
            float dist = DistanceToPlayer();

            // Áp sát → melee, hoặc EarthBump để đẩy player ra xa (đúng mục đích của skill 3).
            if (dist >= 0 && dist <= meleeRange)
            {
                if (Random.value < 0.4f) { ChangeState(MeleeAttackState); return; }
                ChangeState(EarthBumpState);
                return;
            }

            if (_skillBag.Count == 0)
                for (int i = 0; i < 4; i++) _skillBag.Add(i);

            int idx = Random.Range(0, _skillBag.Count);
            int pick = _skillBag[idx];
            _skillBag.RemoveAt(idx);

            switch (pick)
            {
                case 0: ChangeState(EarthBarrageState); break;
                case 1: ChangeState(IrregularRockState); break;
                case 2: ChangeState(EarthBumpState); break;
                default: ChangeState(EarthWallState); break;
            }
        }
    }
}
