using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid.States
{
    /// <summary>
    /// IDLE: khoảng NGHỈ giữa 2 skill — boss ĐI LẠI để giữ khoảng cách preferredDistance với player (quá
    /// gần thì lùi, quá xa thì tiến), hết cooldown mới PickRandomSkill. Trước đây state này StopMovement mỗi
    /// frame nên boss đứng chôn chân và bắn liên tục.
    /// </summary>
    public class D_IdleState : DruidBossState
    {
        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Chase; // cho phép animation đi lại trong lúc nghỉ
        }

        public override void Update(DruidBossAI ai)
        {
            ai.DetectPlayer();
            ai.FaceTowardsPlayer();

            if (ai.PlayerTarget == null) { ai.StopMovement(); return; } // không có mục tiêu → chờ

            if (!ai.SkillCooldownTimer.ExpiredOrNotRunning(ai.Runner))
            {
                // Nghỉ: đi lại giữ khoảng cách thay vì đứng im.
                Reposition(ai);
                return;
            }
            ai.StopMovement();

            // Mất dấu quá xa → chase lại gần; ngược lại tung skill.
            float dist = ai.DistanceToPlayer();
            if (dist > ai.viewRadius * 0.9f) ai.ChangeState(DruidBossAI.ChaseState);
            else ai.PickRandomSkill();
        }

        /// <summary>Giữ khoảng cách preferredDistance: gần quá thì lùi, xa quá thì tiến, vừa thì đứng.</summary>
        private static void Reposition(DruidBossAI ai)
        {
            float dist = ai.DistanceToPlayer();
            float speed = ai.StatsComp != null ? ai.StatsComp.PatrolSpeed : 3f;

            // Chỉ TIẾN lại gần khi quá xa, KHÔNG lùi (lùi làm boss trông như bị đẩy — "đi lùi").
            if (dist > ai.preferredDistance + 1f) ai.MoveTowardsPlayer(speed);
            else ai.StopMovement();
        }
    }

    /// <summary>CHASE: đi bộ lại gần player tới khi vào tầm rồi PickRandomSkill.</summary>
    public class D_ChaseState : DruidBossState
    {
        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Chase;
        }

        public override void Update(DruidBossAI ai)
        {
            ai.DetectPlayer();
            if (ai.PlayerTarget == null) { ai.ChangeState(DruidBossAI.IdleState); return; }

            ai.FaceTowardsPlayer();
            float dist = ai.DistanceToPlayer();

            // Vào tầm hợp lý (khoảng nửa viewRadius) → dừng và tung skill.
            if (dist <= ai.viewRadius * 0.6f)
            {
                ai.StopMovement();
                ai.PickRandomSkill();
                return;
            }

            float chase = ai.StatsComp != null ? ai.StatsComp.ChaseSpeed : 4f;
            ai.MoveTowardsPlayer(chase);
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }

    /// <summary>RECOVERY: đứng nghỉ recoveryTime giây sau mỗi skill rồi về Idle (đặt cooldown skill kế).</summary>
    public class D_RecoveryState : DruidBossState
    {
        public override void Enter(DruidBossAI ai)
        {
            ai.CurrentState = EnemyState.Recovery;
            ai.StopMovement();
            ai.StateLocalTimer = 0f;
        }

        public override void Update(DruidBossAI ai)
        {
            ai.StopMovement();
            ai.StateLocalTimer += ai.Runner.DeltaTime;
            if (ai.StateLocalTimer >= ai.recoveryTime)
            {
                // restTime (không phải 0.1s) = khoảng NGHỈ boss đi lại trước skill kế.
                if (ai.HasStateAuthority)
                    ai.SkillCooldownTimer = TickTimer.CreateFromSeconds(ai.Runner, ai.restTime);
                ai.ChangeState(DruidBossAI.IdleState);
            }
        }
    }

    /// <summary>MELEE: đòn cận chiến cơ bản (animation "Attack" có sẵn). Damage quét hộp trước mặt 1 lần.</summary>
    public class D_MeleeAttackState : DruidBossState
    {
        private bool _hit;
        private readonly Collider2D[] _results = new Collider2D[8];
        private readonly System.Collections.Generic.HashSet<IDamageable> _done = new System.Collections.Generic.HashSet<IDamageable>();

        public override void Enter(DruidBossAI ai)
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

        public override void Update(DruidBossAI ai)
        {
            ai.StopMovement();
            ai.StateLocalTimer += ai.Runner.DeltaTime;

            // Gây damage 1 lần ở giữa animation (khớp frame vung gậy).
            if (!_hit && ai.StateLocalTimer >= ai.meleeDuration * 0.4f && ai.HasStateAuthority)
            {
                _hit = true;
                Vector2 center = (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * ai.meleeRange * 0.5f, 0f);
                Vector2 size = new Vector2(ai.meleeRange, 2.5f);
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Player"), useTriggers = false };
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
                ai.ChangeState(DruidBossAI.RecoveryState);
        }

        public override void Exit(DruidBossAI ai) => ai.StopMovement();
    }
}
