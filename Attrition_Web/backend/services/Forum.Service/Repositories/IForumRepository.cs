using BuildingBlocks.Persistence;
using Forum.Service.Models;

namespace Forum.Service.Repositories;

public interface IForumRepository : IRepository<ForumThread>
{
    Task<List<ForumCategory>> GetCategoriesAsync();
    Task<ForumCategory?> GetCategoryBySlugAsync(string slug);
    Task<ForumCategory?> GetCategoryByIdAsync(int id);
    Task<List<ForumThread>> SearchThreadsAsync(string query, int limit);

    Task CreateThreadWithFirstPostAsync(ForumThread thread, ForumPost firstPost, ThreadSubscription subscription);
    Task DeleteThreadCascadeAsync(Guid threadId);
    Task DeletePostCascadeAsync(ForumPost post);
    Task IncrementReplyCountAsync(Guid threadId, DateTime lastReplyAt);
    Task<string?> GetFirstPostSnippetAsync(Guid threadId);
}
