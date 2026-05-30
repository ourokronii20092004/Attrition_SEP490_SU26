using BuildingBlocks.Persistence;
using Wiki.Service.Models;

namespace Wiki.Service.Repositories;

public interface IWikiRepository : IRepository<WikiArticle>
{
    Task<List<WikiArticle>> SearchAsync(string query, int limit);
    Task<List<WikiCategory>> GetCategoriesAsync();
    Task<WikiCategory?> GetCategoryBySlugAsync(string slug);
    Task<WikiCategory?> GetCategoryByIdAsync(int id);
    Task<int> CountArticlesInCategoryAsync(int categoryId);
}
