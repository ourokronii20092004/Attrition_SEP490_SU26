using UnityEngine;

namespace Attrition.Core
{
    /// <summary>
    /// Công thức sát thương trung tâm — KHÔNG phụ thuộc Unity scene, dễ unit-test.
    /// Physical: Max(1, ad - def) · Magic: Max(1, ap - res) · True: nguyên giá trị.
    /// Min = 1 để người chơi biết "chưa đủ chỉ số cho khu này" thay vì 0 sát thương.
    /// </summary>
    public static class DamageCalculator
    {
        public const int MinDamage = 1;

        public static int Compute(DamageType type, int rawAmount, int targetDef, int targetRes)
        {
            switch (type)
            {
                case DamageType.Physical:
                    return Mathf.Max(MinDamage, rawAmount - targetDef);
                case DamageType.Magic:
                    return Mathf.Max(MinDamage, rawAmount - targetRes);
                case DamageType.True:
                    return Mathf.Max(MinDamage, rawAmount);
                default:
                    return Mathf.Max(MinDamage, rawAmount);
            }
        }
    }
}
