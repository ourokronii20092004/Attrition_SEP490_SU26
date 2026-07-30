using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.ArchDemon.States
{
    /// <summary>
    /// IDLE: đứng chờ hết cooldown rồi PickRandomSkill. ArchDemon di chuyển ÍT NHẤT trong 5 boss →
    /// ngưỡng chase cao nhất (0.99 viewRadius): gần như luôn đứng tại chỗ, chỉ dồn skill.
    /// </summary>
    public class AD_IdleState : ArchDemonBossState
    {
        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Recovery;
            ai.StopMovement();
        }

        public override void Update(ArchDemonBossAI ai)
        {
            ai.StopMovement();
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();

            if (ai.PlayerTarget == null) return;
            if (!ai.SkillCooldownTimer.ExpiredOrNotRunning(ai.Runner)) return;

            if (ai.DistanceToPlayer() > ai.viewRadius * 0.99f) ai.ChangeState(ArchDemonBossAI.ChaseState);
            else ai.PickRandomSkill();
        }
    }

    /// <summary>CHASE: chỉ khi player gần thoát khỏi tầm nhìn — vào lại tầm là tung skill ngay.</summary>
    public class AD_ChaseState : ArchDemonBossState
    {
        public override void Enter(ArchDemonBossAI ai) => ai.CurrentState = EnemyState.Chase;

        public override void Update(ArchDemonBossAI ai)
        {
            ai.DetectPlayer();
            if (ai.PlayerTarget == null) { ai.ChangeState(ArchDemonBossAI.IdleState); return; }

            ai.FaceTowardsPlayer();

            if (ai.DistanceToPlayer() <= ai.viewRadius * 0.9f)
            {
                ai.StopMovement();
                ai.PickRandomSkill();
                return;
            }

            float chase = ai.StatsComp != null ? ai.StatsComp.ChaseSpeed : 4f;
            ai.MoveTowardsPlayer(chase);
        }

        public override void Exit(ArchDemonBossAI ai) => ai.StopMovement();
    }

    /// <summary>RECOVERY: nghỉ recoveryTime giây sau mỗi skill rồi về Idle.</summary>
    public class AD_RecoveryState : ArchDemonBossState
    {
        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Recovery;
            ai.StopMovement();
            ai.StateLocalTimer = 0f;
        }

        public override void Update(ArchDemonBossAI ai)
        {
            ai.StopMovement();
            ai.StateLocalTimer += ai.Runner.DeltaTime;
            if (ai.StateLocalTimer >= ai.recoveryTime)
            {
                if (ai.HasStateAuthority)
                    ai.SkillCooldownTimer = TickTimer.CreateFromSeconds(ai.Runner, 0.1f);
                ai.ChangeState(ArchDemonBossAI.IdleState);
            }
        }
    }

    /// <summary>MELEE: đòn cận chiến cơ bản. Quét hộp trước mặt, damage 1 lần.</summary>
    public class AD_MeleeAttackState : ArchDemonBossState
    {
        private bool _hit;
        private readonly Collider2D[] _results = new Collider2D[8];
        private readonly System.Collections.Generic.HashSet<IDamageable> _done =
            new System.Collections.Generic.HashSet<IDamageable>();

        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            ai.StateLocalTimer = 0f;
            _hit = false;
            _done.Clear();

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();
            ai.PlayAttackAnimNoOrb();   // cắt clip trước frame 8 — melee không kèm cầu bóng tối
        }

        public override void Update(ArchDemonBossAI ai)
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
                ai.ChangeState(ArchDemonBossAI.RecoveryState);
        }

        public override void Exit(ArchDemonBossAI ai) => ai.StopMovement();
    }
}
