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
    ///    Lifesteal (RPC lên state authority của player này).
    ///  - PASSIVE trên host: HealthRegen (hồi khi HP dưới ngưỡng), DamageShield (nạp lại lượt chặn).
    ///  - PotionBoost: PotionSystem (host) đọc PotionHealMultiplier khi uống bình máu.
    ///  - PostSkillDamage: PlayerSkillCaster gọi ArmPostSkill() khi cast → đòn đánh KẾ TIẾP nhân damage.
    ///  - DamageShield: PlayerController.AbsorbWithShield() chặn TRỌN 1 đòn trước khi trừ HP.
    ///
    /// LƯU Ý: đây là NetworkBehaviour nên PHẢI có sẵn trên prefab. Trước đây script tồn tại nhưng
    /// KHÔNG được gắn vào Player prefab nào → mọi GetComponent&lt;AccessoryEffects&gt;() trả null và
    /// toàn bộ hiệu ứng accessory không bao giờ chạy.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class AccessoryEffects : NetworkBehaviour
    {
        /// <summary>DamageShield: đang có 1 lượt chặn sẵn sàng (chặn trọn 1 đòn rồi mất).</summary>
        [Networked] public NetworkBool ShieldReady { get; set; }
        [Networked] private TickTimer ShieldRecharge { get; set; }
        [Networked] public NetworkBool PostSkillArmed { get; set; }
        /// <summary>HealthRegen: đếm tới nhịp hồi kế tiếp (effectCooldown giây).</summary>
        [Networked] private TickTimer RegenTimer { get; set; }

        /// <summary>
        /// AttackBuff (acc_potion): còn hiệu lực tới hết timer này → đòn đánh THƯỜNG được nhân sát thương.
        /// Kích NGAY KHI TRANG BỊ accessory (PlayerInventory gọi ArmBuffsOnEquip), chạy `effectDuration` (60s).
        /// </summary>
        [Networked] private TickTimer AttackBuffTimer { get; set; }

        /// <summary>
        /// SkillBuff (acc_postskill): còn hiệu lực tới hết timer này → mọi SKILL tung ra được nhân sát thương.
        /// Cũng kích lúc TRANG BỊ (xem ArmBuffsOnEquip), không phải lúc tung skill.
        /// </summary>
        [Networked] private TickTimer SkillBuffTimer { get; set; }

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
            TickShieldRecharge(acc);

            if (acc == null || acc.effect != DamageEffectType.HealthRegen)
            {
                RegenTimer = TickTimer.None; // tháo charm → nhịp hồi bắt đầu lại từ đầu khi mặc lại
                return;
            }

            // HỒI THEO NHỊP: cứ effectCooldown giây hồi effectMagnitude HP, không phụ thuộc ngưỡng HP.
            // (Trước đây effectMagnitude là HP/giây và chỉ chạy khi HP dưới effectThreshold.)
            float interval = Mathf.Max(0.5f, acc.effectCooldown);
            if (!RegenTimer.ExpiredOrNotRunning(Runner)) return;
            RegenTimer = TickTimer.CreateFromSeconds(Runner, interval);

            int gain = Mathf.Max(1, Mathf.RoundToInt(acc.effectMagnitude));
            if (_stats.CurrentHP < _stats.MaxHP)
                _stats.CurrentHP = Mathf.Min(_stats.MaxHP, _stats.CurrentHP + gain);
        }

        /// <summary>
        /// DamageShield: cứ effectCooldown giây có LẠI 1 lượt chặn, lượt đó tồn tại tới khi bị đánh.
        /// Vừa trang bị (chưa từng chặn) → có ngay 1 lượt. Tháo charm → mất lượt đang giữ.
        /// </summary>
        private void TickShieldRecharge(AccessorySO acc)
        {
            bool hasShieldCharm = acc != null && acc.effect == DamageEffectType.DamageShield;
            if (!hasShieldCharm)
            {
                if (ShieldReady) ShieldReady = false;
                return;
            }
            if (!ShieldReady && ShieldRecharge.ExpiredOrNotRunning(Runner)) ShieldReady = true;
        }

        // ─────────────────────── ĐÁNH TRÚNG QUÁI ───────────────────────

        /// <summary>Gọi khi player này ĐÁNH TRÚNG 1 quái (máy có InputAuthority). dmgDealt = damage đã gây.
        /// Kích Burn/Slow lên quái + Lifesteal cho bản thân qua RPC lên host.</summary>
        public void OnDealtDamageToEnemy(Attrition.Controllers.EnemyController enemy, int dmgDealt)
        {
            var acc = Current();
            if (acc == null) return;

            switch (acc.effect)
            {
                case DamageEffectType.Burn:
                    // Thiêu đốt: áp cho MỌI bậc quái, kể cả Elite/Boss.
                    if (enemy != null)
                        enemy.RpcApplyBurn(Mathf.RoundToInt(acc.effectMagnitude), acc.effectDuration);
                    break;
                case DamageEffectType.Slow:
                    // Làm chậm: CHỈ quái thường + Elite. Boss miễn nhiễm (khống chế boss quá mạnh).
                    if (enemy != null && enemy.Tier != EnemyTier.Boss)
                        enemy.RpcApplySlow(Mathf.Clamp01(acc.effectMagnitude), acc.effectDuration);
                    break;
                case DamageEffectType.Lifesteal:
                    RpcLifesteal(Mathf.Max(1, Mathf.RoundToInt(dmgDealt * acc.effectMagnitude)));
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

        // ─────────────────────── SHIELD (host, trong TakeDamage) ───────────────────────

        /// <summary>
        /// Chặn TRỌN 1 đòn nếu đang có lượt chặn (trả 0), rồi bắt đầu đếm effectCooldown giây để nạp lại.
        /// Không có lượt → trả nguyên damage. Chỉ host.
        /// </summary>
        public int AbsorbWithShield(int incoming)
        {
            if (!HasStateAuthority || incoming <= 0 || !ShieldReady) return incoming;

            var acc = Current();
            if (acc == null || acc.effect != DamageEffectType.DamageShield) return incoming;

            ShieldReady = false;
            ShieldRecharge = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, acc.effectCooldown));
            return 0;
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

        /// <summary>
        /// Nhân damage ĐÒN ĐÁNH THƯỜNG. Gộp 2 nguồn (mỗi lúc chỉ 1 accessory trang bị nên không cộng dồn):
        ///  - PostSkillDamage: đòn KẾ TIẾP sau skill (cờ 1 lần).
        ///  - AttackBuff (acc_potion): mọi đòn trong effectDuration giây kể từ lúc TRANG BỊ accessory.
        /// PlayerCombat gọi.
        /// </summary>
        public int ApplyAttackDamageMultiplier(int damage)
        {
            var acc = Current();
            if (acc == null) return damage;

            if (acc.effect == DamageEffectType.PostSkillDamage)
                return PostSkillArmed ? Mathf.RoundToInt(damage * Mathf.Max(1f, acc.effectMagnitude)) : damage;

            if (acc.effect == DamageEffectType.AttackBuff && IsBuffActive(AttackBuffTimer))
                return Mathf.RoundToInt(damage * Mathf.Max(1f, acc.effectMagnitude));

            return damage;
        }

        // ─────────────────────── BUFF CÓ THỜI HẠN (AttackBuff / SkillBuff) ───────────────────────

        /// <summary>
        /// Timer còn chạy? `ExpiredOrNotRunning` trả TRUE cả khi timer CHƯA từng chạy, nên phải kiểm
        /// nghịch đảo — nếu dùng trực tiếp thì buff "luôn active" trước lần kích đầu tiên.
        /// </summary>
        private bool IsBuffActive(TickTimer t)
        {
            if (Object == null || !Object.IsValid) return false;
            return !t.ExpiredOrNotRunning(Runner);
        }

        /// <summary>
        /// PlayerInventory (host) gọi NGAY KHI VỪA TRANG BỊ accessory → bật buff có thời hạn.
        ///
        /// MỐC KÍCH HOẠT LÀ LÚC TRANG BỊ (user chốt 2026-07-30), không phải lúc uống bình / tung skill như
        /// bản trước:
        ///   • acc_potion   (AttackBuff) → tăng sát thương ĐÒN ĐÁNH THƯỜNG trong `effectDuration` (60s).
        ///   • acc_postskill (SkillBuff) → tăng sát thương KĨ NĂNG trong `effectDuration` (60s).
        ///
        /// Vì buff chỉ chạy 1 phút sau khi mặc, và đổi accessory lại phải về checkpoint (xem
        /// PlayerInventory.CanSwapAccessory), nên đây là loại buff "dùng trước khi vào trận" — hợp với việc
        /// người chơi phải cân nhắc thời điểm mặc.
        /// </summary>
        public void ArmBuffsOnEquip()
        {
            if (!HasStateAuthority) return;

            var acc = Current();
            if (acc == null) return;

            float dur = Mathf.Max(0.1f, acc.effectDuration);
            if (acc.effect == DamageEffectType.AttackBuff)
                AttackBuffTimer = TickTimer.CreateFromSeconds(Runner, dur);
            else if (acc.effect == DamageEffectType.SkillBuff)
                SkillBuffTimer = TickTimer.CreateFromSeconds(Runner, dur);
        }

        /// <summary>
        /// Nhân damage SKILL khi SkillBuff đang hiệu lực (1× nếu không). PlayerSkillCaster gọi.
        ///
        /// Buff bật NGAY khi tung skill nên chính skill đó cũng được nhân — đúng ý "tăng sát thương kĩ năng
        /// tung ra trong một khoảng thời gian".
        /// </summary>
        public int ApplySkillDamageMultiplier(int damage)
        {
            var acc = Current();
            if (acc == null || acc.effect != DamageEffectType.SkillBuff) return damage;
            if (!IsBuffActive(SkillBuffTimer)) return damage;
            return Mathf.RoundToInt(damage * Mathf.Max(1f, acc.effectMagnitude));
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcConsumePostSkill() => PostSkillArmed = false;
    }
}
