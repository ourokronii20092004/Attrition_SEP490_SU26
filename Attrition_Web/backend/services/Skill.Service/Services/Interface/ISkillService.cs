using BuildingBlocks.Contracts;
using Skill.Service.DTOs;

namespace Skill.Service.Services.Interface;

public interface ISkillService
{
    Task<List<SkillDto>> GetAllAsync();
    Task<SkillDto?> GetByIdAsync(string id);
    Task<ApiResponse<SkillDto>> UpdateAsync(string id, SkillUpdateRequest request);
    Task<ApiResponse<SkillImportResult>> ImportAsync(SkillImportRequest request);
    Task<SkillConfigBundle> GetConfigBundleAsync();
    Task<int> CountAsync();
}
