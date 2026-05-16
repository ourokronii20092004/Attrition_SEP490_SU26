using Fusion;
using UnityEngine;

namespace Attrition.Controllers
{
    /// <summary>
    /// Host chung cho quái: máu, knockback, gọi AI. Có thể cấu hình số lần chết.
    /// </summary>
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

            // Xử lý Despawn an toàn trên Host
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
                knockbackTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);

                // Host TỰ xử lý vật lý để NetworkRigidbody2D đồng bộ, tránh lỗi Client giật lag
                rb.linearVelocity = Vector2.zero;
                rb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);

                // Chỉ gọi RPC để kích hoạt Animation cho mọi người chơi thấy
                RPC_PlayHitAnimation();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayHitAnimation()
        {
            if (!combatComp.IsAttacking) animationComp.PlayHit();
        }

        private void BeginDownedPhase()
        {
            IsAwaitingRevive = true;
            combatComp.IsAttacking = false;
            IsKnockbackActive = false;
            reviveTimer = TickTimer.CreateFromSeconds(Runner, reviveDelaySeconds);

            // KHÓA HOÀN TOÀN AI VÀ COMBAT: Ép quái phải nằm im, không được phép gọi lại animation
            if (aiComp != null) aiComp.enabled = false;
            if (combatComp != null) combatComp.enabled = false;

            transform.position = new Vector2(transform.position.x, transform.position.y - 0.7f);
        }

        private void CompleteRevive()
        {
            IsAwaitingRevive = false;
            Health = maxHealth;

            transform.position = new Vector2(transform.position.x, transform.position.y + 0.7f);

            // MỞ KHÓA LẠI: Cho phép quái hoạt động và đánh tiếp
            if (aiComp != null) aiComp.enabled = true;
            if (combatComp != null) combatComp.enabled = true;

            if (HasStateAuthority) aiComp.NotifyRevived();
        }

        private void DieFinal()
        {
            isDeadNetworked = true;
            combatComp.IsAttacking = false;
            IsKnockbackActive = false;

            // KHÓA HOÀN TOÀN KHI CHẾT HẲN
            if (aiComp != null) aiComp.enabled = false;
            if (combatComp != null) combatComp.enabled = false;

            transform.position = new Vector2(transform.position.x, transform.position.y - 0.7f);

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