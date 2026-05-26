namespace Attrition.API.Services;

using Attrition.API.Data;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

public class GameSaveService : IGameSaveService
{
    private readonly AppDbContext _db;
    public GameSaveService(AppDbContext db) => _db = db;

    public async Task<List<GameSave>> GetSavesAsync(Guid characterId, Guid userId)
    {
        var owns = await _db.Characters.AnyAsync(c => c.CharacterId == characterId && c.UserId == userId);
        if (!owns) return new List<GameSave>();
        return await _db.GameSaves.Where(s => s.CharacterId == characterId).OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<GameSave?> CreateSaveAsync(GameSave save, Guid userId)
    {
        var owns = await _db.Characters.AnyAsync(c => c.CharacterId == save.CharacterId && c.UserId == userId);
        if (!owns) return null;
        _db.GameSaves.Add(save);
        await _db.SaveChangesAsync();
        return save;
    }

    public async Task<GameSave?> GetSaveAsync(Guid saveId, Guid userId)
    {
        var s = await _db.GameSaves.FindAsync(saveId);
        if (s == null) return null;
        var owns = await _db.Characters.AnyAsync(c => c.CharacterId == s.CharacterId && c.UserId == userId);
        return owns ? s : null;
    }

    public async Task<bool> DeleteSaveAsync(Guid saveId, Guid userId)
    {
        var s = await GetSaveAsync(saveId, userId);
        if (s == null) return false;
        _db.GameSaves.Remove(s);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<GameSave?> RenameSaveAsync(Guid saveId, string newName, Guid userId)
    {
        var s = await GetSaveAsync(saveId, userId);
        if (s == null) return null;
        s.SaveName = newName;
        await _db.SaveChangesAsync();
        return s;
    }
}