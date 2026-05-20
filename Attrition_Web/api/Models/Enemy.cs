namespace Attrition.API.Models;

public class Enemy
{
    public string EnemyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string? SpawnBiome { get; set; }
    public int Hp { get; set; }
    public int Ad { get; set; } = 0;
    public int Ap { get; set; } = 0;
    public int Def { get; set; } = 0;
    public int Res { get; set; } = 0;
    public float AttackSpeed { get; set; } = 1.0f;
    public bool IsRanged { get; set; } = false;
    public int ExpReward { get; set; } = 0;
    public int GoldReward { get; set; } = 0;

    public ICollection<EnemyLootEntry> LootTable { get; set; } = new List<EnemyLootEntry>();
}