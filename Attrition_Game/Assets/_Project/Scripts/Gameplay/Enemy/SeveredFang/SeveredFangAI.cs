using Fusion;
using UnityEngine;
using Attrition.Controllers;
using Attrition.Gameplay.Enemy;
using Attrition.Gameplay.Enemy.SeveredFang.States;

namespace Attrition.Gameplay.Enemy.SeveredFang
{
    /// <summary>
    /// AI chính của Boss SeveredFang — kế thừa EnemyAI nhưng ghi đè RunAILogic
    /// để chạy State Machine riêng thay vì switch-case khổng lồ.
    /// 
    /// Moveset lấy cảm hứng Hollow Knight / Afterimage:
    /// - Idle → đi bộ lại gần → chọn ngẫu nhiên 1 trong 3 skill
    /// - Skill 1 (DashExplosion): Lướt dài + để lại FireExplosion
    /// - Skill 2 (SheatheFireball): Rút kiếm ra + ném FireBolt
    /// - Skill 3 (ShortDashFirebolt): Lướt ngắn + ném FireBolt
    /// 
    /// Animator dùng Entry → State → Exit, trigger-based.
    /// </summary>
    public class SeveredFangAI : EnemyAI
    {

        [Header("═══ SEVERED FANG — BOSS SETTINGS ═══")]

        [Header("---- PREFABS ----")]
        [Tooltip("Prefab vụ nổ lửa (để lại phía sau khi dash).")]
        [SerializeField] private NetworkPrefabRef fireExplosionPrefab;
        [Tooltip("Prefab đạn lửa (ném ra từ tay).")]
        [SerializeField] private NetworkPrefabRef fireBoltPrefab;

        [Header("---- TARGETING (COOP) ----")]
        [Tooltip("Coop: boss bám 1 player trong khoảng thời gian này (giây) rồi mới xét lại ai gần nhất. " +
                 "Tránh đổi mục tiêu liên tục khi 2 player ra-vào tầm. Solo bỏ qua (chỉ 1 player).")]
        [SerializeField] private float targetRetargetInterval = 4f;

        private float _retargetCooldown;

        [Header("---- SKILL 1: DASH EXPLOSION ----")]
        [Tooltip("Tốc độ lướt (units/sec).")]
        public float dashSpeed = 18f;
        [Tooltip("Thời gian lướt (giây).")]
        public float dashDuration = 0.45f;
        [Tooltip("Số vụ nổ FireExplosion để lại phía sau khi lướt.")]
        public int dashExplosionCount = 4;
        [Tooltip("Khoảng cách (giây) giữa mỗi vụ nổ trên đường lướt.")]
        public float dashExplosionInterval = 0.1f;
        [Tooltip("Khoảng cách (units) giữa 2 vụ nổ liên tiếp — đủ rộng để player đứng né vào khe. 0 = dùng interval thời gian.")]
        public float dashExplosionSpacing = 3f;
        [Tooltip("Sát thương mỗi vụ nổ FireExplosion.")]
        public int dashExplosionDamage = 20;

        [Header("---- SKILL 2: SHEATHE FIREBALL ----")]
        [Tooltip("Thời gian chờ rút kiếm (charge) trước khi ném cầu lửa.")]
        public float sheatheChargeTime = 0.6f;
        [Tooltip("Sát thương FireBolt (skill 2).")]
        public int sheatheFireboltDamage = 25;
        [Tooltip("Tốc độ bay của FireBolt (skill 2). 0 = giữ tốc độ prefab.")]
        public float sheatheFireboltSpeed = 0f;

        [Header("---- SKILL 3: SHORT DASH + FIREBOLT ----")]
        [Tooltip("Tốc độ lướt ngắn (units/sec).")]
        public float shortDashSpeed = 14f;
        [Tooltip("Thời gian lướt ngắn (giây).")]
        public float shortDashDuration = 0.25f;
        [Tooltip("Sát thương FireBolt (skill 3).")]
        public int shortDashFireboltDamage = 20;
        [Tooltip("Tốc độ bay của FireBolt (skill 3). 0 = giữ tốc độ prefab.")]
        public float shortDashFireboltSpeed = 0f;

        [Header("---- SKILL 4: FIRE BREATH (vệt lửa trên đất) ----")]
        [Tooltip("Thời gian vung kiếm (charge) trước khi vệt lửa đầu tiên xuất hiện.")]
        public float fireBreathChargeTime = 0.5f;
        [Tooltip("Số vệt lửa lan trên mặt đất (5-6).")]
        public int fireBreathStreakCount = 6;
        [Tooltip("Khoảng cách (units) giữa 2 vệt lửa liên tiếp.")]
        public float fireBreathSpacing = 2.2f;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 vệt lửa — tạo cảm giác lan tới trước.")]
        public float fireBreathInterval = 0.12f;
        [Tooltip("Sát thương mỗi vệt lửa. Tái dùng FireExplosion prefab.")]
        public int fireBreathDamage = 18;

        [Header("---- SKILL 5: SPLIT FIREBALL (bắn thẳng rồi tách) ----")]
        [Tooltip("Tốc độ cầu lửa 'carrier' bay tới điểm tách. 0 = giữ tốc độ prefab.")]
        public float splitCarrierSpeed = 12f;
        [Tooltip("Sát thương cầu lửa carrier khi bay tới.")]
        public int splitCarrierDamage = 20;
        [Tooltip("Thời gian carrier bay trước khi tách (giây) — sau đó spawn các cầu lửa con.")]
        public float splitTravelTime = 0.55f;
        [Tooltip("Số cầu lửa tách ra tại điểm tách (3-4).")]
        public int splitFireballCount = 4;
        [Tooltip("Góc toả (độ) của chùm cầu lửa con.")]
        public float splitSpreadAngle = 60f;
        [Tooltip("Sát thương mỗi cầu lửa con.")]
        public int splitFireballDamage = 15;
        [Tooltip("Tốc độ cầu lửa con. 0 = giữ tốc độ prefab.")]
        public float splitFireballSpeed = 0f;

        [Header("---- SKILL 6: FIREBOLT VOLLEY (ném liên tiếp) ----")]
        [Tooltip("Số firebolt ném liên tiếp (2-3).")]
        public int volleyCount = 3;
        [Tooltip("Giãn cách thời gian (giây) giữa 2 firebolt.")]
        public float volleyInterval = 0.22f;
        [Tooltip("Thời gian vung tay (charge) trước phát đầu.")]
        public float volleyChargeTime = 0.4f;
        [Tooltip("Sát thương mỗi firebolt volley.")]
        public int volleyDamage = 16;
        [Tooltip("Tốc độ firebolt volley. 0 = giữ tốc độ prefab.")]
        public float volleyFireboltSpeed = 0f;

        [Header("---- MELEE ATTACK ----")]
        [Tooltip("Phạm vi đánh cận chiến cơ bản (trigger attack animation).")]
        public float meleeRange = 2.0f;
        [Tooltip("Sát thương đòn chém cận chiến.")]
        public int meleeDamage = 15;

        [Header("---- TIMING ----")]
        [Tooltip("Thời gian nghỉ giữa các skill (giây).")]
        public float skillCooldown = 2.5f;
        [Tooltip("Thời gian nghỉ tối thiểu sau mỗi skill xong (recovery, giây).")]
        public float recoveryTime = 0.8f;


        [Networked] public TickTimer BossTimer { get; set; }
        [Networked] public TickTimer SkillCooldownTimer { get; set; }
        [Networked] public int BossPhaseIndex { get; set; }

        /// <summary>Đã bắt đầu encounter chưa (player đã kích hoạt trigger). Networked → client cũng biết.
        /// Dùng để gate thanh máu boss (chỉ hiện khi đã vào phòng đánh).</summary>
        [Networked] public NetworkBool EncounterStarted { get; set; }


        private SeveredFangState _currentState;

        // Singleton state instances — chỉ tạo 1 lần
        public static readonly SF_IntroState IntroState = new SF_IntroState();
        public static readonly SF_TelegraphState TelegraphState = new SF_TelegraphState();
        public static readonly SF_IdleState IdleState = new SF_IdleState();
        public static readonly SF_ChaseState ChaseState = new SF_ChaseState();
        public static readonly SF_DashExplosionState DashExplosionState = new SF_DashExplosionState();
        public static readonly SF_SheatheFireballState SheatheFireballState = new SF_SheatheFireballState();
        public static readonly SF_ShortDashFireboltState ShortDashFireboltState = new SF_ShortDashFireboltState();
        public static readonly SF_RecoveryState RecoveryState = new SF_RecoveryState();
        public static readonly SF_MeleeAttackState MeleeAttackState = new SF_MeleeAttackState();
        public static readonly SF_FireBreathState FireBreathState = new SF_FireBreathState();
        public static readonly SF_SplitFireballState SplitFireballState = new SF_SplitFireballState();
        public static readonly SF_FireboltVolleyState FireboltVolleyState = new SF_FireboltVolleyState();


        /// <summary>Đếm số vụ nổ đã spawn trong dash hiện tại.</summary>
        [HideInInspector] public int DashExplosionSpawned;
        /// <summary>Bộ đếm thời gian cục bộ cho các state (không sync).</summary>
        [HideInInspector] public float StateLocalTimer;
        /// <summary>Hướng dash đã chốt.</summary>
        [HideInInspector] public float DashDirectionX;
        /// <summary>Trạng thái chuẩn bị tung chiêu (để TelegraphState biết cần chuyển sang đâu tiếp).</summary>
        [HideInInspector] public SeveredFangState NextAttackState;
        
        [Header("Intro Sequence")]
        [Tooltip("Có đợi kích hoạt không? Nếu bật, Boss sẽ đứng im và không lấy máu/vô hình cho đến khi StartIntroSequence được gọi.")]
        public bool waitForTrigger = true;
        [Tooltip("File thoại mở đầu")]
        public Attrition.Data.DialogueSO introDialogue;


        public NetworkPrefabRef FireExplosionPrefab => fireExplosionPrefab;
        public NetworkPrefabRef FireBoltPrefab => fireBoltPrefab;
        public Rigidbody2D Rb => rb;
        public EnemyAnimation AnimComp => animationComp;
        public EnemyCombat CombatComp => combatComp;
        public EnemyController Controller => controller;
        public EnemyStats StatsComp => statsComp;
        public Transform PlayerTarget => playerTarget;
        public Vector2 StartPos => startPosition;


        public override void Spawned()
        {
            base.Spawned();

            if (HasStateAuthority)
            {
                SkillCooldownTimer = TickTimer.CreateFromSeconds(Runner, 1.5f); // Đợi 1.5s ban đầu
            }

            if (waitForTrigger)
            {
                // Boss đứng im chờ trigger, không chuyển state
                // (sẽ không FixedUpdateNetwork vì _currentState == null)
            }
            else
            {
                // Không cần trigger → encounter coi như bắt đầu ngay (thanh máu hiện luôn).
                if (HasStateAuthority) EncounterStarted = true;
                ChangeState(IdleState);
            }
        }

        public void StartIntroSequence()
        {
            if (!HasStateAuthority || !waitForTrigger) return;
            waitForTrigger = false;
            EncounterStarted = true;
            ChangeState(IntroState);
        }

        // AI LOGIC — Ghi đè để dùng State Machine

        public override void RunAILogic()
        {
            // Bị knockback → force idle (hồi phục sau stun)
            if (controller != null && controller.IsKnockbackActive)
            {
                if (_currentState != RecoveryState && _currentState != IdleState)
                {
                    ChangeState(RecoveryState);
                }
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

            // Cập nhật animation speed
            animationComp?.UpdateSpeed(NetSpeed);

            // Facing direction
            animationComp?.FaceDirection(NetFacingDir);
        }


        /// <summary>
        /// Chuyển từ state hiện tại sang state mới (Exit → Enter).
        /// </summary>
        public void ChangeState(SeveredFangState newState)
        {
            _currentState?.Exit(this);
            _currentState = newState;
            _currentState?.Enter(this);
        }

        // HELPER — Player detection (delegate lên base class)

        /// <summary>Tìm player gần nhất. Kết quả lưu vào PlayerTarget.</summary>
        public void DetectPlayer()
        {
            // SOLO (hoặc chỉ 1 player) → dùng logic base (bám player duy nhất).
            if (Attrition.Persistence.GameLaunch.Mode != Attrition.Persistence.LaunchMode.Coop)
            {
                FindPlayer();
                return;
            }

            // COOP: bám mục tiêu hiện tại trong targetRetargetInterval giây, rồi mới xét lại AI GẦN NHẤT.
            // Tránh đổi mục tiêu liên tục khi 2 player ra-vào tầm.
            _retargetCooldown -= Runner.DeltaTime;

            bool currentValid = playerTarget != null
                && Vector2.Distance(transform.position, playerTarget.position) <= viewRadius * 1.05f;

            // Còn trong thời gian khoá mục tiêu VÀ mục tiêu hiện tại còn hợp lệ → giữ nguyên.
            if (currentValid && _retargetCooldown > 0f) return;

            // Hết thời gian khoá (hoặc mất mục tiêu) → chọn lại player GẦN NHẤT trong tầm.
            Transform nearest = FindNearestPlayer();
            if (nearest != null)
            {
                playerTarget = nearest;
                _retargetCooldown = targetRetargetInterval; // khoá mục tiêu mới trong N giây
            }
            else
            {
                playerTarget = null; // không còn ai trong tầm
            }
        }

        /// <summary>Quét MỌI player còn sống trong viewRadius, trả về người GẦN boss nhất (null nếu không có).</summary>
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

        /// <summary>Cập nhật hướng nhìn về phía player.</summary>
        public void FaceTowardsPlayer()
        {
            if (playerTarget == null) return;
            float xDiff = playerTarget.position.x - transform.position.x;
            // Dead zone RỘNG cho boss (thân to): khi player đứng đè/giữa boss, xDiff dao động quanh 0
            // → KHÔNG lật hướng (tránh boss xoay liên tục 2 bên). Chỉ đổi hướng khi player thật sự
            // lệch hẳn sang một bên.
            float bossDeadZone = Mathf.Max(facingDeadZone, 1.2f);
            if (Mathf.Abs(xDiff) > bossDeadZone)
            {
                NetFacingDir = xDiff > 0 ? 1f : -1f;
                AttackLockedFacingDir = NetFacingDir;
            }
        }

        /// <summary>Khoảng cách tới player (-1 nếu chưa có target).</summary>
        public float DistanceToPlayer()
        {
            if (playerTarget == null) return -1f;
            return Vector2.Distance(transform.position, playerTarget.position);
        }

        // HELPER — Spawn hiệu ứng / đạn

        /// <summary>Spawn FireExplosion tại vị trí chỉ định. Việc HẠ XUỐNG MẶT ĐẤT do EnemyAoEDamage.Spawned()
        /// tự xử lý (raycast trên Fusion physics scene) — tránh nổ lơ lửng do NetworkTransform ghi đè.</summary>
        public void SpawnFireExplosion(Vector2 position, int damage)
        {
            if (!HasStateAuthority || !fireExplosionPrefab.IsValid) return;

            Runner.Spawn(fireExplosionPrefab, position, Quaternion.identity, null, (runner, obj) =>
            {
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, Vector2.zero, damage,
                    Attrition.Gameplay.Combat.ProjectileInitializer.DefaultSpeed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        /// <summary>Spawn FireBolt bay về hướng dir.</summary>
        public void SpawnFireBolt(Vector2 spawnPos, Vector2 dir, int damage, float speed = 0f)
        {
            if (!HasStateAuthority || !fireBoltPrefab.IsValid) return;
            Runner.Spawn(fireBoltPrefab, spawnPos, Quaternion.identity, null, (runner, obj) =>
            {
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, dir, damage, speed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        /// <summary>Như SpawnFireBolt nhưng TRẢ VỀ NetworkObject để state theo dõi (vd carrier của Split
        /// Fireball cần despawn tại điểm tách). Trả null nếu không phải host / prefab chưa gán.</summary>
        public NetworkObject SpawnFireBoltTracked(Vector2 spawnPos, Vector2 dir, int damage, float speed = 0f)
        {
            if (!HasStateAuthority || !fireBoltPrefab.IsValid) return null;
            return Runner.Spawn(fireBoltPrefab, spawnPos, Quaternion.identity, null, (runner, obj) =>
            {
                Attrition.Gameplay.Combat.ProjectileInitializer.Init(
                    obj, dir, damage, speed,
                    Attrition.Core.DamageType.Magic);
            });
        }

        /// <summary>Despawn 1 NetworkObject (host). Dùng để huỷ carrier fireball tại điểm tách.</summary>
        public void DespawnObject(NetworkObject obj)
        {
            if (!HasStateAuthority || obj == null || !obj.IsValid) return;
            Runner.Despawn(obj);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPER — Animation triggers (RPC broadcast)

        public void PlayAttackAnim()
        {
            RPC_SF_PlayAnim("Attack");
        }

        public void PlaySheatheAnim()
        {
            RPC_SF_PlayAnim("Sheathe");
        }

        /// <summary>Trigger animation FireBreath (skill 4). Cần thêm trigger "FireBreath" + clip trong Animator.
        /// Chưa có clip → Animator bỏ qua trigger, skill vẫn chạy logic (spawn vệt lửa) bình thường.</summary>
        public void PlayFireBreathAnim()
        {
            RPC_SF_PlayAnim("FireBreath");
        }

        public void PlayHurtAnim()
        {
            RPC_SF_PlayAnim("Hurt");
        }

        public void PlayDeathAnim()
        {
            RPC_SF_PlayAnim("Death");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SF_PlayAnim(string triggerName)
        {
            if (animationComp == null) return;
            var anim = animationComp.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(triggerName);
        }

        // HELPER — Movement

        /// <summary>Di chuyển boss về phía player (chỉ trục X).</summary>
        public void MoveTowardsPlayer(float speed)
        {
            if (playerTarget == null) return;
            float xDiff = playerTarget.position.x - transform.position.x;
            float dirX = xDiff > 0 ? 1f : -1f;
            rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
            NetSpeed = Mathf.Abs(rb.linearVelocity.x);
        }

        /// <summary>Dừng di chuyển ngang, giữ trọng lực.</summary>
        public void StopMovement()
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            NetSpeed = 0f;
        }

        // SKILL RANDOMIZER — Chọn skill ngẫu nhiên kiểu Hollow Knight

        /// <summary>
        /// Chọn ngẫu nhiên 1 trong 3 skill (hoặc melee nếu gần),
        /// rồi chuyển sang state tương ứng.
        /// </summary>
        public void PickRandomSkill()
        {
            float dist = DistanceToPlayer();

            // Nếu player rất gần → ưu tiên áp sát/cận chiến (6 lựa chọn ngang nhau).
            if (dist >= 0 && dist <= meleeRange)
            {
                float roll = Random.value;
                if (roll < 0.30f)      ChangeState(MeleeAttackState);
                else if (roll < 0.50f) ChangeState(ShortDashFireboltState);
                else if (roll < 0.70f) ChangeState(DashExplosionState);
                else if (roll < 0.90f) ChangeState(FireBreathState);   // vệt lửa quét quanh chân — mạnh khi gần
                else                   ChangeState(FireboltVolleyState);
            }
            else
            {
                // Player xa → ưu tiên đòn tầm xa (split fireball, firebolt volley) + skill lướt tiếp cận.
                float roll = Random.value;
                if (roll < 0.20f)      ChangeState(DashExplosionState);
                else if (roll < 0.40f) ChangeState(SheatheFireballState);
                else if (roll < 0.58f) ChangeState(ShortDashFireboltState);
                else if (roll < 0.74f) ChangeState(FireBreathState);
                else if (roll < 0.88f) ChangeState(SplitFireballState);
                else                   ChangeState(FireboltVolleyState);
            }
        }
    }
}
