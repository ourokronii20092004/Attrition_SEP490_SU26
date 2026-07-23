using BuildingBlocks.Persistence;
using Wiki.Service.Models;

namespace Wiki.Service.Repositories.Interface;

public interface IWikiRepository : IRepository<WikiArticle>
{
    IRepository<WikiCategory> Categories { get; }
    IRepository<WikiRevision> Revisions { get; }
    IRepository<WikiContribution> Contributions { get; }
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
    Task ExecuteInTransactionAsync(Func<Task> action);

    Task<List<WikiArticle>> SearchAsync(string query, int limit);
    Task<List<WikiCategory>> GetCategoriesAsync();
    Task<WikiCategory?> GetCategoryBySlugAsync(string slug);
    Task<WikiCategory?> GetCategoryByIdAsync(int id);
    Task<int> CountArticlesInCategoryAsync(int categoryId);
    Task<Dictionary<int, int>> CountPublishedArticlesByCategoryAsync();
}
