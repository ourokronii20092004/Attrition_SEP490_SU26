using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;

namespace Enemy.Service.Services;

public interface ISkillService
{
    Task<List<SkillResponse>> GetAllAsync();
    Task<SkillResponse?> GetByIdAsync(string skillId);
    Task<ApiResponse<SkillResponse>> UpdateAsync(string skillId, SkillConfigDto request);
    Task<SkillConfigBundle> GetConfigBundleAsync();
    Task<(string version, int count)> GetVersionInfoAsync();
}
