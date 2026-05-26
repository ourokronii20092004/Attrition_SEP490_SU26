using Attrition.API.Models;

namespace Attrition.API.Services;

public interface IGameSaveService
{
    Task<List<GameSave>> GetSavesAsync(Guid characterId, Guid userId);
    Task<GameSave?> CreateSaveAsync(GameSave save, Guid userId);
    Task<GameSave?> GetSaveAsync(Guid saveId, Guid userId);
    Task<bool> DeleteSaveAsync(Guid saveId, Guid userId);
    Task<GameSave?> RenameSaveAsync(Guid saveId, string newName, Guid userId);
}
