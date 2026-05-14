using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Một biom: danh sách prefab quái + trọng số (random có lặp lại cùng loại).
/// Tạo asset: chuột phải Project → Create → Attrition → Enemy Biome.
/// </summary>
[CreateAssetMenu(menuName = "Attrition/Enemy Biome", fileName = "EnemyBiome")]
public class EnemyBiomeDefinition : ScriptableObject
{
    [Tooltip("Tên hiển thị / id sau này (load map theo biom).")]
    public string biomeId = "Default";

    [Serializable]
    public class WeightedEnemyPrefab
    {
        [Tooltip("Prefab gốc có NetworkObject ở root (giống Axe_Demon / Skeleton_Sword).")]
        public NetworkObject prefab;
        [Min(1)] public int weight = 1;
    }

    public WeightedEnemyPrefab[] pool = Array.Empty<WeightedEnemyPrefab>();

    /// <summary>Random có trùng — mỗi lần gọi là một lần roll độc lập.</summary>
    public NetworkObject PickRandomPrefab()
    {
        if (pool == null || pool.Length == 0)
            return null;

        int total = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].prefab == null) continue;
            total += Mathf.Max(1, pool[i].weight);
        }

        if (total <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].prefab == null) continue;
            int w = Mathf.Max(1, pool[i].weight);
            roll -= w;
            if (roll < 0)
                return pool[i].prefab;
        }

        for (int i = pool.Length - 1; i >= 0; i--)
        {
            if (pool[i].prefab != null)
                return pool[i].prefab;
        }

        return null;
    }
}
