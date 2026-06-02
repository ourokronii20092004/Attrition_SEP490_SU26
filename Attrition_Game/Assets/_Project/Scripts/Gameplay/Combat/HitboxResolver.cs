using UnityEngine;

namespace Attrition.Gameplay.Combat
{
    /// <summary>
    /// Helper dùng chung cho việc quét hitbox theo hình dạng (Cone/Circle/Rectangle).
    /// Gom logic trước đây bị lặp ở EnemyCombat.TriggerAttackDamage và EliteEnemySkills.TriggerSkillDamage.
    /// KHÔNG phụ thuộc Fusion — nhận sẵn PhysicsScene2D để caller truyền Runner.GetPhysicsScene2D().
    ///
    /// Với Cone: quét tròn rồi NÉN mảng tại chỗ, chỉ giữ collider nằm trong góc. Trả về số hit hợp lệ.
    /// overlapOrigin = tâm vùng quét; angleOrigin = gốc đo góc (có thể khác overlapOrigin, vd attackPoint vs transform).
    /// </summary>
    public static class HitboxResolver
    {
        public static int Overlap(
            PhysicsScene2D scene,
            EnemyCombat.HitboxShape shape,
            Vector2 overlapOrigin,
            Vector2 angleOrigin,
            Vector2 facing,
            float range,
            float angle,
            Vector2 rectSize,
            ContactFilter2D filter,
            Collider2D[] results)
        {
            switch (shape)
            {
                case EnemyCombat.HitboxShape.Rectangle:
                {
                    float rad = Mathf.Atan2(facing.y, facing.x);
                    Vector2 center = overlapOrigin + facing * (rectSize.x / 2f);
                    return scene.OverlapBox(center, rectSize, rad * Mathf.Rad2Deg, filter, results);
                }

                case EnemyCombat.HitboxShape.Cone:
                {
                    int n = scene.OverlapCircle(overlapOrigin, range, filter, results);
                    int w = 0;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 dir = ((Vector2)results[i].transform.position - angleOrigin).normalized;
                        if (Vector2.Angle(facing, dir) < angle / 2f)
                            results[w++] = results[i];
                    }
                    return w;
                }

                case EnemyCombat.HitboxShape.Circle:
                default:
                    return scene.OverlapCircle(overlapOrigin, range, filter, results);
            }
        }
    }
}
