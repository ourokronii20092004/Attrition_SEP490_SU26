using BuildingBlocks.Persistence;
using Enemy.Service.Models;

namespace Enemy.Service.Repositories;

public interface IEnemyRepository : IRepository<EnemyEntity>
{
    Task<EnemyEntity?> GetWithLootAsync(string enemyId);
    Task<List<EnemyEntity>> GetAllWithLootAsync(string? tier, string? search);
    Task<List<EnemyEntity>> SearchAsync(string query, int limit);
}
