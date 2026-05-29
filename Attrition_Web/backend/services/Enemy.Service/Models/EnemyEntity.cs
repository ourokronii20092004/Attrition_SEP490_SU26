namespace Enemy.Service.Models;

public class EnemyEntity
{
    public string EnemyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string? SpawnBiome { get; set; }
    public int Hp { get; set; }
    public int Ad { get; set; }
    public int Ap { get; set; }
    public int Def { get; set; }
    public int Res { get; set; }
    public float AttackSpeed { get; set; } = 1.0f;
    public bool IsRanged { get; set; }
    public int ExpReward { get; set; }
    public int GoldReward { get; set; }

    // Lore — new field supporting FE-W-03 ("lore descriptions").
    public string? Lore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Denormalized loot — owned by this enemy, no FK to any items catalog.
    public List<EnemyLootEntry> LootTable { get; set; } = new();
}

/// <summary>Denormalized loot row. Owned by EnemyEntity; carries item display data inline.</summary>
public class EnemyLootEntry
{
    public string ItemName { get; set; } = string.Empty;
    public string Rarity { get; set; } = "Common";
    public string? IconKey { get; set; }
    public float DropChance { get; set; }
    public short MinQty { get; set; } = 1;
    public short MaxQty { get; set; } = 1;
}
