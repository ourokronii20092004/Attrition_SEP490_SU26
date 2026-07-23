using Microsoft.EntityFrameworkCore;
using Skill.Service.Data;
using Skill.Service.Models;

namespace Skill.Service.Repositories;

public class SkillRepository(SkillDbContext db) : ISkillRepository
{
    public Task<List<SkillEntity>> GetAllAsync(bool orderById = false) =>
        (orderById ? db.Skills.AsNoTracking().OrderBy(x => x.SkillId) : db.Skills.AsNoTracking().OrderBy(x => x.Name))
            .Take(500).ToListAsync();
    public Task<SkillEntity?> GetByIdAsync(string id, bool tracked = false) =>
        (tracked ? db.Skills : db.Skills.AsNoTracking()).FirstOrDefaultAsync(x => x.SkillId == id);
    public Task<Dictionary<string, SkillEntity>> GetByIdsAsync(IEnumerable<string> ids) =>
        db.Skills.Where(x => ids.Contains(x.SkillId)).ToDictionaryAsync(x => x.SkillId, StringComparer.Ordinal);
    public void Add(SkillEntity skill) => db.Skills.Add(skill);
    public Task SaveChangesAsync() => db.SaveChangesAsync();
    public async Task<(DateTime? MaxUpdatedAt, int Count)> GetVersionInfoAsync()
    {
        var count = await db.Skills.CountAsync();
        return (count == 0 ? null : await db.Skills.MaxAsync(x => (DateTime?)x.UpdatedAt), count);
    }
}
