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
    List<LootEntryDto> LootTable,
    string? ImageUrl = null,
    int Poise = 0,
    float PoiseRecoveryTime = 3f,
    float PatrolSpeed = 2f,
    float ChaseSpeed = 5f
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
    List<LootEntryDto>? LootTable,
    string? ImageUrl = null,
    int Poise = 0,
    float PoiseRecoveryTime = 3f,
    float PatrolSpeed = 2f,
    float ChaseSpeed = 5f
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
    List<LootEntryDto>? LootTable,
    string? ImageUrl = null,
    int Poise = 0,
    float PoiseRecoveryTime = 3f,
    float PatrolSpeed = 2f,
    float ChaseSpeed = 5f
);

// Summary projection used by Search aggregator.
public record EnemySummaryDto(string EnemyId, string Name, string Tier);

/// <summary>
/// Cục config gộp cho GAME tải 1 lần trước trận. Version = MAX(UpdatedAt) toàn bảng enemy
/// (ISO-8601). Game cache version; lần sau chỉ tải lại bundle khi version đổi (admin sửa web).
/// </summary>
public record GameConfigBundle(string Version, int Count, List<EnemyResponse> Enemies);

/// <summary>Chỉ trả version (nhẹ) để game so trước khi quyết định tải full bundle.</summary>
public record GameConfigVersion(string Version, int Count);

/// <summary>
/// Version gộp cho cả enemy + item trong 1 request (GET /api/gameconfig/versions). Game so
/// 1 lần rồi chỉ tải lại bundle (/api/gameconfig hoặc /api/itemconfig) của phần nào đổi.
/// </summary>
public record GameConfigVersions(
    string EnemyVersion, int EnemyCount,
    string ItemVersion, int ItemCount,
    string SkillVersion, int SkillCount);
