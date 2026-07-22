using BuildingBlocks.Persistence;
using Assets.Service.Models;

namespace Assets.Service.Repositories.Interface;

public interface IAssetRepository : IRepository<Asset>
{
    Task<Asset?> GetBySourceAsync(string sourceType, string sourceId);
    Task AddTrackedAsync(Asset asset);
    void Detach(Asset asset);
    Task SaveAsync();
}
