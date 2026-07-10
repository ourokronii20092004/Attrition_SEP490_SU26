using BuildingBlocks.Persistence;
using Forum.Service.Models;

namespace Forum.Service.Repositories;

/// <summary>A user's reply row (post + its thread's title), projected for the profile Replies tab.</summary>
public record UserReplyRow(Guid PostId, Guid ThreadId, string ThreadTitle, string Content, DateTime CreatedAt);

public interface IForumRepository : IRepository<ForumThread>
{
    Task<List<ForumCategory>> GetCategoriesAsync();
    Task<Dictionary<int, (int ThreadCount, DateTime? LatestActivity)>> GetCategoryStatsAsync();
    Task<ForumCategory?> GetCategoryBySlugAsync(string slug);
    Task<ForumCategory?> GetCategoryByIdAsync(int id);
    Task<List<ForumThread>> SearchThreadsAsync(string query, int limit);

    Task CreateThreadWithFirstPostAsync(ForumThread thread, ForumPost firstPost, ThreadSubscription subscription);
    Task<ForumThread?> GetByWikiArticleIdAsync(Guid articleId);
    Task DeleteThreadCascadeAsync(Guid threadId);
    Task DeletePostCascadeAsync(ForumPost post);
    Task IncrementReplyCountAsync(Guid threadId, DateTime lastReplyAt);
    Task<(Guid PostId, string Content)?> GetFirstPostAsync(Guid threadId);

    // A user's replies (their non-removed posts that aren't a thread's opening post), newest first.
    Task<(List<UserReplyRow> Items, int Total)> GetUserRepliesAsync(Guid userId, int page, int pageSize);
}
