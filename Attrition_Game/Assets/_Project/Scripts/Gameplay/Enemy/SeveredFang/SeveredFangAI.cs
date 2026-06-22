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
        // ═══════════════════════════════════════════════════════════════
        // BOSS-SPECIFIC INSPECTOR FIELDS
        // ═══════════════════════════════════════════════════════════════

        [Header("═══ SEVERED FANG — BOSS SETTINGS ═══")]

        [Header("---- PREFABS ----")]
        [Tooltip("Prefab vụ nổ lửa (để lại phía sau khi dash).")]
        [SerializeField] private NetworkPrefabRef fireExplosionPrefab;
        [Tooltip("Prefab đạn lửa (ném ra từ tay).")]
        [SerializeField] private NetworkPrefabRef fireBoltPrefab;

        [Header("---- SKILL 1: DASH EXPLOSION ----")]
        [Tooltip("Tốc độ lướt (units/sec).")]
        public float dashSpeed = 18f;
        [Tooltip("Thời gian lướt (giây).")]
        public float dashDuration = 0.45f;
        [Tooltip("Số vụ nổ FireExplosion để lại phía sau khi lướt.")]
        public int dashExplosionCount = 4;
        [Tooltip("Khoảng cách (giây) giữa mỗi vụ nổ trên đường lướt.")]
        public float dashExplosionInterval = 0.1f;
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

        // ═══════════════════════════════════════════════════════════════
        // NETWORKED STATE
        // ═══════════════════════════════════════════════════════════════

        [Networked] public TickTimer BossTimer { get; set; }
        [Networked] public TickTimer SkillCooldownTimer { get; set; }
        [Networked] public int BossPhaseIndex { get; set; }

        // ═══════════════════════════════════════════════════════════════
        // STATE MACHINE
        // ═══════════════════════════════════════════════════════════════

        private SeveredFangState _currentState;

        // Singleton state instances — không tạo mới mỗi lần chuyển state.
        public static readonly SF_IdleState IdleState = new SF_IdleState();
        public static readonly SF_ChaseState ChaseState = new SF_ChaseState();
        public static readonly SF_DashExplosionState DashExplosionState = new SF_DashExplosionState();
        public static readonly SF_SheatheFireballState SheatheFireballState = new SF_SheatheFireballState();
        public static readonly SF_ShortDashFireboltState ShortDashFireboltState = new SF_ShortDashFireboltState();
        public static readonly SF_RecoveryState RecoveryState = new SF_RecoveryState();
        public static readonly SF_MeleeAttackState MeleeAttackState = new SF_MeleeAttackState();

        // ═══════════════════════════════════════════════════════════════
        // LOCAL RUNTIME
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Đếm số vụ nổ đã spawn trong dash hiện tại.</summary>
        [HideInInspector] public int DashExplosionSpawned;
        /// <summary>Bộ đếm thời gian cục bộ cho các state (không sync).</summary>
        [HideInInspector] public float StateLocalTimer;
        /// <summary>Hướng dash đã chốt.</summary>
        [HideInInspector] public float DashDirectionX;

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC ACCESSORS
        // ═══════════════════════════════════════════════════════════════

        public NetworkPrefabRef FireExplosionPrefab => fireExplosionPrefab;
        public NetworkPrefabRef FireBoltPrefab => fireBoltPrefab;
        public Rigidbody2D Rb => rb;
        public EnemyAnimation AnimComp => animationComp;
        public EnemyCombat CombatComp => combatComp;
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
            {
                SkillCooldownTimer = TickTimer.CreateFromSeconds(Runner, 1.5f); // Đợi 1.5s ban đầu
            }

            // Bắt đầu ở Idle
            ChangeState(IdleState);
        }

        // ═══════════════════════════════════════════════════════════════
        // AI LOGIC — Ghi đè để dùng State Machine
        // ═══════════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════════
        // STATE TRANSITIONS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Chuyển từ state hiện tại sang state mới (Exit → Enter).
        /// </summary>
        public void ChangeState(SeveredFangState newState)
        {
            _currentState?.Exit(this);
            _currentState = newState;
            _currentState?.Enter(this);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPER — Player detection (delegate lên base class)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Tìm player gần nhất. Kết quả lưu vào PlayerTarget.</summary>
        public void DetectPlayer()
        {
            FindPlayer();
        }

        /// <summary>Cập nhật hướng nhìn về phía player.</summary>
        public void FaceTowardsPlayer()
        {
            if (playerTarget == null) return;
            float xDiff = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > facingDeadZone)
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

        // ═══════════════════════════════════════════════════════════════
        // HELPER — Spawn hiệu ứng / đạn
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Spawn FireExplosion tại vị trí chỉ định.</summary>
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

        // ═══════════════════════════════════════════════════════════════
        // HELPER — Animation triggers (RPC broadcast)
        // ═══════════════════════════════════════════════════════════════

        public void PlayAttackAnim()
        {
            RPC_SF_PlayAnim("Attack");
        }

        public void PlaySheatheAnim()
        {
            RPC_SF_PlayAnim("Sheathe");
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

        // ═══════════════════════════════════════════════════════════════
        // HELPER — Movement
        // ═══════════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════════
        // SKILL RANDOMIZER — Chọn skill ngẫu nhiên kiểu Hollow Knight
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Chọn ngẫu nhiên 1 trong 3 skill (hoặc melee nếu gần),
        /// rồi chuyển sang state tương ứng.
        /// </summary>
        public void PickRandomSkill()
        {
            float dist = DistanceToPlayer();

            // Nếu player rất gần → ưu tiên melee hoặc short dash
            if (dist >= 0 && dist <= meleeRange)
            {
                // 40% melee, 30% short dash + firebolt, 30% dash explosion
                float roll = Random.value;
                if (roll < 0.4f)
                {
                    ChangeState(MeleeAttackState);
                }
                else if (roll < 0.7f)
                {
                    ChangeState(ShortDashFireboltState);
                }
                else
                {
                    ChangeState(DashExplosionState);
                }
            }
            else
            {
                // Player xa → random 3 skill ngang nhau
                float roll = Random.value;
                if (roll < 0.33f)
                {
                    ChangeState(DashExplosionState);
                }
                else if (roll < 0.66f)
                {
                    ChangeState(SheatheFireballState);
                }
                else
                {
                    ChangeState(ShortDashFireboltState);
                }
            }
        }
    }
}
