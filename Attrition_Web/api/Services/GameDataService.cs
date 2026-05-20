namespace Attrition.API.Services;

using Attrition.API.Data;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

public class GameDataService
{
    private readonly AppDbContext _db;
    public GameDataService(AppDbContext db) => _db = db;

    public async Task<List<Item>> GetItemsAsync() => await _db.Items.ToListAsync();
    public async Task<List<Enemy>> GetEnemiesAsync() => await _db.Enemies.Include(e => e.LootTable).ToListAsync();
    public async Task<List<Skill>> GetSkillsAsync() => await _db.Skills.Include(s => s.Effects).ToListAsync();
    public async Task<List<Level>> GetLevelsAsync() => await _db.Levels.OrderBy(l => l.LevelNumber).ToListAsync();
}