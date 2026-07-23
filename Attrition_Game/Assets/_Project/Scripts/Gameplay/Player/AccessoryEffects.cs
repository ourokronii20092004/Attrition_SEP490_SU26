using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Trung tâm xử lý HIỆU ỨNG của accessory dạng DamageEffect đang TRANG BỊ (chỉ 1 ô EquippedAccessory
    /// → tại một thời điểm chỉ 1 hiệu ứng active). Gắn lên player prefab cùng cấp PlayerStats/PlayerCombat.
    ///
    /// Phân loại theo nơi kích hoạt:
    ///  - Khi ĐÁNH TRÚNG quái (máy có InputAuthority phát hiện) → Burn/Slow (RPC lên enemy),
    ///    Lifesteal + DamageShield (RPC lên state authority của player này).
    ///  - PASSIVE trên host: HealthRegen (hồi khi HP dưới ngưỡng).
    ///  - PotionBoost: PotionSystem (host) đọc PotionHealMultiplier khi uống bình máu.
    ///  - PostSkillDamage: PlayerSkillCaster gọi ArmPostSkill() khi cast → đòn đánh KẾ TIẾP nhân damage.
    ///  - DamageShield: PlayerController.AbsorbWithShield() trừ vào Shield trước khi trừ HP.
    ///
    /// Chưa có asset/VFX riêng — chỉ logic + tham số đọc từ AccessorySO. Shield/postSkill là [Networked]
    /// để đồng bộ; effect hiện tại đọc trực tiếp từ PlayerInventory.GetEquippedAccessorySO() (đã networked).
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class AccessoryEffects : NetworkBehaviour
    {
        [Networked] public int Shield { get; set; }
        [Networked] private TickTimer ShieldCooldown { get; set; }
        [Networked] public NetworkBool PostSkillArmed { get; set; }

        private PlayerStats _stats;
        private PlayerInventory _inventory;

        public override void Spawned()
        {
            _stats = GetComponent<PlayerStats>();
            _inventory = GetComponent<PlayerInventory>();
        }

        /// <summary>AccessorySO DamageEffect đang trang bị (hoặc null). Đọc được trên mọi máy (networked slot).</summary>
        private AccessorySO Current()
        {
            if (_inventory == null) return null;
            var acc = _inventory.GetEquippedAccessorySO();
            return (acc != null && acc.kind == AccessoryKind.DamageEffect) ? acc : null;
        }

        // ─────────────────────── PASSIVE (host) ───────────────────────

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _stats == null || _stats.CurrentHP <= 0) return;

            var acc = Current();
            if (acc == null || acc.effect != DamageEffectType.HealthRegen) return;

            int max = _stats.MaxHP;
            int lowHp = Mathf.RoundToInt(max * acc.effectThreshold);
            int stopHp = Mathf.RoundToInt(max * acc.effectThresholdStop);
            if (_stats.CurrentHP >= lowHp) return; // chỉ hồi khi DƯỚI ngưỡng kích hoạt

            // effectMagnitude = HP hồi mỗi giây. Hồi tới ngưỡng dừng thì thôi.
            int perTick = Mathf.Max(1, Mathf.RoundToInt(acc.effectMagnitude * Runner.DeltaTime));
            int target = Mathf.Min(stopHp, max);
            if (_stats.CurrentHP < target)
                _stats.CurrentHP = Mathf.Min(target, _stats.CurrentHP + perTick);
        }

        // ─────────────────────── ĐÁNH TRÚNG QUÁI ───────────────────────

        /// <summary>Gọi khi player này ĐÁNH TRÚNG 1 quái (máy có InputAuthority). dmgDealt = damage đã gây.
        /// Kích Burn/Slow lên quái + Lifesteal/Shield cho bản thân qua RPC lên host.</summary>
        public void OnDealtDamageToEnemy(Attrition.Controllers.EnemyController enemy, int dmgDealt)
        {
            var acc = Current();
            if (acc == null) return;

            switch (acc.effect)
            {
                case DamageEffectType.Burn:
                    if (enemy != null)
                        enemy.RpcApplyBurn(Mathf.RoundToInt(acc.effectMagnitude), acc.effectDuration);
                    break;
                case DamageEffectType.Slow:
                    if (enemy != null)
                        enemy.RpcApplySlow(Mathf.Clamp01(acc.effectMagnitude), acc.effectDuration);
                    break;
                case DamageEffectType.Lifesteal:
                    RpcLifesteal(Mathf.Max(1, Mathf.RoundToInt(dmgDealt * acc.effectMagnitude)));
                    break;
                case DamageEffectType.DamageShield:
                    RpcGrantShield(Mathf.Max(1, Mathf.RoundToInt(acc.effectMagnitude)), acc.effectDuration, acc.effectCooldown);
                    break;
            }

            // PostSkillDamage: đòn đánh kế sau skill đã được nhân damage ở PlayerCombat → tiêu cờ.
            if (PostSkillArmed) RpcConsumePostSkill();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcLifesteal(int amount)
        {
            if (_stats != null) _stats.RestoreHP(amount);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcGrantShield(int amount, float duration, float cooldown)
        {
            if (!ShieldCooldown.ExpiredOrNotRunning(Runner)) return; // còn cooldown → không tạo khiên mới
            Shield = amount;
            ShieldCooldown = TickTimer.CreateFromSeconds(Runner, cooldown);
            StartCoroutine(ExpireShield(duration));
        }

        private System.Collections.IEnumerator ExpireShield(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (HasStateAuthority) Shield = 0;
        }

        // ─────────────────────── SHIELD ABSORB (host, trong TakeDamage) ───────────────────────

        /// <summary>Trừ sát thương vào lá chắn trước. Trả về damage CÒN LẠI sau khi khiên hấp thụ. Chỉ host.</summary>
        public int AbsorbWithShield(int incoming)
        {
            if (!HasStateAuthority || Shield <= 0 || incoming <= 0) return incoming;
            int absorbed = Mathf.Min(Shield, incoming);
            Shield -= absorbed;
            return incoming - absorbed;
        }

        // ─────────────────────── POTION BOOST ───────────────────────

        /// <summary>Hệ số nhân lượng hồi khi uống bình máu (1 = không đổi). PotionSystem đọc.</summary>
        public float PotionHealMultiplier
        {
            get
            {
                var acc = Current();
                return (acc != null && acc.effect == DamageEffectType.PotionBoost)
                    ? 1f + Mathf.Max(0f, acc.effectMagnitude) : 1f;
            }
        }

        // ─────────────────────── POST-SKILL DAMAGE ───────────────────────

        /// <summary>PlayerSkillCaster gọi (host) khi vừa cast skill → vũ trang cho đòn đánh kế tiếp.</summary>
        public void ArmPostSkill()
        {
            if (!HasStateAuthority) return;
            var acc = Current();
            if (acc != null && acc.effect == DamageEffectType.PostSkillDamage) PostSkillArmed = true;
        }

        /// <summary>Nhân damage đòn đánh kế tiếp khi PostSkillDamage đang vũ trang (1× nếu không). PlayerCombat gọi.</summary>
        public int ApplyAttackDamageMultiplier(int damage)
        {
            if (!PostSkillArmed) return damage;
            var acc = Current();
            if (acc == null || acc.effect != DamageEffectType.PostSkillDamage) return damage;
            return Mathf.RoundToInt(damage * Mathf.Max(1f, acc.effectMagnitude));
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcConsumePostSkill() => PostSkillArmed = false;
    }
}
