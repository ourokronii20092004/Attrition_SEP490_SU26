namespace Enemy.Service.DTOs;

public record LootEntryDto(string ItemName, string Rarity, string? IconKey, float DropChance, short MinQty, short MaxQty);

public record EnemyResponse(
    string EnemyId,
    string Name,
    string Tier,
    string? SpawnBiome,
    int Hp,
    int Ad,
    int Ap,
    int Def,
    int Res,
    float AttackSpeed,
    bool IsRanged,
    int ExpReward,
    int GoldReward,
    string? Lore,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<LootEntryDto> LootTable
);

public record EnemyCreateRequest(
    string EnemyId,
    string Name,
    string Tier,
    string? SpawnBiome,
    int Hp,
    int Ad,
    int Ap,
    int Def,
    int Res,
    float AttackSpeed,
    bool IsRanged,
    int ExpReward,
    int GoldReward,
    string? Lore,
    List<LootEntryDto>? LootTable
);

public record EnemyUpdateRequest(
    string Name,
    string Tier,
    string? SpawnBiome,
    int Hp,
    int Ad,
    int Ap,
    int Def,
    int Res,
    float AttackSpeed,
    bool IsRanged,
    int ExpReward,
    int GoldReward,
    string? Lore,
    List<LootEntryDto>? LootTable
);

// Summary projection used by Search aggregator.
public record EnemySummaryDto(string EnemyId, string Name, string Tier);
