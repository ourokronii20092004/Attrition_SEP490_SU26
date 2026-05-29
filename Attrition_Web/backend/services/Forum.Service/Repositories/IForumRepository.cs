using BuildingBlocks.Persistence;
using Forum.Service.Models;

namespace Forum.Service.Repositories;

public interface IForumRepository : IRepository<ForumThread>
{
    Task<List<ForumCategory>> GetCategoriesAsync();
    Task<ForumCategory?> GetCategoryBySlugAsync(string slug);
    Task<ForumCategory?> GetCategoryByIdAsync(int id);
    Task<List<ForumThread>> SearchThreadsAsync(string query, int limit);
}
