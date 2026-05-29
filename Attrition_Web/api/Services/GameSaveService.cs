namespace Attrition.API.Services;

using Attrition.API.Models;
using Attrition.API.Repositories;

public class GameSaveService : IGameSaveService
{
    private readonly IRepository<GameSave> _saveRepo;
    private readonly IRepository<Character> _characterRepo;

    public GameSaveService(IRepository<GameSave> saveRepo, IRepository<Character> characterRepo)
    {
        _saveRepo = saveRepo;
        _characterRepo = characterRepo;
    }

    public async Task<List<GameSave>> GetSavesAsync(Guid characterId, Guid userId)
    {
        var owns = await _characterRepo.CountAsync(c => c.CharacterId == characterId && c.UserId == userId) > 0;
        if (!owns) return new List<GameSave>();
        var (saves, _) = await _saveRepo.GetPagedAsync(
            1, int.MaxValue,
            s => s.CharacterId == characterId,
            q => q.OrderByDescending(s => s.CreatedAt)
        );
        return saves;
    }

    public async Task<GameSave?> CreateSaveAsync(GameSave save, Guid userId)
    {
        var owns = await _characterRepo.CountAsync(c => c.CharacterId == save.CharacterId && c.UserId == userId) > 0;
        if (!owns) return null;
        await _saveRepo.AddAsync(save);
        return save;
    }

    public async Task<GameSave?> GetSaveAsync(Guid saveId, Guid userId)
    {
        var s = await _saveRepo.GetByIdAsync(saveId);
        if (s == null) return null;
        var owns = await _characterRepo.CountAsync(c => c.CharacterId == s.CharacterId && c.UserId == userId) > 0;
        return owns ? s : null;
    }

    public async Task<bool> DeleteSaveAsync(Guid saveId, Guid userId)
    {
        var s = await GetSaveAsync(saveId, userId);
        if (s == null) return false;
        await _saveRepo.DeleteAsync(s);
        return true;
    }

    public async Task<GameSave?> RenameSaveAsync(Guid saveId, string newName, Guid userId)
    {
        var s = await GetSaveAsync(saveId, userId);
        if (s == null) return null;
        s.SaveName = newName;
        await _saveRepo.UpdateAsync(s);
        return s;
    }
}