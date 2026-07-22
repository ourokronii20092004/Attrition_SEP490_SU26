using BuildingBlocks.Persistence;
using Forum.Service.Models;

namespace Forum.Service.Repositories;

public record UserReplyRow(Guid PostId, Guid ThreadId, string ThreadTitle, string Content, DateTime CreatedAt);

public interface IForumRepository : IRepository<ForumPost>
{
    Task<List<ForumCategory>> GetCategoriesAsync();
    Task<Dictionary<int, (int ThreadCount, DateTime? LatestActivity)>> GetCategoryStatsAsync();
    Task<ForumCategory?> GetCategoryBySlugAsync(string slug);
    Task<ForumCategory?> GetCategoryByIdAsync(int id);
    Task<List<ForumPost>> SearchThreadsAsync(string query, int limit);
    Task<ForumPost?> GetByWikiArticleIdAsync(Guid articleId);
    Task CreateRootAsync(ForumPost root, ThreadSubscription? subscription);
    Task DeleteThreadCascadeAsync(Guid rootId);
    Task DeletePostCascadeAsync(ForumPost post);
    Task IncrementReplyCountAsync(Guid rootId, DateTime lastReplyAt);
    Task<(Guid PostId, string Content)?> GetFirstPostAsync(Guid rootId);
    Task<(List<UserReplyRow> Items, int Total)> GetUserRepliesAsync(Guid userId, int page, int pageSize);
}
