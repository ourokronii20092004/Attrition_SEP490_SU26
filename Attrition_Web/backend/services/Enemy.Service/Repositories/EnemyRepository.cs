using BuildingBlocks.Persistence;
using Enemy.Service.Data;
using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Enemy.Service.Repositories;

public class EnemyRepository : Repository<EnemyEntity>, IEnemyRepository
{
    private readonly EnemyDbContext _context;

    public EnemyRepository(EnemyDbContext context) : base(context) => _context = context;

    public async Task<EnemyEntity?> GetWithLootAsync(string enemyId) =>
        await _context.Enemies
            .Include(e => e.LootTable)
            .FirstOrDefaultAsync(e => e.EnemyId == enemyId);

    public async Task<List<EnemyEntity>> GetAllWithLootAsync(string? tier, string? search)
    {
        var query = _context.Enemies.Include(e => e.LootTable).AsQueryable();

        if (!string.IsNullOrWhiteSpace(tier))
            query = query.Where(e => e.Tier == tier);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(s) || e.EnemyId.ToLower().Contains(s));
        }

        // Bound the public, unauthenticated result set to avoid a large materialization DoS.
        return await query.OrderBy(e => e.Name).Take(500).ToListAsync();
    }

    public async Task<List<EnemyEntity>> SearchAsync(string query, int limit)
    {
        var s = query.ToLower();
        return await _context.Enemies
            .Where(e => e.Name.ToLower().Contains(s) || e.EnemyId.ToLower().Contains(s))
            .OrderBy(e => e.Name)
            .Take(limit)
            .ToListAsync();
    }

    public Task SaveTrackedAsync() => _context.SaveChangesAsync();
}
