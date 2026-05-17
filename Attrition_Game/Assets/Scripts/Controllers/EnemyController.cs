using Fusion;
using UnityEngine;

namespace Attrition.Controllers
{
    public class EnemyController : NetworkBehaviour, IDamageable
    {
        [Header("---- INJECT COMPONENTS ----")]
        [SerializeField] private EnemyAI aiComp;
        [SerializeField] private EnemyAnimation animationComp;
        [SerializeField] private EnemyCombat combatComp;

        [Header("---- DEATH / REVIVE ----")]
        [Tooltip("Số lần HP về 0 nhưng hồi sinh sau reviveDelaySeconds (0 = chết hẳn ngay như Axe_Demon).")]
        [SerializeField] private int extraLivesAfterHpZero;
        [SerializeField] private float reviveDelaySeconds = 2.5f;

        [Header("---- HIT / STUN ----")]
        [Tooltip("Thời gian bị choáng hoặc bật lùi khi nhận sát thương.")]
        public float stunDuration = 0.4f;

        [Header("---- STATE ----")]
        [Networked] public int Health { get; set; }
        [Networked] public NetworkBool isDeadNetworked { get; set; }
        [Networked] public NetworkBool IsKnockbackActive { get; set; }
        [Networked] public NetworkBool IsAwaitingRevive { get; set; }

        [Networked] private TickTimer knockbackTimer { get; set; }
        [Networked] private TickTimer reviveTimer { get; set; }
        [Networked] private TickTimer despawnTimer { get; set; }
        [Networked] private int RevivesRemaining { get; set; }

        public int maxHealth = 3;
        private Rigidbody2D rb;
        private bool _localDeathHandled;
        private bool _localDownedHandled;

        public bool IsDead => isDeadNetworked || IsAwaitingRevive;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                Health = maxHealth;
                RevivesRemaining = extraLivesAfterHpZero;
            }

            rb = GetComponent<Rigidbody2D>();
            if (aiComp == null) aiComp = GetComponent<EnemyAI>();
            if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
            if (combatComp == null) combatComp = GetComponent<EnemyCombat>();

            _localDeathHandled = false;
            _localDownedHandled = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (isDeadNetworked)
            {
                if (despawnTimer.Expired(Runner))
                {
                    despawnTimer = TickTimer.None;
                    Runner.Despawn(Object);
                }
                return;
            }

            if (IsAwaitingRevive)
            {
                if (reviveTimer.Expired(Runner)) CompleteRevive();
                return;
            }

            // Kiểm tra thời gian choáng (Khóa AI)
            if (IsKnockbackActive && knockbackTimer.Expired(Runner))
            {
                IsKnockbackActive = false;
            }

            aiComp.RunAILogic();
        }

        public override void Render()
        {
            if (isDeadNetworked && !_localDeathHandled)
            {
                HandleDeathVisuals();
                _localDeathHandled = true;
                return;
            }

            if (IsAwaitingRevive && !_localDownedHandled)
            {
                HandleDownedVisuals();
                _localDownedHandled = true;
            }
            else if (!IsAwaitingRevive && _localDownedHandled)
            {
                HandleReviveVisuals();
                _localDownedHandled = false;
            }
        }

        public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
        {
            if (isDeadNetworked || IsAwaitingRevive) return;
            RPC_TakeDamage(damage, knockbackDir, knockbackForce);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce)
        {
            if (isDeadNetworked || IsAwaitingRevive) return;

            Health -= damage;
            aiComp.ForceFacePlayer();

            if (Health <= 0)
            {
                if (RevivesRemaining > 0)
                {
                    RevivesRemaining--;
                    BeginDownedPhase();
                }
                else
                {
                    DieFinal();
                }
            }
            else
            {
                IsKnockbackActive = true;

                // SỬA: Đặt bộ đếm thời gian choáng bằng biến stunDuration
                knockbackTimer = TickTimer.CreateFromSeconds(Runner, stunDuration);

                combatComp.IsAttacking = false;

                rb.linearVelocity = Vector2.zero;
                // Nếu lực force = 0 thì quái chỉ đứng yên tại chỗ chịu choáng (Stun)
                // Nếu force > 0 thì bị đẩy lùi theo knockbackDir (Knockback)
                rb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);

                RPC_PlayHitAnimation();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayHitAnimation()
        {
            if (animationComp != null) animationComp.PlayHit();
        }

        private void BeginDownedPhase()
        {
            IsAwaitingRevive = true;
            combatComp.IsAttacking = false;
            IsKnockbackActive = false;
            reviveTimer = TickTimer.CreateFromSeconds(Runner, reviveDelaySeconds);

            if (aiComp != null) aiComp.enabled = false;
            if (combatComp != null) combatComp.enabled = false;
        }

        private void CompleteRevive()
        {
            IsAwaitingRevive = false;
            Health = maxHealth;

            if (aiComp != null) aiComp.enabled = true;
            if (combatComp != null) combatComp.enabled = true;

            if (HasStateAuthority) aiComp.NotifyRevived();
        }

        private void DieFinal()
        {
            isDeadNetworked = true;
            combatComp.IsAttacking = false;
            IsKnockbackActive = false;

            if (aiComp != null) aiComp.enabled = false;
            if (combatComp != null) combatComp.enabled = false;

            despawnTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
        }

        private void HandleDownedVisuals()
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            animationComp.PlayDeath();
        }

        private void HandleReviveVisuals()
        {
            rb.bodyType = RigidbodyType2D.Dynamic;

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;

            animationComp.ResetAlive();
        }

        private void HandleDeathVisuals()
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            animationComp.PlayDeath();
        }
    }
}