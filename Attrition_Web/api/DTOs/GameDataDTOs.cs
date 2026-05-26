namespace Attrition.API.DTOs;

public record CreateItemRequest(
    string Name,
    string ItemType,
    string Rarity,
    short StackLimit,
    string? Description,
    string? IconKey,
    string? EquipSlot,
    int? BonusAd,
    int? BonusAp,
    int? BonusDef,
    int? BonusRes,
    float? BonusAttackSpeed,
    int? BonusMaxHp,
    int? BonusMaxMana,
    int? RequiredLevel
);

public record CreateSkillRequest(
    string Name,
    string SkillType,
    string? DamageType,
    int ManaCost,
    int StaminaCost,
    float CooldownSec,
    int BaseDamage,
    string? Description,
    string? IconKey,
    int RequiredLevel
);

public record CreateLevelRequest(
    int LevelNumber,
    int ExpRequired,
    int HpGrowth,
    int AdGrowth,
    int ApGrowth,
    int DefGrowth,
    int ResGrowth,
    int SpeedGrowth
);

public record CreateSpawnPointRequest(
    string EnemyId,
    string SceneName,
    float PositionX,
    float PositionY,
    float PositionZ,
    int SpawnIntervalSeconds,
    int MaxActiveCount
);

public record UpsertGameConfigRequest(
    string Key,
    string Value,
    string? Description
);
