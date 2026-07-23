using BuildingBlocks.Persistence;
using Enemy.Service.Models;

namespace Enemy.Service.Repositories.Interface;

public interface IItemRepository : IRepository<ItemEntity>
{
    Task<ItemEntity?> GetWithModifiersAsync(string itemId);
    Task<List<ItemEntity>> GetAllWithModifiersAsync(string? category, string? search);
    Task<List<ItemEntity>> GetAllForBundleAsync();

    /// <summary>(MAX UpdatedAt, count) toàn bảng item — version cho game config bundle.</summary>
    Task<(DateTime? maxUpdatedAt, int count)> GetVersionInfoAsync();

    Task SaveTrackedAsync();
}
