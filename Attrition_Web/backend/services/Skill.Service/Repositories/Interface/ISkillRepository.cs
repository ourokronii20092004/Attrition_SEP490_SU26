using Skill.Service.Models;

namespace Skill.Service.Repositories.Interface;

public interface ISkillRepository
{
    Task<List<SkillEntity>> GetAllAsync(bool orderById = false);

    Task<SkillEntity?> GetByIdAsync(string id, bool tracked = false);

    Task<Dictionary<string, SkillEntity>> GetByIdsAsync(IEnumerable<string> ids);

    void Add(SkillEntity skill);

    Task SaveChangesAsync();

    Task<(DateTime? MaxUpdatedAt, int Count)> GetVersionInfoAsync();
}