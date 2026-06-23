using UnityEngine;

namespace Attrition.Data
{
    [CreateAssetMenu(menuName = "Attrition/Potion Config", fileName = "PotionConfig")]
    public class PotionConfigSO : ScriptableObject
    {
        [Header("---- POTION CHARGES ----")]
        [Tooltip("Số bình máu khởi đầu (concept: bắt đầu 3).")]
        public int startingHealthCharges = 3;
        [Tooltip("Số bình mana khởi đầu.")]
        public int startingManaCharges = 3;
        [Tooltip("Cap tuyệt đối số bình máu (concept: 7-8).")]
        public int hardMaxHealthCharges = 8;
        [Tooltip("Cap tuyệt đối số bình mana.")]
        public int hardMaxManaCharges = 8;
    }
}
