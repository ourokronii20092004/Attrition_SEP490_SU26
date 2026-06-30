using BuildingBlocks.Persistence;
using Enemy.Service.Data;
using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Enemy.Service.Repositories;

public class ItemRepository : Repository<ItemEntity>, IItemRepository
{
    private readonly EnemyDbContext _context;

    public ItemRepository(EnemyDbContext context) : base(context) => _context = context;

    public async Task<ItemEntity?> GetWithModifiersAsync(string itemId) =>
        await _context.Items
            .Include(i => i.Modifiers)
            .FirstOrDefaultAsync(i => i.ItemId == itemId);

    public async Task<List<ItemEntity>> GetAllWithModifiersAsync(string? category, string? search)
    {
        var query = _context.Items.Include(i => i.Modifiers).AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(i => i.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s) || i.ItemId.ToLower().Contains(s));
        }

        // Bound the public, unauthenticated result set.
        return await query.OrderBy(i => i.Name).Take(500).ToListAsync();
    }

    public async Task<List<ItemEntity>> GetAllForBundleAsync() =>
        await _context.Items.Include(i => i.Modifiers).OrderBy(i => i.ItemId).ToListAsync();

    public async Task<(DateTime? maxUpdatedAt, int count)> GetVersionInfoAsync()
    {
        var count = await _context.Items.CountAsync();
        if (count == 0) return (null, 0);
        var max = await _context.Items.MaxAsync(i => (DateTime?)i.UpdatedAt);
        return (max, count);
    }

    public Task SaveTrackedAsync() => _context.SaveChangesAsync();
}
