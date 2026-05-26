using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/game/data")]
public class GameDataController : ControllerBase
{
    private readonly IGameDataService _service;
    public GameDataController(IGameDataService service) => _service = service;

    // --- Items ---
    [HttpGet("items")]
    public async Task<IActionResult> GetItems() 
        => Ok(new ApiResponse<List<Item>>(true, await _service.GetItemsAsync()));

    [HttpGet("items/{id:int}")]
    public async Task<IActionResult> GetItem(int id)
    {
        var item = await _service.GetItemByIdAsync(id);
        if (item == null) return NotFound(new ApiResponse(false, "Item not found."));
        return Ok(new ApiResponse<Item>(true, item));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest req)
    {
        Item item = req.ItemType.ToLower() == "gear" ? new Gear
        {
            EquipSlot = req.EquipSlot ?? string.Empty,
            BonusAd = req.BonusAd ?? 0,
            BonusAp = req.BonusAp ?? 0,
            BonusDef = req.BonusDef ?? 0,
            BonusRes = req.BonusRes ?? 0,
            BonusAttackSpeed = req.BonusAttackSpeed ?? 0f,
            BonusMaxHp = req.BonusMaxHp ?? 0,
            BonusMaxMana = req.BonusMaxMana ?? 0,
            RequiredLevel = req.RequiredLevel ?? 1,
            Name = req.Name,
            ItemType = req.ItemType,
            Rarity = req.Rarity,
            StackLimit = req.StackLimit,
            Description = req.Description,
            IconKey = req.IconKey
        } : new Item
        {
            Name = req.Name,
            ItemType = req.ItemType,
            Rarity = req.Rarity,
            StackLimit = req.StackLimit,
            Description = req.Description,
            IconKey = req.IconKey
        };
        return Ok(new ApiResponse<Item>(true, await _service.CreateItemAsync(item)));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] CreateItemRequest req)
    {
        Item item = req.ItemType.ToLower() == "gear" ? new Gear
        {
            EquipSlot = req.EquipSlot ?? string.Empty,
            BonusAd = req.BonusAd ?? 0,
            BonusAp = req.BonusAp ?? 0,
            BonusDef = req.BonusDef ?? 0,
            BonusRes = req.BonusRes ?? 0,
            BonusAttackSpeed = req.BonusAttackSpeed ?? 0f,
            BonusMaxHp = req.BonusMaxHp ?? 0,
            BonusMaxMana = req.BonusMaxMana ?? 0,
            RequiredLevel = req.RequiredLevel ?? 1,
            Name = req.Name,
            ItemType = req.ItemType,
            Rarity = req.Rarity,
            StackLimit = req.StackLimit,
            Description = req.Description,
            IconKey = req.IconKey
        } : new Item
        {
            Name = req.Name,
            ItemType = req.ItemType,
            Rarity = req.Rarity,
            StackLimit = req.StackLimit,
            Description = req.Description,
            IconKey = req.IconKey
        };
        var result = await _service.UpdateItemAsync(id, item);
        if (result == null) return NotFound(new ApiResponse(false, "Item not found."));
        return Ok(new ApiResponse<Item>(true, result));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var result = await _service.DeleteItemAsync(id);
        if (!result) return NotFound(new ApiResponse(false, "Item not found."));
        return Ok(new ApiResponse(true));
    }

    // --- Enemies ---
    [HttpGet("enemies")]
    public async Task<IActionResult> GetEnemies() 
        => Ok(new ApiResponse<List<Enemy>>(true, await _service.GetEnemiesAsync()));

    [HttpGet("enemies/{id}")]
    public async Task<IActionResult> GetEnemy(string id)
    {
        var enemy = await _service.GetEnemyByIdAsync(id);
        if (enemy == null) return NotFound(new ApiResponse(false, "Enemy not found."));
        return Ok(new ApiResponse<Enemy>(true, enemy));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPost("enemies")]
    public async Task<IActionResult> CreateEnemy([FromBody] EnemyCreateRequest req)
    {
        var enemy = new Enemy
        {
            EnemyId = req.EnemyId,
            Name = req.Name,
            Tier = req.Tier,
            SpawnBiome = req.SpawnBiome,
            Hp = req.Hp,
            Ad = req.Ad,
            Ap = req.Ap,
            Def = req.Def,
            Res = req.Res,
            AttackSpeed = req.AttackSpeed,
            IsRanged = req.IsRanged,
            ExpReward = req.ExpReward,
            GoldReward = req.GoldReward
        };
        return Ok(new ApiResponse<Enemy>(true, await _service.CreateEnemyAsync(enemy)));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPut("enemies/{id}")]
    public async Task<IActionResult> UpdateEnemy(string id, [FromBody] EnemyUpdateRequest req)
    {
        var enemy = new Enemy
        {
            Name = req.Name,
            Tier = req.Tier,
            SpawnBiome = req.SpawnBiome,
            Hp = req.Hp,
            Ad = req.Ad,
            Ap = req.Ap,
            Def = req.Def,
            Res = req.Res,
            AttackSpeed = req.AttackSpeed,
            IsRanged = req.IsRanged,
            ExpReward = req.ExpReward,
            GoldReward = req.GoldReward
        };
        var result = await _service.UpdateEnemyAsync(id, enemy);
        if (result == null) return NotFound(new ApiResponse(false, "Enemy not found."));
        return Ok(new ApiResponse<Enemy>(true, result));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpDelete("enemies/{id}")]
    public async Task<IActionResult> DeleteEnemy(string id)
    {
        var result = await _service.DeleteEnemyAsync(id);
        if (!result) return NotFound(new ApiResponse(false, "Enemy not found."));
        return Ok(new ApiResponse(true));
    }

    // --- Skills ---
    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills() 
        => Ok(new ApiResponse<List<Skill>>(true, await _service.GetSkillsAsync()));

    [HttpGet("skills/{id:int}")]
    public async Task<IActionResult> GetSkill(int id)
    {
        var skill = await _service.GetSkillByIdAsync(id);
        if (skill == null) return NotFound(new ApiResponse(false, "Skill not found."));
        return Ok(new ApiResponse<Skill>(true, skill));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPost("skills")]
    public async Task<IActionResult> CreateSkill([FromBody] CreateSkillRequest req)
    {
        var skill = new Skill
        {
            Name = req.Name,
            SkillType = req.SkillType,
            DamageType = req.DamageType,
            ManaCost = req.ManaCost,
            StaminaCost = req.StaminaCost,
            CooldownSec = req.CooldownSec,
            BaseDamage = req.BaseDamage,
            Description = req.Description,
            IconKey = req.IconKey,
            RequiredLevel = req.RequiredLevel
        };
        return Ok(new ApiResponse<Skill>(true, await _service.CreateSkillAsync(skill)));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPut("skills/{id:int}")]
    public async Task<IActionResult> UpdateSkill(int id, [FromBody] CreateSkillRequest req)
    {
        var skill = new Skill
        {
            Name = req.Name,
            SkillType = req.SkillType,
            DamageType = req.DamageType,
            ManaCost = req.ManaCost,
            StaminaCost = req.StaminaCost,
            CooldownSec = req.CooldownSec,
            BaseDamage = req.BaseDamage,
            Description = req.Description,
            IconKey = req.IconKey,
            RequiredLevel = req.RequiredLevel
        };
        var result = await _service.UpdateSkillAsync(id, skill);
        if (result == null) return NotFound(new ApiResponse(false, "Skill not found."));
        return Ok(new ApiResponse<Skill>(true, result));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpDelete("skills/{id:int}")]
    public async Task<IActionResult> DeleteSkill(int id)
    {
        var result = await _service.DeleteSkillAsync(id);
        if (!result) return NotFound(new ApiResponse(false, "Skill not found."));
        return Ok(new ApiResponse(true));
    }

    // --- Levels ---
    [HttpGet("levels")]
    public async Task<IActionResult> GetLevels() 
        => Ok(new ApiResponse<List<Level>>(true, await _service.GetLevelsAsync()));

    [HttpGet("levels/{levelNumber:int}")]
    public async Task<IActionResult> GetLevel(int levelNumber)
    {
        var level = await _service.GetLevelByNumberAsync(levelNumber);
        if (level == null) return NotFound(new ApiResponse(false, "Level not found."));
        return Ok(new ApiResponse<Level>(true, level));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPost("levels")]
    public async Task<IActionResult> CreateLevel([FromBody] CreateLevelRequest req)
    {
        var level = new Level
        {
            LevelNumber = req.LevelNumber,
            ExpRequired = req.ExpRequired,
            HpGrowth = req.HpGrowth,
            AdGrowth = req.AdGrowth,
            ApGrowth = req.ApGrowth,
            DefGrowth = req.DefGrowth,
            ResGrowth = req.ResGrowth,
            SpeedGrowth = req.SpeedGrowth
        };
        return Ok(new ApiResponse<Level>(true, await _service.CreateLevelAsync(level)));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPut("levels/{levelNumber:int}")]
    public async Task<IActionResult> UpdateLevel(int levelNumber, [FromBody] CreateLevelRequest req)
    {
        var level = new Level
        {
            ExpRequired = req.ExpRequired,
            HpGrowth = req.HpGrowth,
            AdGrowth = req.AdGrowth,
            ApGrowth = req.ApGrowth,
            DefGrowth = req.DefGrowth,
            ResGrowth = req.ResGrowth,
            SpeedGrowth = req.SpeedGrowth
        };
        var result = await _service.UpdateLevelAsync(levelNumber, level);
        if (result == null) return NotFound(new ApiResponse(false, "Level not found."));
        return Ok(new ApiResponse<Level>(true, result));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpDelete("levels/{levelNumber:int}")]
    public async Task<IActionResult> DeleteLevel(int levelNumber)
    {
        var result = await _service.DeleteLevelAsync(levelNumber);
        if (!result) return NotFound(new ApiResponse(false, "Level not found."));
        return Ok(new ApiResponse(true));
    }

    // --- SpawnPoints ---
    [AllowAnonymous]
    [HttpGet("spawnpoints")]
    public async Task<IActionResult> GetSpawnPoints()
        => Ok(new ApiResponse<List<SpawnPoint>>(true, await _service.GetSpawnPointsAsync()));

    [AllowAnonymous]
    [HttpGet("spawnpoints/{id:guid}")]
    public async Task<IActionResult> GetSpawnPoint(Guid id)
    {
        var sp = await _service.GetSpawnPointByIdAsync(id);
        if (sp == null) return NotFound(new ApiResponse(false, "Spawn point not found."));
        return Ok(new ApiResponse<SpawnPoint>(true, sp));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPost("spawnpoints")]
    public async Task<IActionResult> CreateSpawnPoint([FromBody] CreateSpawnPointRequest req)
    {
        var spawnPoint = new SpawnPoint
        {
            EnemyId = req.EnemyId,
            SceneName = req.SceneName,
            PositionX = req.PositionX,
            PositionY = req.PositionY,
            PositionZ = req.PositionZ,
            SpawnIntervalSeconds = req.SpawnIntervalSeconds,
            MaxActiveCount = req.MaxActiveCount
        };
        return Ok(new ApiResponse<SpawnPoint>(true, await _service.CreateSpawnPointAsync(spawnPoint)));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPut("spawnpoints/{id:guid}")]
    public async Task<IActionResult> UpdateSpawnPoint(Guid id, [FromBody] CreateSpawnPointRequest req)
    {
        var spawnPoint = new SpawnPoint
        {
            EnemyId = req.EnemyId,
            SceneName = req.SceneName,
            PositionX = req.PositionX,
            PositionY = req.PositionY,
            PositionZ = req.PositionZ,
            SpawnIntervalSeconds = req.SpawnIntervalSeconds,
            MaxActiveCount = req.MaxActiveCount
        };
        var result = await _service.UpdateSpawnPointAsync(id, spawnPoint);
        if (result == null) return NotFound(new ApiResponse(false, "Spawn point not found."));
        return Ok(new ApiResponse<SpawnPoint>(true, result));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpDelete("spawnpoints/{id:guid}")]
    public async Task<IActionResult> DeleteSpawnPoint(Guid id)
    {
        var result = await _service.DeleteSpawnPointAsync(id);
        if (!result) return NotFound(new ApiResponse(false, "Spawn point not found."));
        return Ok(new ApiResponse(true));
    }

    // --- GameConfigs ---
    [Authorize(Roles = "Admin,GameServer")]
    [HttpGet("configs")]
    public async Task<IActionResult> GetGameConfigs()
        => Ok(new ApiResponse<List<GameConfig>>(true, await _service.GetGameConfigsAsync()));

    [Authorize(Roles = "Admin,GameServer")]
    [HttpGet("configs/{key}")]
    public async Task<IActionResult> GetGameConfig(string key)
    {
        var gc = await _service.GetGameConfigByKeyAsync(key);
        if (gc == null) return NotFound(new ApiResponse(false, "Config not found."));
        return Ok(new ApiResponse<GameConfig>(true, gc));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPost("configs")]
    public async Task<IActionResult> CreateGameConfig([FromBody] UpsertGameConfigRequest req)
    {
        var gameConfig = new GameConfig
        {
            Key = req.Key,
            Value = req.Value,
            Description = req.Description
        };
        return Ok(new ApiResponse<GameConfig>(true, await _service.CreateGameConfigAsync(gameConfig)));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpPut("configs/{key}")]
    public async Task<IActionResult> UpdateGameConfig(string key, [FromBody] UpsertGameConfigRequest req)
    {
        var gameConfig = new GameConfig
        {
            Key = req.Key,
            Value = req.Value,
            Description = req.Description
        };
        var result = await _service.UpdateGameConfigAsync(key, gameConfig);
        if (result == null) return NotFound(new ApiResponse(false, "Config not found."));
        return Ok(new ApiResponse<GameConfig>(true, result));
    }

    [Authorize(Roles = "Admin,GameServer")]
    [HttpDelete("configs/{key}")]
    public async Task<IActionResult> DeleteGameConfig(string key)
    {
        var result = await _service.DeleteGameConfigAsync(key);
        if (!result) return NotFound(new ApiResponse(false, "Config not found."));
        return Ok(new ApiResponse(true));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("export")]
    public async Task<IActionResult> ExportAll()
        => Ok(new ApiResponse<GameBulkData>(true, await _service.ExportAllAsync()));

    [Authorize(Roles = "Admin")]
    [HttpPost("import")]
    public async Task<IActionResult> ImportAll([FromBody] GameBulkData data, [FromServices] Attrition.API.Data.AppDbContext dbContext)
    {
        using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await _service.ImportAllAsync(data);
            await transaction.CommitAsync();
            return Ok(new ApiResponse(true));
        }
        catch (System.Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new ApiResponse(false, Error: "Import failed: " + ex.Message));
        }
    }
}