using BuildingBlocks.Persistence;
using Forum.Service.Data;
using Forum.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Forum.Service.Repositories;

public class ForumRepository : Repository<ForumThread>, IForumRepository
{
    private readonly ForumDbContext _context;

    public ForumRepository(ForumDbContext context) : base(context) => _context = context;

    public async Task<List<ForumCategory>> GetCategoriesAsync() =>
        await _context.ForumCategories.OrderBy(c => c.SortOrder).ToListAsync();

    public async Task<ForumCategory?> GetCategoryBySlugAsync(string slug) =>
        await _context.ForumCategories.FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<ForumCategory?> GetCategoryByIdAsync(int id) =>
        await _context.ForumCategories.FindAsync(id);

    public async Task<List<ForumThread>> SearchThreadsAsync(string query, int limit)
    {
        var s = query.ToLower();
        return await _context.ForumThreads
            .Where(t => t.Title.ToLower().Contains(s))
            .OrderByDescending(t => t.LastReplyAt)
            .Take(limit)
            .ToListAsync();
    }
}
