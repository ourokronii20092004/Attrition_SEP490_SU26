using UnityEngine;

namespace Attrition.Data
{
    public enum ConsumableKind { HealthPotion, ManaPotion }

    /// <summary>
    /// STATIC data — bình hồi (HP/Mana). Lượng hồi cố định hoặc theo % máu tối đa.
    /// Cơ chế Sekiro/Afterimage: rest hồi đầy số bình; giết elite/giải mission tăng cap.
    /// Tạo asset: Create → Attrition → Consumable.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Consumable", fileName = "Consumable")]
    public class ConsumableSO : ScriptableObject
    {
        [Header("---- IDENTITY ----")]
        public string consumableId = "health_potion";
        public string displayName = "Health Potion";
        public Sprite icon;
        public ConsumableKind kind = ConsumableKind.HealthPotion;

        [Header("---- RESTORE AMOUNT ----")]
        [Tooltip("Lượng hồi cố định (HP hoặc Mana).")]
        public int flatRestore = 50;
        [Tooltip("Hồi thêm theo % giá trị tối đa (0..1). Tổng = flat + percent*max.")]
        [Range(0f, 1f)] public float percentOfMax = 0f;

        public int ComputeRestore(int maxValue)
        {
            return flatRestore + Mathf.RoundToInt(percentOfMax * maxValue);
        }
    }
}
