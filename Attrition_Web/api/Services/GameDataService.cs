using Attrition.API.Models;
using Attrition.API.Repositories;
using Attrition.API.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace Attrition.API.Services;

public class GameDataService : IGameDataService
{
    private readonly IRepository<Item> _itemRepo;
    private readonly IEnemyRepository _enemyRepo;
    private readonly IRepository<Skill> _skillRepo;
    private readonly IRepository<Level> _levelRepo;
    private readonly IRepository<SpawnPoint> _spawnRepo;
    private readonly IRepository<GameConfig> _configRepo;
    private readonly IConnectionMultiplexer _redis;

    public GameDataService(
        IRepository<Item> itemRepo,
        IEnemyRepository enemyRepo,
        IRepository<Skill> skillRepo,
        IRepository<Level> levelRepo,
        IRepository<SpawnPoint> spawnRepo,
        IRepository<GameConfig> configRepo,
        IConnectionMultiplexer redis)
    {
        _itemRepo = itemRepo;
        _enemyRepo = enemyRepo;
        _skillRepo = skillRepo;
        _levelRepo = levelRepo;
        _spawnRepo = spawnRepo;
        _configRepo = configRepo;
        _redis = redis;
    }

    private async Task PublishEventAsync(string entity, string action, object data)
    {
        var pubsub = _redis.GetSubscriber();
        var msg = JsonSerializer.Serialize(new { Entity = entity, Action = action, Data = data, Timestamp = DateTime.UtcNow });
        await pubsub.PublishAsync(RedisChannel.Literal("game-server-events"), msg);
    }

    public async Task<List<Item>> GetItemsAsync()
    {
        return (await _itemRepo.GetAllAsync()).ToList();
    }

    public async Task<List<Enemy>> GetEnemiesAsync()
    {
        var (enemies, _) = await _enemyRepo.GetPagedAsync(
            1, int.MaxValue, null, null,
            e => e.LootTable
        );
        return enemies;
    }

    public async Task<List<Skill>> GetSkillsAsync()
    {
        var (skills, _) = await _skillRepo.GetPagedAsync(
            1, int.MaxValue, null, null,
            s => s.Effects
        );
        return skills;
    }

    public async Task<List<Level>> GetLevelsAsync()
    {
        var (levels, _) = await _levelRepo.GetPagedAsync(
            1, int.MaxValue, null,
            q => q.OrderBy(l => l.LevelNumber)
        );
        return levels;
    }

    // Enemies CRUD
    public async Task<Enemy?> GetEnemyByIdAsync(string id)
    {
        var (enemies, _) = await _enemyRepo.GetPagedAsync(1, 1, e => e.EnemyId == id, null, e => e.LootTable);
        return enemies.FirstOrDefault();
    }

    public async Task<Enemy> CreateEnemyAsync(Enemy enemy)
    {
        await _enemyRepo.AddAsync(enemy);
        await PublishEventAsync("Enemy", "Created", enemy);
        return enemy;
    }

    public async Task<Enemy?> UpdateEnemyAsync(string id, Enemy enemy)
    {
        var existing = await GetEnemyByIdAsync(id);
        if (existing == null) return null;

        existing.Name = enemy.Name;
        existing.Tier = enemy.Tier;
        existing.SpawnBiome = enemy.SpawnBiome;
        existing.Hp = enemy.Hp;
        existing.Ad = enemy.Ad;
        existing.Ap = enemy.Ap;
        existing.Def = enemy.Def;
        existing.Res = enemy.Res;
        existing.AttackSpeed = enemy.AttackSpeed;
        existing.IsRanged = enemy.IsRanged;
        existing.ExpReward = enemy.ExpReward;
        existing.GoldReward = enemy.GoldReward;

        // Sync LootTable
        existing.LootTable.Clear();
        foreach (var entry in enemy.LootTable)
        {
            existing.LootTable.Add(entry);
        }

        await _enemyRepo.UpdateAsync(existing);
        await PublishEventAsync("Enemy", "Updated", existing);
        return existing;
    }

    public async Task<bool> DeleteEnemyAsync(string id)
    {
        var existing = await GetEnemyByIdAsync(id);
        if (existing == null) return false;
        await _enemyRepo.DeleteAsync(existing);
        await PublishEventAsync("Enemy", "Deleted", new { EnemyId = id });
        return true;
    }

    // Items CRUD
    public async Task<Item?> GetItemByIdAsync(int id)
    {
        return await _itemRepo.GetByIdAsync(id);
    }

    public async Task<Item> CreateItemAsync(Item item)
    {
        await _itemRepo.AddAsync(item);
        await PublishEventAsync("Item", "Created", item);
        return item;
    }

    public async Task<Item?> UpdateItemAsync(int id, Item item)
    {
        var existing = await _itemRepo.GetByIdAsync(id);
        if (existing == null) return null;

        existing.Name = item.Name;
        existing.ItemType = item.ItemType;
        existing.Rarity = item.Rarity;
        existing.StackLimit = item.StackLimit;
        existing.Description = item.Description;
        existing.IconKey = item.IconKey;

        // If it's a Gear, update gear stats
        if (existing is Gear gExisting && item is Gear gItem)
        {
            gExisting.EquipSlot = gItem.EquipSlot;
            gExisting.BonusAd = gItem.BonusAd;
            gExisting.BonusAp = gItem.BonusAp;
            gExisting.BonusDef = gItem.BonusDef;
            gExisting.BonusRes = gItem.BonusRes;
            gExisting.BonusAttackSpeed = gItem.BonusAttackSpeed;
            gExisting.BonusMaxHp = gItem.BonusMaxHp;
            gExisting.BonusMaxMana = gItem.BonusMaxMana;
            gExisting.RequiredLevel = gItem.RequiredLevel;
        }

        await _itemRepo.UpdateAsync(existing);
        await PublishEventAsync("Item", "Updated", existing);
        return existing;
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        var existing = await _itemRepo.GetByIdAsync(id);
        if (existing == null) return false;
        await _itemRepo.DeleteAsync(existing);
        await PublishEventAsync("Item", "Deleted", new { ItemId = id });
        return true;
    }

    // Skills CRUD
    public async Task<Skill?> GetSkillByIdAsync(int id)
    {
        var (skills, _) = await _skillRepo.GetPagedAsync(1, 1, s => s.SkillId == id, null, s => s.Effects);
        return skills.FirstOrDefault();
    }

    public async Task<Skill> CreateSkillAsync(Skill skill)
    {
        await _skillRepo.AddAsync(skill);
        await PublishEventAsync("Skill", "Created", skill);
        return skill;
    }

    public async Task<Skill?> UpdateSkillAsync(int id, Skill skill)
    {
        var existing = await GetSkillByIdAsync(id);
        if (existing == null) return null;

        existing.Name = skill.Name;
        existing.SkillType = skill.SkillType;
        existing.DamageType = skill.DamageType;
        existing.ManaCost = skill.ManaCost;
        existing.StaminaCost = skill.StaminaCost;
        existing.CooldownSec = skill.CooldownSec;
        existing.BaseDamage = skill.BaseDamage;
        existing.Description = skill.Description;
        existing.IconKey = skill.IconKey;
        existing.RequiredLevel = skill.RequiredLevel;

        existing.Effects.Clear();
        foreach (var fx in skill.Effects)
        {
            existing.Effects.Add(fx);
        }

        await _skillRepo.UpdateAsync(existing);
        await PublishEventAsync("Skill", "Updated", existing);
        return existing;
    }

    public async Task<bool> DeleteSkillAsync(int id)
    {
        var existing = await _skillRepo.GetByIdAsync(id);
        if (existing == null) return false;
        await _skillRepo.DeleteAsync(existing);
        await PublishEventAsync("Skill", "Deleted", new { SkillId = id });
        return true;
    }

    // Levels CRUD
    public async Task<Level?> GetLevelByNumberAsync(int levelNumber)
    {
        return await _levelRepo.GetByIdAsync(levelNumber);
    }

    public async Task<Level> CreateLevelAsync(Level level)
    {
        await _levelRepo.AddAsync(level);
        await PublishEventAsync("Level", "Created", level);
        return level;
    }

    public async Task<Level?> UpdateLevelAsync(int levelNumber, Level level)
    {
        var existing = await _levelRepo.GetByIdAsync(levelNumber);
        if (existing == null) return null;

        existing.ExpRequired = level.ExpRequired;
        existing.HpGrowth = level.HpGrowth;
        existing.AdGrowth = level.AdGrowth;
        existing.ApGrowth = level.ApGrowth;
        existing.DefGrowth = level.DefGrowth;
        existing.ResGrowth = level.ResGrowth;
        existing.SpeedGrowth = level.SpeedGrowth;

        await _levelRepo.UpdateAsync(existing);
        await PublishEventAsync("Level", "Updated", existing);
        return existing;
    }

    public async Task<bool> DeleteLevelAsync(int levelNumber)
    {
        var existing = await _levelRepo.GetByIdAsync(levelNumber);
        if (existing == null) return false;
        await _levelRepo.DeleteAsync(existing);
        await PublishEventAsync("Level", "Deleted", new { LevelNumber = levelNumber });
        return true;
    }

    // SpawnPoints CRUD
    public async Task<List<SpawnPoint>> GetSpawnPointsAsync()
    {
        return (await _spawnRepo.GetAllAsync()).ToList();
    }

    public async Task<SpawnPoint?> GetSpawnPointByIdAsync(Guid id)
    {
        return await _spawnRepo.GetByIdAsync(id);
    }

    public async Task<SpawnPoint> CreateSpawnPointAsync(SpawnPoint spawnPoint)
    {
        await _spawnRepo.AddAsync(spawnPoint);
        await PublishEventAsync("SpawnPoint", "Created", spawnPoint);
        return spawnPoint;
    }

    public async Task<SpawnPoint?> UpdateSpawnPointAsync(Guid id, SpawnPoint spawnPoint)
    {
        var existing = await _spawnRepo.GetByIdAsync(id);
        if (existing == null) return null;

        existing.EnemyId = spawnPoint.EnemyId;
        existing.SceneName = spawnPoint.SceneName;
        existing.PositionX = spawnPoint.PositionX;
        existing.PositionY = spawnPoint.PositionY;
        existing.PositionZ = spawnPoint.PositionZ;
        existing.SpawnIntervalSeconds = spawnPoint.SpawnIntervalSeconds;
        existing.MaxActiveCount = spawnPoint.MaxActiveCount;

        await _spawnRepo.UpdateAsync(existing);
        await PublishEventAsync("SpawnPoint", "Updated", existing);
        return existing;
    }

    public async Task<bool> DeleteSpawnPointAsync(Guid id)
    {
        var existing = await _spawnRepo.GetByIdAsync(id);
        if (existing == null) return false;
        await _spawnRepo.DeleteAsync(existing);
        await PublishEventAsync("SpawnPoint", "Deleted", new { Id = id });
        return true;
    }

    // GameConfigs CRUD
    public async Task<List<GameConfig>> GetGameConfigsAsync()
    {
        return (await _configRepo.GetAllAsync()).ToList();
    }

    public async Task<GameConfig?> GetGameConfigByKeyAsync(string key)
    {
        return await _configRepo.GetByIdAsync(key);
    }

    public async Task<GameConfig> CreateGameConfigAsync(GameConfig gameConfig)
    {
        await _configRepo.AddAsync(gameConfig);
        await PublishEventAsync("GameConfig", "Created", gameConfig);
        return gameConfig;
    }

    public async Task<GameConfig?> UpdateGameConfigAsync(string key, GameConfig gameConfig)
    {
        var existing = await _configRepo.GetByIdAsync(key);
        if (existing == null) return null;

        existing.Value = gameConfig.Value;
        existing.Description = gameConfig.Description;

        await _configRepo.UpdateAsync(existing);
        await PublishEventAsync("GameConfig", "Updated", existing);
        return existing;
    }

    public async Task<bool> DeleteGameConfigAsync(string key)
    {
        var existing = await _configRepo.GetByIdAsync(key);
        if (existing == null) return false;
        await _configRepo.DeleteAsync(existing);
        await PublishEventAsync("GameConfig", "Deleted", new { Key = key });
        return true;
    }

    public async Task<GameBulkData> ExportAllAsync()
    {
        return new GameBulkData(
            await GetItemsAsync(),
            await GetEnemiesAsync(),
            await GetSkillsAsync(),
            await GetLevelsAsync(),
            await GetSpawnPointsAsync(),
            await GetGameConfigsAsync()
        );
    }

    public async Task ImportAllAsync(GameBulkData data)
    {
        // Import Items
        if (data.Items != null)
        {
            foreach (var item in data.Items)
            {
                var existing = await GetItemByIdAsync(item.ItemId);
                if (existing != null) await UpdateItemAsync(item.ItemId, item);
                else await CreateItemAsync(item);
            }
        }

        // Import Enemies
        if (data.Enemies != null)
        {
            foreach (var enemy in data.Enemies)
            {
                var existing = await GetEnemyByIdAsync(enemy.EnemyId);
                if (existing != null) await UpdateEnemyAsync(enemy.EnemyId, enemy);
                else await CreateEnemyAsync(enemy);
            }
        }

        // Import Skills
        if (data.Skills != null)
        {
            foreach (var skill in data.Skills)
            {
                var existing = await GetSkillByIdAsync(skill.SkillId);
                if (existing != null) await UpdateSkillAsync(skill.SkillId, skill);
                else await CreateSkillAsync(skill);
            }
        }

        // Import Levels
        if (data.Levels != null)
        {
            foreach (var level in data.Levels)
            {
                var existing = await GetLevelByNumberAsync(level.LevelNumber);
                if (existing != null) await UpdateLevelAsync(level.LevelNumber, level);
                else await CreateLevelAsync(level);
            }
        }

        // Import SpawnPoints
        if (data.SpawnPoints != null)
        {
            foreach (var sp in data.SpawnPoints)
            {
                var existing = await GetSpawnPointByIdAsync(sp.Id);
                if (existing != null) await UpdateSpawnPointAsync(sp.Id, sp);
                else await CreateSpawnPointAsync(sp);
            }
        }

        // Import GameConfigs
        if (data.GameConfigs != null)
        {
            foreach (var config in data.GameConfigs)
            {
                var existing = await GetGameConfigByKeyAsync(config.Key);
                if (existing != null) await UpdateGameConfigAsync(config.Key, config);
                else await CreateGameConfigAsync(config);
            }
        }
        await PublishEventAsync("GameBulkData", "Imported", new { Count = 1 });
    }
}