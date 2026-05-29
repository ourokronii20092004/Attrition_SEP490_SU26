using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;

namespace Enemy.Service.Services;

public interface IEnemyService
{
    Task<List<EnemyResponse>> GetAllAsync(string? tier, string? search);
    Task<EnemyResponse?> GetByIdAsync(string enemyId);
    Task<ApiResponse<EnemyResponse>> CreateAsync(EnemyCreateRequest request);
    Task<ApiResponse<EnemyResponse>> UpdateAsync(string enemyId, EnemyUpdateRequest request);
    Task<ApiResponse> DeleteAsync(string enemyId);
    Task<List<EnemySummaryDto>> SearchAsync(string query, int limit);
    Task<int> CountAsync();
}
