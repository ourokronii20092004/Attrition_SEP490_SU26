using BuildingBlocks.Persistence;
using Enemy.Service.Models;

namespace Enemy.Service.Repositories.Interface;

public interface IEnemyRepository : IRepository<EnemyEntity>
{
    Task<EnemyEntity?> GetWithLootAsync(string enemyId);
    Task<List<EnemyEntity>> GetAllWithLootAsync(string? tier, string? search);
    Task<List<EnemyEntity>> SearchAsync(string query, int limit);

    /// <summary>(MAX UpdatedAt, count) toàn bảng — dùng làm version cho game config bundle.</summary>
    Task<(DateTime? maxUpdatedAt, int count)> GetVersionInfoAsync();
    Task<(int Enemies, int Items)> GetStatsAsync();

    /// <summary>Toàn bộ enemy + loot (không filter, không Take) cho bundle game tải về.</summary>
    Task<List<EnemyEntity>> GetAllForBundleAsync();

    // Persists changes to an already-tracked enemy graph (including owned-loot add/remove),
    // letting EF change-tracking drive the diff instead of forcing the root to Modified.
    Task SaveTrackedAsync();
}
