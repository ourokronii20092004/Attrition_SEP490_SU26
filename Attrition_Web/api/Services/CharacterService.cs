namespace Attrition.API.Services;

using Attrition.API.Data;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

public class CharacterService : ICharacterService
{
    private readonly AppDbContext _db;
    public CharacterService(AppDbContext db) => _db = db;

    public async Task<List<Character>> GetUserCharactersAsync(Guid userId)
        => await _db.Characters.Where(c => c.UserId == userId).ToListAsync();

    public async Task<Character?> CreateCharacterAsync(Guid userId, string name, string charClass)
    {
        if (await _db.Characters.AnyAsync(c => c.UserId == userId && c.CharacterName == name)) return null;

        var character = new Character
        {
            UserId = userId,
            CharacterName = name,
            CharacterClass = charClass
        };
        _db.Characters.Add(character);
        await _db.SaveChangesAsync();
        return character;
    }

    public async Task<Character?> GetCharacterAsync(Guid id, Guid userId)
        => await _db.Characters.Include(c => c.Inventory).Include(c => c.Skills).FirstOrDefaultAsync(c => c.CharacterId == id && c.UserId == userId);

    public async Task<bool> DeleteCharacterAsync(Guid id, Guid userId)
    {
        var ch = await GetCharacterAsync(id, userId);
        if (ch == null) return false;
        _db.Characters.Remove(ch);
        await _db.SaveChangesAsync();
        return true;
    }
}