using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.Elf.States
{
    /// <summary>
    /// IDLE: khoảng NGHỈ giữa 2 skill — Elf đi lại giữ khoảng cách preferredDistance (ít di chuyển nhưng
    /// KHÔNG chôn chân), hết cooldown mới PickRandomSkill. Trước đây StopMovement mỗi frame + cooldown 0.1s
    /// nên boss dồn skill liên tục không có nhịp nghỉ.
    /// </summary>
    public class E_IdleState : ElfBossState
    {
        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Chase; // cho phép animation đi lại trong lúc nghỉ
            ai.PlayAnim("Idle"); // reset Attack/Skill trigger trên mọi peer rồi về Idle/Walk
        }

        public override void Update(ElfBossAI ai)
        {
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();

            if (ai.PlayerTarget == null) { ai.StopMovement(); return; }
            if (!ai.SkillCooldownTimer.ExpiredOrNotRunning(ai.Runner))
            {
                Reposition(ai);
                return;
            }
            ai.StopMovement();

            float dist = ai.DistanceToPlayer();
            if (dist > ai.viewRadius * 0.95f) ai.ChangeState(ElfBossAI.ChaseState);
            else ai.PickRandomSkill();
        }

        /// <summary>Giữ khoảng cách preferredDistance: gần quá thì lùi, xa quá thì tiến, vừa thì đứng.</summary>
        private static void Reposition(ElfBossAI ai)
        {
            float dist = ai.DistanceToPlayer();
            float speed = ai.StatsComp != null ? ai.StatsComp.PatrolSpeed : 3f;

            // Chỉ TIẾN lại gần khi quá xa, KHÔNG lùi.
            if (dist > ai.preferredDistance + 1f) ai.MoveTowardsPlayer(speed);
            else ai.StopMovement();
        }
    }

    /// <summary>CHASE: đi lại gần cho tới khi vào tầm bắn rồi tung skill ngay.</summary>
    public class E_ChaseState : ElfBossState
    {
        public override void Enter(ElfBossAI ai) => ai.CurrentState = EnemyState.Chase;

        public override void Update(ElfBossAI ai)
        {
            ai.DetectPlayer();
            if (ai.PlayerTarget == null) { ai.ChangeState(ElfBossAI.IdleState); return; }

            ai.FaceTowardsPlayer();

            // Elf đánh xa: chỉ cần vào tầm nhìn rộng là đủ, không cần áp sát như Druid (0.6).
            if (ai.DistanceToPlayer() <= ai.viewRadius * 0.8f)
            {
                ai.StopMovement();
                ai.PickRandomSkill();
                return;
            }

            float chase = ai.StatsComp != null ? ai.StatsComp.ChaseSpeed : 4f;
            ai.MoveTowardsPlayer(chase);
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }

    /// <summary>RECOVERY: nghỉ recoveryTime giây sau mỗi skill rồi về Idle.</summary>
    public class E_RecoveryState : ElfBossState
    {
        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Recovery;
            ai.StopMovement();
            ai.PlayAnim("Idle");
            ai.StateLocalTimer = 0f;
        }

        public override void Update(ElfBossAI ai)
        {
            ai.StopMovement();
            ai.StateLocalTimer += ai.Runner.DeltaTime;
            if (ai.StateLocalTimer >= ai.recoveryTime)
            {
                // restTime (không phải 0.1s) = khoảng NGHỈ boss đi lại trước skill kế.
                if (ai.HasStateAuthority)
                    ai.SkillCooldownTimer = TickTimer.CreateFromSeconds(ai.Runner, ai.restTime);
                ai.ChangeState(ElfBossAI.IdleState);
            }
        }
    }

    /// <summary>MELEE: đòn cận chiến cơ bản (animation "Attack"). Quét hộp trước mặt, gây damage 1 lần.</summary>
    public class E_MeleeAttackState : ElfBossState
    {
        private bool _hit;
        private readonly Collider2D[] _results = new Collider2D[8];
        private readonly System.Collections.Generic.HashSet<IDamageable> _done =
            new System.Collections.Generic.HashSet<IDamageable>();

        public override void Enter(ElfBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            ai.StateLocalTimer = 0f;
            _hit = false;
            _done.Clear();

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();
            ai.PlayAnim("Attack");
        }

        public override void Update(ElfBossAI ai)
        {
            ai.StopMovement();
            ai.StateLocalTimer += ai.Runner.DeltaTime;

            if (!_hit && ai.StateLocalTimer >= ai.meleeDuration * 0.4f && ai.HasStateAuthority)
            {
                _hit = true;
                Vector2 center = (Vector2)ai.transform.position
                                 + new Vector2(ai.AttackLockedFacingDir * ai.meleeRange * 0.5f, 0f);
                Vector2 size = new Vector2(ai.meleeRange, 2.5f);
                var filter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = LayerMask.GetMask("Player"),
                    useTriggers = false
                };
                int n = ai.Runner.GetPhysicsScene2D().OverlapBox(center, size, 0f, filter, _results);
                for (int i = 0; i < n; i++)
                {
                    var dmg = _results[i] != null ? _results[i].GetComponentInParent<IDamageable>() : null;
                    if (dmg == null || dmg.IsDead || _done.Contains(dmg)) continue;
                    _done.Add(dmg);
                    Vector2 push = new Vector2(ai.AttackLockedFacingDir, 0.4f).normalized;
                    dmg.TakeDamage(ai.meleeDamage, push, 5f, Attrition.Core.DamageType.Physical);
                }
            }

            if (ai.StateLocalTimer >= Mathf.Max(ai.meleeDuration, ElfBossAI.SkillAttackWindup))
                ai.ChangeState(ElfBossAI.RecoveryState);
        }

        public override void Exit(ElfBossAI ai) => ai.StopMovement();
    }
}
