using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.DemonKin.States
{
    /// <summary>
    /// IDLE: đứng chờ hết cooldown rồi PickRandomSkill. DemonKin di chuyển ÍT HƠN Elf → ngưỡng chase còn
    /// cao hơn nữa (0.98 viewRadius): gần như chỉ đứng tại chỗ và dồn skill.
    /// </summary>
    public class DK_IdleState : DemonKinBossState
    {
        public override void Enter(DemonKinBossAI ai)
        {
            ai.CurrentState = EnemyState.Recovery;
            ai.StopMovement();
        }

        public override void Update(DemonKinBossAI ai)
        {
            ai.StopMovement();
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();

            if (ai.PlayerTarget == null) return;
            if (!ai.SkillCooldownTimer.ExpiredOrNotRunning(ai.Runner)) return;

            if (ai.DistanceToPlayer() > ai.viewRadius * 0.98f) ai.ChangeState(DemonKinBossAI.ChaseState);
            else ai.PickRandomSkill();
        }
    }

    /// <summary>CHASE: chỉ dùng khi player gần như thoát khỏi tầm — vào lại tầm là tung skill ngay.</summary>
    public class DK_ChaseState : DemonKinBossState
    {
        public override void Enter(DemonKinBossAI ai) => ai.CurrentState = EnemyState.Chase;

        public override void Update(DemonKinBossAI ai)
        {
            ai.DetectPlayer();
            if (ai.PlayerTarget == null) { ai.ChangeState(DemonKinBossAI.IdleState); return; }

            ai.FaceTowardsPlayer();

            if (ai.DistanceToPlayer() <= ai.viewRadius * 0.85f)
            {
                ai.StopMovement();
                ai.PickRandomSkill();
                return;
            }

            float chase = ai.StatsComp != null ? ai.StatsComp.ChaseSpeed : 4f;
            ai.MoveTowardsPlayer(chase);
        }

        public override void Exit(DemonKinBossAI ai) => ai.StopMovement();
    }

    /// <summary>RECOVERY: nghỉ recoveryTime giây sau mỗi skill rồi về Idle.</summary>
    public class DK_RecoveryState : DemonKinBossState
    {
        public override void Enter(DemonKinBossAI ai)
        {
            ai.CurrentState = EnemyState.Recovery;
            ai.StopMovement();
            ai.StateLocalTimer = 0f;
        }

        public override void Update(DemonKinBossAI ai)
        {
            ai.StopMovement();
            ai.StateLocalTimer += ai.Runner.DeltaTime;
            if (ai.StateLocalTimer >= ai.recoveryTime)
            {
                if (ai.HasStateAuthority)
                    ai.SkillCooldownTimer = TickTimer.CreateFromSeconds(ai.Runner, 0.1f);
                ai.ChangeState(DemonKinBossAI.IdleState);
            }
        }
    }

    /// <summary>MELEE: đòn cận chiến cơ bản (animation "Attack"). Quét hộp trước mặt, damage 1 lần.</summary>
    public class DK_MeleeAttackState : DemonKinBossState
    {
        private bool _hit;
        private readonly Collider2D[] _results = new Collider2D[8];
        private readonly System.Collections.Generic.HashSet<IDamageable> _done =
            new System.Collections.Generic.HashSet<IDamageable>();

        public override void Enter(DemonKinBossAI ai)
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

        public override void Update(DemonKinBossAI ai)
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

            if (ai.StateLocalTimer >= ai.meleeDuration)
                ai.ChangeState(DemonKinBossAI.RecoveryState);
        }

        public override void Exit(DemonKinBossAI ai) => ai.StopMovement();
    }
}
