using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Wiki.Service.Data;
using Wiki.Service.Models;

namespace Wiki.Service.Repositories;

public class WikiRepository : Repository<WikiArticle>, IWikiRepository
{
    private readonly WikiDbContext _context;

    public WikiRepository(WikiDbContext context) : base(context) => _context = context;

    public async Task<List<WikiArticle>> SearchAsync(string query, int limit)
    {
        var s = query.ToLower();
        return await _context.WikiArticles
            .Where(a => a.Status == ArticleStatus.Published && a.Title.ToLower().Contains(s))
            .OrderByDescending(a => a.UpdatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<WikiCategory>> GetCategoriesAsync() =>
        await _context.WikiCategories.OrderBy(c => c.SortOrder).ToListAsync();

    public async Task<WikiCategory?> GetCategoryBySlugAsync(string slug) =>
        await _context.WikiCategories.FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<WikiCategory?> GetCategoryByIdAsync(int id) =>
        await _context.WikiCategories.FindAsync(id);

    public async Task<int> CountArticlesInCategoryAsync(int categoryId) =>
        await _context.WikiArticles.CountAsync(a => a.CategoryId == categoryId && a.Status == ArticleStatus.Published);

    public async Task<Dictionary<int, int>> CountPublishedArticlesByCategoryAsync() =>
        await _context.WikiArticles
            .Where(a => a.Status == ArticleStatus.Published)
            .GroupBy(a => a.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
}
