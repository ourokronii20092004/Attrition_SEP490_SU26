using Fusion;
using UnityEngine;
using Attrition.Controllers;
using Attrition.Gameplay.Enemy;
using Attrition.Gameplay.Enemy.Druid.States;

namespace Attrition.Gameplay.Enemy.Druid
{
    /// <summary>
    /// AI chính của Boss DRUID (Boss 2, khu vực Wood/Wind) — mirror kiến trúc
    /// <see cref="SeveredFang.SeveredFangAI"/>: kế thừa EnemyAI, ghi đè RunAILogic để chạy State Machine
    /// riêng (Enter/Update/Exit) thay vì switch-case.
    ///
    /// Moveset (5 skill):
    ///  - Basic attack (melee, animation có sẵn — trigger "Attack").
    ///  - Skill 1 WoodWild: 3 lượt, mỗi lượt 3 viên gỗ rơi dọc, có khoảng trống giữa các lượt để né.
    ///  - Skill 2 WindBreath: 1 luồng gió dài kéo hết sân theo 1 đường thẳng rồi biến mất.
    ///  - Skill 3 WindSword: bắn 3-4 lưỡi dao gió về phía player.
    ///  - Skill 4 AirBurst: tạo 5-6 điểm ngẫu nhiên quanh player (preparing) rồi bung airburst gây damage.
    ///  - Skill 5 AirExplosion: tạo chuỗi 6-7 điểm nổ hình zigzag, nổ LẦN LƯỢT theo thứ tự.
    ///
    /// Các "prefab" là NetworkPrefabRef gán ở Inspector. Damage đến từ EnemyProjectile (đạn bay) hoặc
    /// EnemyAoEDamage (nổ đứng yên) gắn trên chính prefab đó — AI chỉ Spawn + Init.
    /// </summary>
    public class DruidBossAI : EnemyAI, Attrition.Core.IBossEncounter
    {
        [Header("═══ DRUID — BOSS SETTINGS ═══")]

        [Header("---- PREFABS ----")]
        [Tooltip("Viên gỗ RƠI DỌC (Skill 1). Prefab có EnemyProjectile, hitLayer gồm Player.")]
        [SerializeField] private NetworkPrefabRef woodProjectilePrefab;
        [Tooltip("Đốt gió của luồng WindBreath (Skill 2). Prefab có EnemyAoEDamage (nổ đứng yên).")]
        [SerializeField] private NetworkPrefabRef windBeamPrefab;
        [Tooltip("Lưỡi dao gió bay về player (Skill 3). Prefab có EnemyProjectile.")]
        [SerializeField] private NetworkPrefabRef windSwordPrefab;
        [Tooltip("AirBurst pull-in/telegraph trước khi nổ. Cosmetic, damage = 0.")]
        [SerializeField] private NetworkPrefabRef airBurstPullInPrefab;
        [Tooltip("Airburst tại điểm ngắm (Skill 4). Prefab có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef airBurstPrefab;
        [Tooltip("AirExplosion startup/repair trước khi nổ. Cosmetic, damage = 0.")]
        [SerializeField] private NetworkPrefabRef airExplosionStartupPrefab;
        [Tooltip("Điểm nổ zigzag (Skill 5). Prefab có EnemyAoEDamage.")]
        [SerializeField] private NetworkPrefabRef airExplosionPrefab;

        [Header("---- TARGETING (COOP) ----")]
        [Tooltip("Coop: bám 1 player trong khoảng này (giây) rồi mới xét lại ai gần nhất. Solo bỏ qua.")]
        [SerializeField] private float targetRetargetInterval = 4f;
        private float _retargetCooldown;
        // Skill bag: mỗi khi hết, nạp lại đủ 5 chỉ số → boss tung hết 1 vòng mới lặp, không bị lặp lại
        // cùng 1 skill nhiều lần liên tiếp (lỗi "hay bị lặp lại, không tung hết kĩ năng").
        private readonly System.Collections.Generic.List<int> _skillBag =
            new System.Collections.Generic.List<int>();

        [Header("---- MELEE (basic attack) ----")]
        [Tooltip("Phạm vi kích hoạt đòn cận chiến cơ bản.")]
        public float meleeRange = 2.2f;
        [Tooltip("Sát thương đòn cận chiến.")]
        public int meleeDamage = 16;
        [Tooltip("Thời gian khoá state trong lúc chơi animation Attack (giây).")]
        public float meleeDuration = 0.7f;

        [Header("---- SKILL 1: WOOD WILD (gỗ rơi dọc) ----")]
        [Tooltip("Số lượt phóng (mỗi lượt 3 viên).")]
        public int woodWaves = 3;
        [Tooltip("Số viên mỗi lượt.")]
        public int woodPerWave = 3;
        [Tooltip("Khoảng cách ngang (units) giữa các viên trong 1 lượt.")]
        public float woodSpacing = 2f;
        [Tooltip("Độ cao (units) phía trên player để thả viên gỗ.")]
        public float woodSpawnHeight = 8f;
        [Tooltip("Khoảng trống thời gian (giây) giữa 2 lượt — đủ để player né.")]
        public float woodWaveGap = 0.9f;
        [Tooltip("Tốc độ rơi. 0 = giữ tốc độ prefab.")]
        public float woodSpeed = 12f;
        [Tooltip("Sát thương mỗi viên gỗ.")]
        public int woodDamage = 14;

        [Header("---- SKILL 2: WIND BREATH (luồng gió dài) ----")]
        [Tooltip("Thời gian hít hơi (charge) trước khi luồng gió xuất hiện.")]
        public float windBreathChargeTime = 0.6f;
        [Tooltip("Số đốt gió trải dài theo đường thẳng (độ dài luồng).")]
        public int windBreathSegments = 10;
        [Tooltip("Khoảng cách (units) giữa 2 đốt gió liên tiếp.")]
        public float windBreathSpacing = 2f;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 đốt — tạo cảm giác luồng gió lan tới.")]
        public float windBreathInterval = 0.05f;
        [Tooltip("Độ cao (units) của luồng gió so với chân boss.")]
        public float windBreathHeight = 1f;
        [Tooltip("Sát thương mỗi đốt gió.")]
        public int windBreathDamage = 18;

        [Header("---- SKILL 3: WIND SWORD (lưỡi dao gió) ----")]
        [Tooltip("Số lưỡi dao gió bắn về player (3-4).")]
        public int windSwordCount = 4;
        [Tooltip("Góc toả (độ) của chùm lưỡi dao.")]
        public float windSwordSpread = 40f;
        [Tooltip("Giãn cách thời gian (giây) giữa mỗi lưỡi — 0 = bắn cùng lúc theo quạt.")]
        public float windSwordInterval = 0.12f;
        [Tooltip("Thời gian vung tay (charge) trước phát đầu.")]
        public float windSwordChargeTime = 0.4f;
        [Tooltip("Tốc độ lưỡi dao. 0 = giữ tốc độ prefab.")]
        public float windSwordSpeed = 0f;
        [Tooltip("Sát thương mỗi lưỡi dao.")]
        public int windSwordDamage = 15;

        [Header("---- SKILL 4: AIR BURST (điểm ngẫu nhiên + bung) ----")]
        [Tooltip("Số điểm airburst ngẫu nhiên quanh player (5-6).")]
        public int airBurstCount = 6;
        [Tooltip("Bán kính rải điểm quanh player (units).")]
        public float airBurstScatter = 4f;
        [Tooltip("Thời gian Pull In báo trước trước khi mỗi AirBurst gây damage.")]
        public float airBurstPrepareTime = 0.8f;
        [Tooltip("Thời gian nghỉ sau khi AirBurst kết thúc trước Pull In kế tiếp.")]
        public float airBurstInterval = 0.55f;
        [Tooltip("Sát thương mỗi airburst.")]
        public int airBurstDamage = 20;

        [Header("---- SKILL 5: AIR EXPLOSION (zigzag lần lượt) ----")]
        [Tooltip("Số điểm nổ theo hình zigzag (6-7).")]
        public int airExplosionCount = 7;
        [Tooltip("Khoảng cách ngang (units) giữa 2 điểm zigzag liên tiếp.")]
        public float airExplosionStepX = 2f;
        [Tooltip("Biên độ dọc (units) của zigzag (điểm lẻ cao, chẵn thấp).")]
        public float airExplosionAmplitudeY = 1.5f;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 điểm nổ liên tiếp — nổ LẦN LƯỢT.")]
        public float airExplosionInterval = 0.18f;
        [Tooltip("Thời gian Explosion Startup/repair hiện trước mỗi vụ nổ.")]
        public float airExplosionStartupLead = 0.35f;
        [Tooltip("Sát thương mỗi điểm nổ.")]
        public int airExplosionDamage = 18;

        [Header("---- TIMING ----")]
        [Tooltip("Nghỉ giữa các skill (giây).")]
        public float recoveryTime = 1f;
        [Tooltip("Thời gian ĐI LẠI tự do sau khi nghỉ, trước khi tung skill kế (giây). Boss dùng khoảng này " +
                 "để giữ khoảng cách với player thay vì bắn liên tục.")]
        public float restTime = 1.6f;
        [Tooltip("Khoảng cách (units) boss muốn giữ với player trong lúc đi lại nghỉ.")]
        public float preferredDistance = 6f;
        [Tooltip("Chờ ban đầu trước skill đầu tiên (giây).")]
        public float initialDelay = 1.5f;

        [Header("---- INTRO ----")]
        [Tooltip("Đợi trigger mới bắt đầu? Bật = boss đứng im tới khi StartIntroSequence được gọi.")]
        public bool waitForTrigger = true;
        [Tooltip("File thoại mở đầu (tùy chọn).")]
        public Attrition.Data.DialogueSO introDialogue;

        // IBossEncounter: bridge NetworkBool → bool (xem ghi chú cùng chỗ trong SeveredFangAI).
        bool Attrition.Core.IBossEncounter.EncounterStarted => EncounterStarted;
        public bool IsWaitingForTrigger => waitForTrigger;

        // ═══════════════════════════════════════════════════════════════
        // NETWORKED STATE
        // ═══════════════════════════════════════════════════════════════

        [Networked] public TickTimer SkillCooldownTimer { get; set; }
        [Networked] public NetworkBool EncounterStarted { get; set; }

        // ═══════════════════════════════════════════════════════════════
        // STATE MACHINE
        // ═══════════════════════════════════════════════════════════════

        private DruidBossState _currentState;

        public static readonly D_IdleState IdleState = new D_IdleState();
        public static readonly D_ChaseState ChaseState = new D_ChaseState();
        public static readonly D_RecoveryState RecoveryState = new D_RecoveryState();
        public static readonly D_MeleeAttackState MeleeAttackState = new D_MeleeAttackState();
        public static readonly D_WoodWildState WoodWildState = new D_WoodWildState();
        public static readonly D_WindBreathState WindBreathState = new D_WindBreathState();
        public static readonly D_WindSwordState WindSwordState = new D_WindSwordState();
        public static readonly D_AirBurstState AirBurstState = new D_AirBurstState();
        public static readonly D_AirExplosionState AirExplosionState = new D_AirExplosionState();

        // Bộ đếm cục bộ dùng chung cho state (không sync — chỉ host chạy AI).
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
            if (introDialogue != null) RPC_ShowIntroDialogue();
            else ChangeState(IdleState);
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

        /// <summary>
        /// Player wipe (cả team chết) → trả boss về trạng thái CHỜ TRIGGER như lúc mới vào phòng:
        /// đứng im, không state, ẩn thanh máu (EncounterStarted=false), về đúng chỗ spawn.
        /// HP do EnemyController.ResetForEncounterRetry lo. Chỉ host.
        /// </summary>
        public void ResetEncounter()
        {
            if (!HasStateAuthority) return;

            ChangeState(null);              // dừng state machine — boss đứng im chờ trigger lại
            waitForTrigger = true;
            EncounterStarted = false;       // ẩn thanh máu tới khi player kích hoạt lại

            playerTarget = null;
            _retargetCooldown = 0f;
            StateLocalTimer = 0f;
            SkillCooldownTimer = TickTimer.CreateFromSeconds(Runner, initialDelay);

            StopMovement();
            if (rb != null) rb.position = StartPos;   // về đúng vị trí đặt trong scene
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

        public void ChangeState(DruidBossState newState)
        {
            _currentState?.Exit(this);
            _currentState = newState;
            _currentState?.Enter(this);
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER DETECTION (mirror SeveredFang)
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

        /// <summary>Lùi xa player (giữ khoảng cách trong lúc nghỉ). Không lùi quá leash/StartPos ±viewRadius.</summary>
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

        /// <summary>Spawn AoE có art mặc định nhìn sang phải và đảo hình theo hướng tấn công.</summary>
        public void SpawnAoEOriented(NetworkPrefabRef prefab, Vector2 pos, int damage, float dirX)
        {
            if (!HasStateAuthority || !prefab.IsValid) return;
            Runner.Spawn(prefab, pos, Quaternion.identity, null, (runner, obj) =>
            {
                var s = obj.transform.localScale;
                // WindBreath/AirBurst source art nhìn sang phải: bắn phải giữ X dương, bắn trái đảo X.
                obj.transform.localScale = new Vector3(dirX > 0f ? Mathf.Abs(s.x) : -Mathf.Abs(s.x), s.y, s.z);
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, Vector2.zero, BossStatDamage(Attrition.Core.DamageType.Magic),
                    Attrition.Gameplay.Combat.ProjectileInitializer.DefaultSpeed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        // Public prefab accessors cho state.
        public NetworkPrefabRef WoodProjectilePrefab => woodProjectilePrefab;
        public NetworkPrefabRef WindBeamPrefab => windBeamPrefab;
        public NetworkPrefabRef WindSwordPrefab => windSwordPrefab;
        public NetworkPrefabRef AirBurstPullInPrefab => airBurstPullInPrefab;
        public NetworkPrefabRef AirBurstPrefab => airBurstPrefab;
        public NetworkPrefabRef AirExplosionStartupPrefab => airExplosionStartupPrefab;
        public NetworkPrefabRef AirExplosionPrefab => airExplosionPrefab;

        // ═══════════════════════════════════════════════════════════════
        // ANIMATION (trigger-based, RPC broadcast)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Trigger animation. Basic attack dùng "Attack" (đã có sẵn). Các skill dùng trigger riêng
        /// nếu Animator có; thiếu trigger thì SetTrigger là no-op an toàn.</summary>
        public void PlayAnim(string triggerName)
        {
            RPC_Druid_PlayAnim(triggerName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Druid_PlayAnim(string triggerName)
        {
            if (animationComp == null) return;
            var anim = animationComp.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(triggerName);
        }

        // ═══════════════════════════════════════════════════════════════
        // SKILL RANDOMIZER
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Chọn skill kế tiếp. Gần → ưu tiên melee/airburst; xa → random 5 skill tầm xa.</summary>
        public void PickRandomSkill()
        {
            float dist = DistanceToPlayer();

            if (dist >= 0 && dist <= meleeRange)
            {
                float roll = Random.value;
                if (roll < 0.4f) ChangeState(MeleeAttackState);
                else if (roll < 0.7f) ChangeState(AirBurstState);
                else ChangeState(WindSwordState);
                return;
            }

            // Player xa → RÚT THĂM KHÔNG TRÙNG (skill bag) như Elf/DemonKin: Random.Range thuần khiến boss
            // lặp lại 1-2 skill và có skill cả trận không thấy — đúng lỗi "không tung hết kĩ năng, hay bị lặp".
            if (_skillBag.Count == 0)
                for (int i = 0; i < 5; i++) _skillBag.Add(i);

            int idx = Random.Range(0, _skillBag.Count);
            int pick = _skillBag[idx];
            _skillBag.RemoveAt(idx);

            switch (pick)
            {
                case 0: ChangeState(WoodWildState); break;
                case 1: ChangeState(WindBreathState); break;
                case 2: ChangeState(WindSwordState); break;
                case 3: ChangeState(AirBurstState); break;
                default: ChangeState(AirExplosionState); break;
            }
        }
    }
}
