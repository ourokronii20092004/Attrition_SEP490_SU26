using UnityEngine;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Chuyển tiếp Animation Event từ Visual (nơi đặt Animator sau khi tách khỏi root để nội suy mượt)
    /// lên PlayerCombat ở GameObject cha. Unity chỉ gọi Animation Event tới component NẰM CÙNG
    /// GameObject với Animator — sau khi Animator dời xuống Visual, các method TriggerAttackDamage/
    /// TriggerChargeAttackDamage/FinishAttack (ở PlayerCombat trên root) mất receiver. Script này
    /// đứng cùng Animator để nhận, rồi gọi lại PlayerCombat.
    /// </summary>
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;

        private void Awake()
        {
            if (combat == null) combat = GetComponentInParent<PlayerCombat>();
        }

        // Tên các method PHẢI khớp chính xác functionName trong file .anim.
        public void TriggerAttackDamage()
        {
            if (combat != null) combat.TriggerAttackDamage();
        }

        public void TriggerChargeAttackDamage()
        {
            if (combat != null) combat.TriggerChargeAttackDamage();
        }

        public void FinishAttack()
        {
            if (combat != null) combat.FinishAttack();
        }
    }
}
