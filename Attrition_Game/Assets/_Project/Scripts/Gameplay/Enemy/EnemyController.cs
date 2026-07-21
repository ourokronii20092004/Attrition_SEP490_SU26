using Fusion;
using UnityEngine;
using Attrition.Gameplay.Enemy;

namespace Attrition.Controllers
{
    public class EnemyController : NetworkBehaviour, IDamageable
    {
        [Header("---- INJECT COMPONENTS ----")]
        [SerializeField] private EnemyAI aiComp;
        [SerializeField] private EnemyAnimation animationComp;
        [SerializeField] private EnemyCombat combatComp;
        [Tooltip("Gắn EliteEnemySkills nếu đây là quái tinh anh. Bỏ trống nếu quái thường.")]
        [SerializeField] private EliteEnemySkills eliteSkills;

        [Header("---- DEATH / REVIVE ----")]
        [Tooltip("Số lần HP về 0 nhưng hồi sinh sau reviveDelaySeconds (0 = chết hẳn ngay như Axe_Demon).")]
        [SerializeField] private int extraLivesAfterHpZero;
        [SerializeField] private float reviveDelaySeconds = 2.5f;
        [Tooltip("Tên clip animation Chết. Có → thời gian despawn KHỚP độ dài clip. Trống → dùng despawnFallback.")]
        [SerializeField] private string deathClipName = "";
        [Tooltip("Thời gian chờ despawn sau khi chết nếu không gán deathClipName (giây).")]
        [SerializeField] private float despawnFallback = 1.5f;

        [Header("---- LOOT (khi chết) ----")]
        [Tooltip("itemId trong ItemDatabase mà quái thưởng khi chết. NORMAL: drop ra thế giới (theo dropChance). ELITE/BOSS: thêm THẲNG vào kho mọi player, chỉ 1 lần.")]
        [SerializeField] private string[] lootItemIds = new string[0];
        [Tooltip("Tỉ lệ rơi cho quái THƯỜNG (0..1). Elite/Boss luôn cho (bỏ qua giá trị này).")]
        [Range(0f, 1f)][SerializeField] private float normalDropChance = 0.35f;
        [Tooltip("Prefab DroppedItem để rơi ra thế giới (quái THƯỜNG). Bỏ trống = không rơi.")]
        [SerializeField] private Fusion.NetworkPrefabRef droppedItemPrefab;

        private Vector3 _spawnPos;

        [Header("---- HIT / STUN ----")]
        [Tooltip("Bật nếu quái có thể bị đẩy lùi và choáng. Tắt đi đối với Boss hoặc Quái to.")]
        public bool canBeKnockedBack = true;
        [Tooltip("Thời gian bị choáng hoặc bật lùi khi nhận sát thương.")]
        public float stunDuration = 0.4f;

        // Đã ẩn toàn bộ các biến chạy ngầm khỏi Inspector để tránh tick nhầm
        [HideInInspector][Networked] public int Health { get; set; }
        [HideInInspector][Networked] public float CurrentPoise { get; set; }
        [HideInInspector][Networked] public NetworkBool isDeadNetworked { get; set; }
        [HideInInspector][Networked] public NetworkBool IsKnockbackActive { get; set; }
        [HideInInspector][Networked] public NetworkBool IsAwaitingRevive { get; set; }

        /// <summary>Khi true: KHÔNG tự despawn sau khi chết (để bên ngoài — vd BossGateController — điều khiển
        /// thời điểm biến mất, vd sau khi thoại kết thúc). Mặc định false (despawn theo timer như thường).</summary>
        [HideInInspector][Networked] public NetworkBool HoldDespawn { get; set; }

        [Networked] private TickTimer knockbackTimer { get; set; }
        [Networked] private TickTimer poiseRecoveryTimer { get; set; }
        [Networked] private TickTimer reviveTimer { get; set; }
        [Networked] private TickTimer despawnTimer { get; set; }
        [Networked] private int RevivesRemaining { get; set; }

        [HideInInspector] public int maxHealth = 1;
        [Tooltip("Nguồn chỉ số (point-based).")]
        [SerializeField] private EnemyStats statsComp;
        private Rigidbody2D rb;
        private bool _localDeathHandled;
        private bool _localDownedHandled;

        public bool IsDead => isDeadNetworked || IsAwaitingRevive;
        public int CurrentHealth => Health;

        public override void Spawned()
        {
            // Bắt buộc set layer về Enemy để nhận được đòn đánh từ Player
            gameObject.layer = LayerMask.NameToLayer("Enemy");

            if (statsComp == null) statsComp = GetComponent<EnemyStats>();

            // KHÔNG đọc statsComp.MaxHP ở đây vì nó là [Networked] và
            // EnemyStats.Spawned() có thể chưa chạy → giá trị = 0.
            // EnemyStats.Spawned() sẽ gọi lại gán maxHealth + Health sau khi build xong.

            if (HasStateAuthority)
            {
                // Chỉ set Health tạm nếu EnemyStats đã chạy trước (MaxHP > 0).
                // Nếu chưa (MaxHP == 0) thì EnemyStats.Spawned() sẽ gán sau.
                if (statsComp != null && statsComp.MaxHP > 0)
                {
                    maxHealth = statsComp.MaxHP;
                    Health = maxHealth;
                }
                // Nếu không có statsComp → dùng maxHealth mặc định (prefab cũ)
                else if (statsComp == null)
                {
                    Health = maxHealth;
                }
                // else: statsComp có nhưng MaxHP=0 → KHÔNG gán Health,
                //       chờ EnemyStats.Spawned() gán đúng giá trị.

                RevivesRemaining = extraLivesAfterHpZero;
                CurrentPoise = GetMaxPoise();
                _spawnPos = transform.position;
            }

            rb = GetComponent<Rigidbody2D>();
            if (aiComp == null) aiComp = GetComponent<EnemyAI>();
            if (animationComp == null) animationComp = GetComponent<EnemyAnimation>();
            if (combatComp == null) combatComp = GetComponent<EnemyCombat>();

            _localDeathHandled = false;
            _localDownedHandled = false;

            // Khởi tạo EliteEnemySkills (nếu có)
            if (eliteSkills != null)
            {
                eliteSkills.Init(
                    amount => Heal(amount),
                    aiComp != null && aiComp.isFlying,
                    combatComp != null ? combatComp.attackPoint : null,
                    combatComp != null ? combatComp.playerLayer : default
                );
            }

            // Tắt va chạm vật lý giữa Enemy và Player để Player đi xuyên qua được
            // CHỈ dùng Collider-based (không dùng IgnoreLayerCollision vì nó chặn cả trigger → ContactDamage không hoạt động)
            IgnoreAllPlayerColliders();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // SOLO pause: đóng băng hoàn toàn (Fusion bỏ qua Time.timeScale).
            if (Attrition.Persistence.GamePause.IsPaused)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (isDeadNetworked)
            {
                // HoldDespawn: chờ bên ngoài (BossGateController) ra lệnh biến mất (sau thoại). Không tự despawn.
                if (HoldDespawn) return;
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

            if (IsKnockbackActive && knockbackTimer.ExpiredOrNotRunning(Runner))
            {
                IsKnockbackActive = false;
            }

            bool usePoise = statsComp != null && statsComp.Poise > 0;
            if (usePoise && poiseRecoveryTimer.Expired(Runner))
            {
                CurrentPoise = GetMaxPoise();
                poiseRecoveryTimer = TickTimer.None;
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

        public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, Attrition.Core.DamageType type = Attrition.Core.DamageType.Physical)
        {
            if (isDeadNetworked || IsAwaitingRevive) return;
            RPC_TakeDamage(damage, knockbackDir, knockbackForce, (int)type);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, int type)
        {
            if (isDeadNetworked || IsAwaitingRevive) return;

            // damage = chỉ số tấn công GỐC; quái tự áp DEF (Physical) hoặc RES (Magic).
            int def = statsComp != null ? statsComp.DEF : 0;
            int res = statsComp != null ? statsComp.RES : 0;
            int taken = Attrition.Core.DamageCalculator.Compute((Attrition.Core.DamageType)type, damage, def, res);
            Health -= taken;
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
                bool shouldStun = false;
                bool usePoise = statsComp != null && statsComp.Poise > 0;

                if (usePoise)
                {
                    CurrentPoise -= damage; // Poise trừ trực tiếp theo lượng damage thô nhận vào
                    float recoveryTime = statsComp != null ? statsComp.PoiseRecoveryTime : 3f;
                    poiseRecoveryTimer = TickTimer.CreateFromSeconds(Runner, recoveryTime);

                    if (CurrentPoise <= 0)
                    {
                        shouldStun = true;
                        CurrentPoise = GetMaxPoise(); // Reset poise ngay lập tức sau khi bị vỡ
                    }
                }
                else if (canBeKnockedBack)
                {
                    shouldStun = true;
                }

                // Chỉ áp dụng Knockback và ngắt đòn đánh (Stun) nếu thỏa điều kiện Poise hoặc canBeKnockedBack
                if (shouldStun)
                {
                    // Ngắt heal, skill, summon nếu đang thực hiện (Elite)
                    if (eliteSkills != null)
                    {
                        eliteSkills.InterruptHealing();
                        eliteSkills.InterruptSkill();
                        eliteSkills.InterruptSummon();
                    }

                    IsKnockbackActive = true;
                    knockbackTimer = TickTimer.CreateFromSeconds(Runner, stunDuration);
                    combatComp.CancelAllActions(); // Ngắt TOÀN BỘ hành động (attack, dash, leap, freeze anim)

                    // SỬA: Nếu quái không có trọng lực (quái bay), loại bỏ lực đẩy thẳng đứng để không bị bay tuốt lên trời
                    if (rb != null && rb.gravityScale == 0)
                    {
                        knockbackDir.y = 0;
                        knockbackDir = knockbackDir.normalized;
                    }

                    rb.linearVelocity = knockbackDir * knockbackForce; // Bị văng đi
                }

                // Dù có bị knockback hay không, quái vẫn chớp sáng báo hiệu đã nhận sát thương
                RPC_PlayHitAnimation();
            }

            // Báo cho UI thế-giới (thanh máu + số sát thương nổi) trên MỌI máy.
            RPC_NotifyDamageTaken(taken);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyDamageTaken(int taken)
        {
            // Tự gắn EnemyWorldUI nếu prefab chưa có → mọi quái đều có thanh máu + số damage.
            var worldUI = GetComponent<Attrition.Gameplay.Enemy.EnemyWorldUI>();
            if (worldUI == null) worldUI = gameObject.AddComponent<Attrition.Gameplay.Enemy.EnemyWorldUI>();
            worldUI.OnDamaged(taken);
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
        {            isDeadNetworked = true;
            combatComp.IsAttacking = false;
            IsKnockbackActive = false;

            // Coop: cộng EXP NHƯ NHAU cho mọi player (không qua orb nhặt — concept "exp như nhau").
            if (statsComp != null && statsComp.ExpReward > 0)
            {
                var players = FindObjectsByType<Attrition.Gameplay.Player.PlayerProgression>(FindObjectsSortMode.None);
                foreach (var p in players)
                    if (p != null) p.GainExp(statsComp.ExpReward);
            }

            // Thông báo cho hệ thống quest biết quái vừa chết (host cộng tiến độ quest nếu khớp)
            Attrition.Gameplay.NPC.NetworkNPC.NotifyEnemyKilled(statsComp != null ? statsComp.EnemyId : "");

            GrantLoot();

            if (aiComp != null) aiComp.enabled = false;
            if (combatComp != null) combatComp.enabled = false;

            // Despawn tất cả summon khi Undead chết
            if (eliteSkills != null) eliteSkills.DespawnAllSummons();

            // Thời gian despawn khớp độ dài clip Chết (nếu gán), ngược lại dùng fallback.
            float deathDur = animationComp != null && !string.IsNullOrEmpty(deathClipName)
                ? animationComp.GetClipLength(deathClipName, despawnFallback)
                : despawnFallback;
            despawnTimer = TickTimer.CreateFromSeconds(Runner, deathDur);
        }

        /// <summary>Host ra lệnh boss biến mất NGAY (dùng khi HoldDespawn=true và chuỗi thoại đã xong).</summary>
        public void ForceDespawnNow()
        {
            if (!HasStateAuthority) return;
            if (Object != null && Object.IsValid) Runner.Despawn(Object);
        }

        /// <summary>
        /// Loot hiệu lực: ưu tiên cấu hình admin trên WEB (qua EnemyStatProvider theo enemyId);
        /// rỗng/offline → fallback lootItemIds + normalDropChance trong SO. Trả list rule đã chuẩn hoá.
        /// </summary>
        private System.Collections.Generic.List<Attrition.Systems.LootRule> ResolveLootRules(string enemyId)
        {
            var prov = Attrition.Persistence.EnemyStatProvider.Instance;
            var ov = prov != null ? prov.GetOverride(enemyId) : null;
            if (ov != null && ov.loot != null && ov.loot.Count > 0)
                return ov.loot;

            // Fallback: SO local (solo/offline hoặc web chưa cấu hình loot cho quái này).
            if (lootItemIds == null || lootItemIds.Length == 0) return null;
            var list = new System.Collections.Generic.List<Attrition.Systems.LootRule>(lootItemIds.Length);
            foreach (var id in lootItemIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                list.Add(new Attrition.Systems.LootRule
                {
                    itemId = id, dropChance = normalDropChance, minQty = 1, maxQty = 1
                });
            }
            return list.Count > 0 ? list : null;
        }

        /// <summary>
        /// Thưởng vật phẩm khi chết (host).
        ///  - NORMAL: rơi 1 item ra thế giới theo dropChance của rule (player tự nhặt).
        ///  - ELITE/BOSS: thêm THẲNG vào kho mọi player (instanced BR-33), CHỈ 1 lần — respawn sau rest không cho nữa.
        /// </summary>
        private void GrantLoot()
        {
            if (!HasStateAuthority) return;
            var db = Attrition.Data.ItemDatabaseSO.Instance;
            if (db == null) return;

            var tier = statsComp != null ? statsComp.Tier : Attrition.Data.EnemyTier.Normal;
            string enemyId = statsComp != null ? statsComp.EnemyId : name;

            var rules = ResolveLootRules(enemyId);
            if (rules == null || rules.Count == 0) return;

            if (tier == Attrition.Data.EnemyTier.Normal)
            {
                // Quái thường: roll từng rule, rơi item ra thế giới theo dropChance riêng của rule.
                // Prefab DroppedItem: ưu tiên field riêng trên enemy (nếu ai đó gán), nếu trống thì dùng
                // prefab CHUNG từ NetworkSpawner (gán 1 lần) → không cần gán từng enemy. DroppedItem tự
                // đổi icon theo item nên 1 prefab dùng cho mọi vật phẩm.
                var dropPrefab = droppedItemPrefab.IsValid ? droppedItemPrefab
                                                           : NetworkSpawner.SharedDroppedItemPrefab;
                if (!dropPrefab.IsValid) return;
                foreach (var rule in rules)
                {
                    if (Random.value > rule.dropChance) continue;
                    int idx = db.GetIndex(rule.itemId);
                    if (idx < 0) continue;
                    int qty = Random.Range(rule.minQty, rule.maxQty + 1);
                    if (qty <= 0) continue;

                    Vector3 pos = transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0.5f, 0f);
                    int idxCopy = idx, qtyCopy = qty;
                    Runner.Spawn(dropPrefab, pos, Quaternion.identity, null, (r, obj) =>
                    {
                        var d = obj.GetComponent<Attrition.Gameplay.World.DroppedItem>();
                        if (d != null)
                        {
                            d.ItemIndex = idxCopy;
                            d.Amount = qtyCopy;
                            d.InitDrop(Fusion.PlayerRef.None, r.Tick);
                        }
                    });
                }
            }
            else
            {
                // Elite/Boss: chỉ thưởng 1 lần/chỗ. Respawn sau rest → không cho lại. Roll theo dropChance;
                // rule dropChance>=1 (hoặc admin để 1.0) = chắc chắn cho.
                if (EnemyLootTracker.AlreadyLooted(enemyId, _spawnPos)) return;
                EnemyLootTracker.MarkLooted(enemyId, _spawnPos);

                var players = FindObjectsByType<Attrition.Gameplay.Player.Inventory.PlayerInventory>(FindObjectsSortMode.None);
                var granted = new System.Collections.Generic.List<string>();
                foreach (var rule in rules)
                {
                    if (Random.value > rule.dropChance) continue;
                    int idx = db.GetIndex(rule.itemId);
                    if (idx < 0) continue;
                    int qty = Random.Range(rule.minQty, rule.maxQty + 1);
                    if (qty <= 0) continue;
                    foreach (var inv in players)
                        if (inv != null) inv.TryAddItem(idx, qty); // instanced cho từng player
                    granted.Add(rule.itemId);
                }

                if (granted.Count > 0) RpcNotifyEliteBossLoot(granted.ToArray());
            }
        }

        /// <summary>Thông báo tất cả client về loot Elite/Boss để hiện reward popup.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcNotifyEliteBossLoot(string[] itemIds)
        {
            if (itemIds == null) return;
            foreach (var id in itemIds)
            {
                if (!string.IsNullOrEmpty(id))
                    Attrition.Data.RewardEvents.NotifyItem(id, 1);
            }
            Attrition.Data.RewardEvents.NotifyBatchComplete();
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

        /// <summary>
        /// Hồi máu cho quái. Gọi bởi EliteEnemySkills khi heal xong.
        /// </summary>
        public void Heal(int amount)
        {
            if (!HasStateAuthority) return;
            if (isDeadNetworked || IsAwaitingRevive) return;
            Health = Mathf.Min(Health + amount, maxHealth);
        }

        private float GetMaxPoise()
        {
            if (statsComp == null) return 0f;
            // Kiểm tra số lượng người chơi để scale Poise (BR-22)
            var players = FindObjectsByType<Attrition.Gameplay.Player.PlayerProgression>(FindObjectsSortMode.None);
            return (players != null && players.Length > 1) ? statsComp.Poise * 1.5f : statsComp.Poise;
        }

        // IGNORE PLAYER COLLIDERS — Đảm bảo Player đi xuyên qua quái

        private void IgnoreAllPlayerColliders()
        {
            Collider2D[] myCols = GetComponentsInChildren<Collider2D>();

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                Collider2D playerCol = player.GetComponent<Collider2D>();
                if (playerCol == null) continue;

                foreach (var myCol in myCols)
                {
                    if (!myCol.isTrigger && !playerCol.isTrigger)
                    {
                        Physics2D.IgnoreCollision(myCol, playerCol, true);
                    }
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerController player = collision.gameObject.GetComponentInParent<PlayerController>();
            if (player == null) player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                Collider2D playerCol = player.GetComponent<Collider2D>();
                if (playerCol == null) return;

                Collider2D[] myCols = GetComponentsInChildren<Collider2D>();
                foreach (var myCol in myCols)
                {
                    if (!myCol.isTrigger)
                    {
                        Physics2D.IgnoreCollision(myCol, playerCol, true);
                    }
                }
            }
        }
    }
}