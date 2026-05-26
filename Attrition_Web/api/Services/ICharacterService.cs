using Attrition.API.Models;

namespace Attrition.API.Services;

public interface ICharacterService
{
    Task<List<Character>> GetUserCharactersAsync(Guid userId);
    Task<Character?> CreateCharacterAsync(Guid userId, string name, string charClass);
    Task<Character?> GetCharacterAsync(Guid id, Guid userId);
    Task<bool> DeleteCharacterAsync(Guid id, Guid userId);
}
