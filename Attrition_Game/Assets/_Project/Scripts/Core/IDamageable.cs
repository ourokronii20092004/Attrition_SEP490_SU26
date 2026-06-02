using UnityEngine;
using Attrition.Core;

public interface IDamageable
{
    // damage = chỉ số tấn công GỐC (AD/AP). Defender tự áp DEF/RES theo type qua DamageCalculator.
    void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, DamageType type = DamageType.Physical);
    bool IsDead { get; }
}
