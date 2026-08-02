namespace Enemy.Service.DTOs;

public record UnityItemImport(
    string ItemId, string Name, string Category, string? Description, int MaxStack,
    bool IsKeyItem, List<ItemModifierDto>? Modifiers, string? ImageUrl = null);

public record UnityLootImport(
    string ItemId, float DropChance, short MinQty = 1, short MaxQty = 1);

public record UnityEnemyImport(
    string EnemyId, string Name, string Tier, int Hp, int Ad, int Ap, int Def, int Res,
    int Poise, float PoiseRecoveryTime, float PatrolSpeed, float ChaseSpeed,
    float AttackSpeed, int ExpReward, string? ImageUrl = null, List<UnityLootImport>? LootTable = null);

public record GameDataImportRequest(List<UnityItemImport> Items, List<UnityEnemyImport> Enemies);
public record ImportCounts(int Created, int BaselinesUpdated, int Unchanged);
public record GameDataImportResult(ImportCounts Items, ImportCounts Enemies, string EnemyVersion, string ItemVersion);
