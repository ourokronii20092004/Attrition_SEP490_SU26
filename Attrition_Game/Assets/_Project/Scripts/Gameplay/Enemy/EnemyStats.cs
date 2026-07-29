using System.Linq;
using Fusion;
using UnityEngine;
using Attrition.Core;
using Attrition.Data;
using Attrition.Systems;
using Attrition.Persistence;

namespace Attrition.Gameplay.Enemy
{
    /// <summary>
    /// Nguồn chỉ số DUY NHẤT của quái lúc runtime.
    /// Host build sheet = EnemyStatsSO (default) ⊕ override từ web (EnemyStatProvider) ⊕ coop scaling,
    /// rồi sync con số đã chốt xuống client qua [Networked]. Client KHÔNG gọi API.
    /// EnemyController đọc MaxHP/AD/DEF... qua đây thay vì field hard-code.
    /// </summary>
    public class EnemyStats : NetworkBehaviour
    {
        [Header("---- STATIC DATA ----")]
        [Tooltip("ScriptableObject chỉ số gốc của loại quái này.")]
        [SerializeField] private EnemyStatsSO statsSO;

        // Con số đã chốt (host tính, sync xuống client)
        [Networked] public int MaxHP { get; set; }
        [Networked] public int AD { get; set; }
        [Networked] public int AP { get; set; }
        [Networked] public int DEF { get; set; }
        [Networked] public int RES { get; set; }
        [Networked] public int Poise { get; set; }
        [Networked] public float PoiseRecoveryTime { get; set; }
        [Networked] public float PatrolSpeed { get; set; }
        [Networked] public float ChaseSpeed { get; set; }
        [Networked] public float AttackSpeed { get; set; }
        [Networked] public int RuntimeExpReward { get; set; }

        public EnemyTier Tier => statsSO != null ? statsSO.tier : EnemyTier.Normal;
        public string EnemyId => statsSO != null ? statsSO.enemyId : null;
        public EnemyStatsSO StatsSO => statsSO;
        public int ExpReward => RuntimeExpReward;

        // Fallback khi chưa gán SO (prefab cũ chưa migrate)
        private const int FallbackHP = 30, FallbackAD = 10;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            if (statsSO == null)
            {
                MaxHP = FallbackHP; AD = FallbackAD;
                var fallbackCtrl = GetComponent<Attrition.Controllers.EnemyController>();
                if (fallbackCtrl != null)
                {
                    fallbackCtrl.maxHealth = MaxHP;
                    fallbackCtrl.Health = MaxHP;
                }
                return;
            }

            bool isCoop = Runner != null && Runner.ActivePlayers.Count() > 1;
            var ovr = EnemyStatProvider.Instance != null
                ? EnemyStatProvider.Instance.GetOverride(statsSO.enemyId)
                : null;

            var sheet = EnemyStatSheet.Build(statsSO, ovr, isCoop);
            MaxHP = sheet.MaxHP;
            AD = sheet.AD;
            AP = sheet.AP;
            DEF = sheet.DEF;
            RES = sheet.RES;
            Poise = sheet.Poise;

            PoiseRecoveryTime = Mathf.Max(0f, ovr?.poiseRecoveryTime ?? statsSO.poiseRecoveryTime);
            PatrolSpeed = Mathf.Max(0f, ovr?.patrolSpeed ?? statsSO.patrolSpeed);
            ChaseSpeed = Mathf.Max(0f, ovr?.chaseSpeed ?? statsSO.chaseSpeed);
            AttackSpeed = Mathf.Max(0.01f, ovr?.attackSpeed ?? statsSO.attackSpeed);
            RuntimeExpReward = Mathf.Max(0, ovr?.expReward ?? statsSO.expReward);

            var ctrl = GetComponent<Attrition.Controllers.EnemyController>();
            if (ctrl != null)
            {
                ctrl.maxHealth = MaxHP;
                ctrl.Health = MaxHP;
            }
        }

        /// <summary>Sát thương quái gây lên player (phòng thủ-aware).</summary>
        public int ComputeContactDamage(int targetDef)
            => DamageCalculator.Compute(DamageType.Physical, AD, targetDef, 0);

        /// <summary>Boss phase: tăng nhịp độ (di chuyển + đánh). Chỉ host (giá trị networked).</summary>
        public void ApplyPhaseSpeedMultiplier(float mult)
        {
            if (!HasStateAuthority || mult <= 0f) return;
            PatrolSpeed *= mult;
            ChaseSpeed *= mult;
            AttackSpeed *= mult;
        }

        /// <summary>
        /// Trả tốc độ về giá trị GỐC (SO ⊕ override web), xoá hệ số phase đã nhân dồn.
        /// Dùng khi đánh lại boss sau khi cả team chết — nếu không, boss giữ tốc phase 3 ở lượt đánh mới.
        /// Chỉ host.
        /// </summary>
        public void RebuildBaseSpeeds()
        {
            if (!HasStateAuthority || statsSO == null) return;

            var ovr = EnemyStatProvider.Instance != null
                ? EnemyStatProvider.Instance.GetOverride(statsSO.enemyId)
                : null;

            PatrolSpeed = Mathf.Max(0f, ovr?.patrolSpeed ?? statsSO.patrolSpeed);
            ChaseSpeed = Mathf.Max(0f, ovr?.chaseSpeed ?? statsSO.chaseSpeed);
            AttackSpeed = Mathf.Max(0.01f, ovr?.attackSpeed ?? statsSO.attackSpeed);
        }
    }
}
