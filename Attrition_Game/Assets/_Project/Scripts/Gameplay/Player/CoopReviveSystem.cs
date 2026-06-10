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
                // Consume 1 flask
                if (_myPotions.TryUseHealthPotion())
                {
                    target.isDeadNetworked = false;
                    
                    var targetStats = target.GetComponent<PlayerStats>();
                    if (targetStats != null)
                    {
                        // Revive with half HP or something, rules didn't strictly say, assume MaxHP / 2
                        targetStats.CurrentHP = targetStats.MaxHP / 2;
                    }
                    else
                    {
                        target.HP = target.maxHP / 2;
                    }

                    // BR-18: 3.0s invincibility on revive
                    target.GrantReviveInvincibility(3.0f);
                }
            }
            CancelRevive();
        }

        private PlayerController FindDeadPlayerInRadius()
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p != _myController && p.IsDead)
                {
                    if (Vector3.Distance(transform.position, p.transform.position) <= reviveRadius)
                    {
                        return p;
                    }
                }
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
