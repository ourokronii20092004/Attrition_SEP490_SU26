using Fusion;
using UnityEngine;
using Attrition.Data;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Handles the Co-op revive mechanics (BR-23, BR-24, BR-25).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class CoopReviveSystem : NetworkBehaviour
    {
        [Header("---- CONFIG ----")]
        [SerializeField] private float reviveRadius = 3.0f; // BR-23
        [SerializeField] private float reviveTimeRequired = 3.0f; // BR-24

        [Networked] public NetworkBool IsReviving { get; set; }
        [Networked] public float ReviveProgress { get; set; }
        [Networked] public PlayerRef TargetPlayerToRevive { get; set; }

        private PlayerController _myController;
        private PlayerStats _myStats;
        private PotionSystem _myPotions;

        private int _lastHp;

        public override void Spawned()
        {
            _myController = GetComponent<PlayerController>();
            _myStats = GetComponent<PlayerStats>();
            _myPotions = GetComponent<PotionSystem>();
        }

        // FindObjectsByType + p.IsDead (Networked) hoạt động trên proxy, nên client đọc được
        // trạng thái đồng đội để quyết định hiện prompt "[R] HỒI SINH".

        /// <summary>True khi mình còn sống, còn bình máu, VÀ có đồng đội đã chết trong bán kính.</summary>
        public bool HasRevivableAllyNearby()
        {
            // Guard: Object chưa Spawned → đọc IsDead ([Networked]) sẽ ném. UI gọi mỗi frame kể cả lúc
            // player đang spawn, nên phải kiểm tra hợp lệ trước.
            if (_myController == null || Object == null || !Object.IsValid) return false;
            if (_myController.IsDead) return false;
            if (_myPotions == null || _myPotions.HealthCharges <= 0) return false;
            return FindDeadPlayerInRadius() != null;
        }

        /// <summary>Tiến trình hồi sinh 0..1 để UI vẽ thanh fill (đọc [Networked] ReviveProgress).</summary>
        public float ReviveFraction =>
            reviveTimeRequired > 0f ? Mathf.Clamp01(ReviveProgress / reviveTimeRequired) : 0f;

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // Update HP tracking for interruption (BR-25)
            int currentHp = _myStats != null ? _myStats.CurrentHP : _myController.HP;
            if (currentHp < _lastHp && IsReviving)
            {
                // Interrupted by damage
                CancelRevive();
            }
            _lastHp = currentHp;

            // R (giữ) để hồi sinh đồng đội — tách riêng khỏi nút Rest (F) tại checkpoint.
            if (GetInput(out NetworkInputData data))
            {
                if (data.buttons.IsSet(MyButtons.Revive) && !_myController.IsDead)
                {
                    HandleReviveLogic();
                }
                else
                {
                    if (IsReviving) CancelRevive();
                }
            }
        }

        private void HandleReviveLogic()
        {
            if (!IsReviving)
            {
                // Try to find a dead player
                var target = FindDeadPlayerInRadius();
                if (target != null && _myPotions != null && _myPotions.HealthCharges > 0)
                {
                    IsReviving = true;
                    ReviveProgress = 0f;
                    TargetPlayerToRevive = target.Object.InputAuthority;
                }
            }
            else
            {
                // Progress the revive
                ReviveProgress += Runner.DeltaTime;

                // Check distance again to prevent moving away while reviving
                var target = GetPlayerByRef(TargetPlayerToRevive);
                if (target == null || Vector3.Distance(transform.position, target.transform.position) > reviveRadius)
                {
                    CancelRevive();
                    return;
                }

                if (ReviveProgress >= reviveTimeRequired)
                {
                    CompleteRevive(target);
                }
            }
        }

        private void CancelRevive()
        {
            IsReviving = false;
            ReviveProgress = 0f;
            TargetPlayerToRevive = PlayerRef.None;
        }

        private void CompleteRevive(PlayerController target)
        {
            if (target != null)
            {
                // Trả giá 1 bình máu NHƯNG người cứu KHÔNG được hồi máu của bình đó — chỉ mất bình.
                if (_myPotions.TryConsumeHealthPotionNoHeal())
                {
                    var targetStats = target.GetComponent<PlayerStats>();
                    int reviveHp = targetStats != null ? targetStats.MaxHP / 2 : target.maxHP / 2;
                    // Khôi phục cờ chết + physics + HP trong 1 path chung (tránh xác Kinematic/collider-off).
                    target.ReviveInPlace(reviveHp);
                }
            }
            CancelRevive();
        }

        private PlayerController FindDeadPlayerInRadius()
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p == _myController) continue;
                // Guard: đọc IsDead ([Networked]) ném InvalidOperationException nếu Object chưa Spawned
                // (player khác đang trong quá trình spawn). Bỏ qua tới khi hợp lệ.
                if (p.Object == null || !p.Object.IsValid) continue;
                if (p.IsDead && Vector3.Distance(transform.position, p.transform.position) <= reviveRadius)
                    return p;
            }
            return null;
        }

        private PlayerController GetPlayerByRef(PlayerRef playerRef)
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p.Object.InputAuthority == playerRef) return p;
            }
            return null;
        }
    }
}
