using Attrition.API.Models;
using Attrition.API.DTOs;

namespace Attrition.API.Services;

public interface IGameDataService
{
    Task<List<Item>> GetItemsAsync();
    Task<List<Enemy>> GetEnemiesAsync();
    Task<List<Skill>> GetSkillsAsync();
    Task<List<Level>> GetLevelsAsync();

    // Enemies CRUD
    Task<Enemy?> GetEnemyByIdAsync(string id);
    Task<Enemy> CreateEnemyAsync(Enemy enemy);
    Task<Enemy?> UpdateEnemyAsync(string id, Enemy enemy);
    Task<bool> DeleteEnemyAsync(string id);

    // Items CRUD
    Task<Item?> GetItemByIdAsync(int id);
    Task<Item> CreateItemAsync(Item item);
    Task<Item?> UpdateItemAsync(int id, Item item);
    Task<bool> DeleteItemAsync(int id);

    // Skills CRUD
    Task<Skill?> GetSkillByIdAsync(int id);
    Task<Skill> CreateSkillAsync(Skill skill);
    Task<Skill?> UpdateSkillAsync(int id, Skill skill);
    Task<bool> DeleteSkillAsync(int id);

    // Levels CRUD
    Task<Level?> GetLevelByNumberAsync(int levelNumber);
    Task<Level> CreateLevelAsync(Level level);
    Task<Level?> UpdateLevelAsync(int levelNumber, Level level);
    Task<bool> DeleteLevelAsync(int levelNumber);

    // SpawnPoints CRUD
    Task<List<SpawnPoint>> GetSpawnPointsAsync();
    Task<SpawnPoint?> GetSpawnPointByIdAsync(Guid id);
    Task<SpawnPoint> CreateSpawnPointAsync(SpawnPoint spawnPoint);
    Task<SpawnPoint?> UpdateSpawnPointAsync(Guid id, SpawnPoint spawnPoint);
    Task<bool> DeleteSpawnPointAsync(Guid id);

    // GameConfigs CRUD
    Task<List<GameConfig>> GetGameConfigsAsync();
    Task<GameConfig?> GetGameConfigByKeyAsync(string key);
    Task<GameConfig> CreateGameConfigAsync(GameConfig gameConfig);
    Task<GameConfig?> UpdateGameConfigAsync(string key, GameConfig gameConfig);
    Task<bool> DeleteGameConfigAsync(string key);

    // Bulk Import/Export
    Task<GameBulkData> ExportAllAsync();
    Task ImportAllAsync(GameBulkData data);
}
