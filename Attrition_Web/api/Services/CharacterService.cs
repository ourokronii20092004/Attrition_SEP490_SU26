namespace Attrition.API.Services;

using Attrition.API.Models;
using Attrition.API.Repositories;

public class CharacterService : ICharacterService
{
    private readonly IRepository<Character> _characterRepo;
    public CharacterService(IRepository<Character> characterRepo) => _characterRepo = characterRepo;

    public async Task<List<Character>> GetUserCharactersAsync(Guid userId)
    {
        var (characters, _) = await _characterRepo.GetPagedAsync(1, int.MaxValue, c => c.UserId == userId);
        return characters;
    }

    public async Task<Character?> CreateCharacterAsync(Guid userId, string name, string charClass)
    {
        var (existing, _) = await _characterRepo.GetPagedAsync(1, 1, c => c.UserId == userId && c.CharacterName == name);
        if (existing.Count > 0) return null;

        var character = new Character
        {
            UserId = userId,
            CharacterName = name,
            CharacterClass = charClass
        };
        await _characterRepo.AddAsync(character);
        return character;
    }

    public async Task<Character?> GetCharacterAsync(Guid id, Guid userId)
    {
        var (characters, _) = await _characterRepo.GetPagedAsync(
            1, 1,
            c => c.CharacterId == id && c.UserId == userId,
            null,
            c => c.Inventory,
            c => c.Skills
        );
        return characters.FirstOrDefault();
    }

    public async Task<bool> DeleteCharacterAsync(Guid id, Guid userId)
    {
        var ch = await GetCharacterAsync(id, userId);
        if (ch == null) return false;
        await _characterRepo.DeleteAsync(ch);
        return true;
    }
}