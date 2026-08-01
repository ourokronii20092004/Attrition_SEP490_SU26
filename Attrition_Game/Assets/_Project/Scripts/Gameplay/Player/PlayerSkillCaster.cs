using Fusion;
using UnityEngine;
using Attrition.Core;
using Attrition.Data;
using Attrition.Gameplay.Combat;
using Attrition.Gameplay.Enemy;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Skill chủ động (phím K) — KHÔNG cần animation. Mỗi SkillSO tự định nghĩa hitbox/VFX/đạn
    /// nên mỗi skill cast ra khác nhau. Cải tiến kiểu game hành động:
    ///  - Active frames: hitbox chỉ sống trong [activeStartFrac, activeEndFrac] của castTime.
    ///  - Multi-hit: tickInterval >0 → gây nhiều hit (lingering AoE) thay vì 1 phát.
    ///  - Sweet spot: trúng vùng lõi được nhân damage.
    ///  - Per-target dedup trong 1 lần tick (không đánh trùng cùng tick).
    /// Host tính damage; VFX phát trên mọi máy qua RPC.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerSkillCaster : NetworkBehaviour
    {
        [Header("---- HITBOX ----")]
        [SerializeField] private Transform castPoint;
        [SerializeField] private LayerMask targetLayers;

        [Networked] public NetworkBool IsCasting { get; set; }
        [Networked] private TickTimer _castTimer { get; set; }
        [Networked] private TickTimer _cooldown { get; set; }
        [Networked] private int _ticksDone { get; set; }
        [Networked] private NetworkBool _projectileFired { get; set; }
        private Attrition.Persistence.SkillRuntimeConfig _activeConfig;
        private SkillSO _activeSkill;

        private PlayerStats _stats;
        private PlayerInventory _inventory;
        private NetworkButtons _prevButtons;
        private readonly Collider2D[] _hits = new Collider2D[16];

        public override void Spawned()
        {
            _stats = GetComponent<PlayerStats>();
            _inventory = GetComponent<PlayerInventory>();
        }

        public void HandleSkill(NetworkInputData data, bool isFacingRight)
        {
            if (castPoint != null)
            {
                var lp = castPoint.localPosition;
                castPoint.localPosition = new Vector3(Mathf.Abs(lp.x) * (isFacingRight ? 1f : -1f), lp.y, lp.z);
            }

            if (IsCasting) { TickCast(isFacingRight); _prevButtons = data.buttons; return; }

            var pressed = data.buttons.GetPressed(_prevButtons);
            _prevButtons = data.buttons;
            if (!pressed.IsSet(MyButtons.Skill)) return;
            if (!_cooldown.ExpiredOrNotRunning(Runner)) return;

            var skill = _inventory.GetEquippedSkillSO();
            if (skill == null) return;
            var config = Attrition.Persistence.SkillRuntimeConfig.From(skill);
            if (!_stats.HasMana(config.manaCost)) return;
            if (!_stats.TryConsumeMana(config.manaCost)) return;

            _activeSkill = skill;
            _activeConfig = config;
            IsCasting = true;
            _ticksDone = 0;
            _projectileFired = false;
            _castTimer = TickTimer.CreateFromSeconds(Runner, config.castTime);
            _cooldown = TickTimer.CreateFromSeconds(Runner, config.castTime + config.cooldown);

            // Accessory PostSkillDamage: vũ trang đòn đánh thường KẾ TIẾP. Chỉ host set cờ networked.
            // (SkillBuff của acc_postskill KHÔNG bật ở đây — nó bật lúc TRANG BỊ, xem ArmBuffsOnEquip.)
            if (HasStateAuthority)
            {
                var fx = GetComponent<AccessoryEffects>();
                if (fx != null) fx.ArmPostSkill();
            }

            if (Runner.IsForward) RPC_PlayVfx((int)config.element, config.vfxLifetime);
        }

        private void TickCast(bool isFacingRight)
        {
            var skill = _activeSkill;
            var config = _activeConfig;
            float total = config != null ? config.castTime : 0.6f;
            float remain = _castTimer.RemainingTime(Runner) ?? 0f;
            float elapsedFrac = total <= 0f ? 1f : 1f - (remain / total);

            if (skill != null && elapsedFrac >= config.activeStartFrac && elapsedFrac <= config.activeEndFrac + 0.001f)
            {
                if (config.delivery == SkillDelivery.Projectile)
                {
                    if (config.projectileInterval > 0f)
                    {
                        float activeElapsed = elapsedFrac * total - config.activeStartFrac * total;
                        int wanted = Mathf.Min(Mathf.Max(1, config.projectileCount),
                            Mathf.FloorToInt(activeElapsed / config.projectileInterval) + 1);
                        while (_ticksDone < wanted)
                        {
                            FireProjectile(skill, config, isFacingRight);
                            _ticksDone++;
                        }
                    }
                    else if (!_projectileFired)
                    {
                        FireProjectiles(skill, config, isFacingRight);
                        _projectileFired = true;
                    }
                }
                else if (config.delivery == SkillDelivery.SpawnAoE)
                {
                    if (!_projectileFired)
                    {
                        SpawnAoEs(skill, config, isFacingRight);
                        _projectileFired = true;
                    }
                }
                else
                {
                    int tickCount = config.ComputeTickCount();
                    int wantTicks = Mathf.Min(tickCount,
                        Mathf.FloorToInt((elapsedFrac - config.activeStartFrac) / Mathf.Max(0.0001f,
                            (config.activeEndFrac - config.activeStartFrac)) * tickCount) + 1);
                    if (_ticksDone < wantTicks) { DealArea(config, isFacingRight); _ticksDone++; }
                }
            }

            if (_castTimer.Expired(Runner)) IsCasting = false;
        }

        private void DealArea(Attrition.Persistence.SkillRuntimeConfig skill, bool isFacingRight)
        {
            if (castPoint == null) return;
            Vector2 facing = isFacingRight ? Vector2.right : Vector2.left;
            Vector2 origin = (Vector2)transform.position + new Vector2(skill.hitboxOffset.x * (isFacingRight ? 1f : -1f), skill.hitboxOffset.y);

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = targetLayers, useTriggers = false };
            var shape = (EnemyCombat.HitboxShape)(int)skill.hitShape;
            int n = HitboxResolver.Overlap(Runner.GetPhysicsScene2D(), shape, origin, origin, facing,
                skill.range, skill.angle, skill.rectSize, filter, _hits);

            int baseRaw = skill.baseDamage + Mathf.RoundToInt(_stats.AP * skill.apScaling);

            // Accessory SkillBuff (acc_postskill): nhân sát thương mọi skill trong thời gian buff.
            var accFx = GetComponent<AccessoryEffects>();
            if (accFx != null) baseRaw = accFx.ApplySkillDamageMultiplier(baseRaw);

            for (int i = 0; i < n; i++)
            {
                var hit = _hits[i];
                if (hit == null || hit.gameObject == gameObject) continue;
                var dmg = hit.GetComponentInParent<IDamageable>();
                if (dmg == null || dmg.IsDead) continue;

                int raw = baseRaw;
                if (skill.sweetSpotRadius > 0f)
                {
                    float d = Vector2.Distance(hit.ClosestPoint(origin), origin);
                    if (d <= skill.sweetSpotRadius) raw = Mathf.RoundToInt(raw * skill.sweetSpotMultiplier);
                }
                Vector2 dir = ((Vector2)hit.transform.position - origin).normalized;
                dmg.TakeDamage(raw, new Vector2(dir.x, 0.4f).normalized, skill.knockbackForce, skill.damageType);
            }
        }

        private int SkillRawDamage(Attrition.Persistence.SkillRuntimeConfig config)
        {
            int raw = config.baseDamage + Mathf.RoundToInt(_stats.AP * config.apScaling);
            var accFx = GetComponent<AccessoryEffects>();
            return accFx != null ? accFx.ApplySkillDamageMultiplier(raw) : raw;
        }

        private void FireProjectile(SkillSO skill, Attrition.Persistence.SkillRuntimeConfig config, bool isFacingRight)
        {
            if (!skill.projectilePrefab.IsValid) return;
            Vector3 spawn = castPoint != null ? castPoint.position : transform.position;
            Vector2 dir = isFacingRight ? Vector2.right : Vector2.left;
            int raw = SkillRawDamage(config);
            Runner.Spawn(skill.projectilePrefab, spawn, Quaternion.identity, null,
                (r, obj) => ProjectileInitializer.Init(obj, dir, raw, config.projectileSpeed,
                    config.damageType, config.knockbackForce));
        }

        private void SpawnAoEs(SkillSO skill, Attrition.Persistence.SkillRuntimeConfig config, bool isFacingRight)
        {
            if (!skill.projectilePrefab.IsValid) return;
            int count = Mathf.Max(1, config.projectileCount);
            float dir = isFacingRight ? 1f : -1f;
            float spacing = Mathf.Max(0f, config.spreadAngle);
            Vector3 origin = transform.position + new Vector3(config.hitboxOffset.x * dir, config.hitboxOffset.y, 0f);
            int raw = SkillRawDamage(config);

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = origin + new Vector3(dir * spacing * i, 0f, 0f);
                Runner.Spawn(skill.projectilePrefab, pos, Quaternion.identity, null,
                    (r, obj) => ProjectileInitializer.Init(obj, Vector2.zero, raw, 0f,
                        config.damageType, config.knockbackForce));
            }
        }

        private void FireProjectiles(SkillSO skill, Attrition.Persistence.SkillRuntimeConfig config, bool isFacingRight)
        {
            if (!skill.projectilePrefab.IsValid) return;
            Vector3 spawn = castPoint != null ? castPoint.position : transform.position;
            int raw = config.baseDamage + Mathf.RoundToInt(_stats.AP * config.apScaling);

            // Accessory SkillBuff: đạn skill cũng phải được buff (xem DealArea).
            var accFx = GetComponent<AccessoryEffects>();
            if (accFx != null) raw = accFx.ApplySkillDamageMultiplier(raw);

            int count = Mathf.Max(1, config.projectileCount);
            float baseAng = isFacingRight ? 0f : 180f;
            float step = count > 1 ? config.spreadAngle / (count - 1) : 0f;
            float start = baseAng - (count > 1 ? config.spreadAngle * 0.5f : 0f);

            for (int i = 0; i < count; i++)
            {
                float a = (start + step * i) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)).normalized;
                Runner.Spawn(skill.projectilePrefab, spawn, Quaternion.identity, null,
                    (r, obj) => ProjectileInitializer.Init(obj, dir, raw, config.projectileSpeed, config.damageType, config.knockbackForce));
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayVfx(int element, float lifetime)
        {
            var skill = _inventory != null ? _inventory.GetEquippedSkillSO() : null;
            if (skill == null || skill.castVfxPrefab == null) return;
            Vector3 pos = castPoint != null ? castPoint.position : transform.position;
            var fx = Instantiate(skill.castVfxPrefab, pos, Quaternion.identity);
            if (lifetime > 0f) Destroy(fx, lifetime);
        }
    }
}
