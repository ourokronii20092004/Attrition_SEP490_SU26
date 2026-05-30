using BuildingBlocks.Persistence;
using Enemy.Service.Models;

namespace Enemy.Service.Repositories;

public interface IEnemyRepository : IRepository<EnemyEntity>
{
    Task<EnemyEntity?> GetWithLootAsync(string enemyId);
    Task<List<EnemyEntity>> GetAllWithLootAsync(string? tier, string? search);
    Task<List<EnemyEntity>> SearchAsync(string query, int limit);

    // Persists changes to an already-tracked enemy graph (including owned-loot add/remove),
    // letting EF change-tracking drive the diff instead of forcing the root to Modified.
    Task SaveTrackedAsync();
}
