using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;

namespace Enemy.Service.Services;

public class GameDataImportService(IGameDataImportRepository repository, ICacheService cache) : IGameDataImportService
{
    public async Task<ApiResponse<GameDataImportResult>> ImportAsync(GameDataImportRequest request)
    {
        var result = await repository.ImportAsync(request);
        await cache.RemoveByPrefixAsync("list:");
        await cache.RemoveAsync("bundle:all");
        await cache.RemoveByPrefixAsync("item-list:");
        await cache.RemoveAsync("item-bundle:all");
        return ApiResponse<GameDataImportResult>.Ok(result);
    }
}
