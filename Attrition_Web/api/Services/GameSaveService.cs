namespace Attrition.API.Services;

using Attrition.API.Data;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

public class GameSaveService
{
    private readonly AppDbContext _db;
    public GameSaveService(AppDbContext db) => _db = db;

    public async Task<List<GameSave>> GetSavesAsync(Guid characterId)
        => await _db.GameSaves.Where(s => s.CharacterId == characterId).OrderByDescending(s => s.CreatedAt).ToListAsync();

    public async Task<GameSave?> CreateSaveAsync(GameSave save)
    {
        _db.GameSaves.Add(save);
        await _db.SaveChangesAsync();
        return save;
    }

    public async Task<GameSave?> GetSaveAsync(Guid saveId)
        => await _db.GameSaves.FindAsync(saveId);

    public async Task<bool> DeleteSaveAsync(Guid saveId)
    {
        var s = await GetSaveAsync(saveId);
        if (s == null) return false;
        _db.GameSaves.Remove(s);
        await _db.SaveChangesAsync();
        return true;
    }
}