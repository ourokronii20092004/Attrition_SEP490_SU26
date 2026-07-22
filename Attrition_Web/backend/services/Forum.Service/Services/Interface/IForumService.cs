using BuildingBlocks.Contracts;
using Forum.Service.DTOs;

namespace Forum.Service.Services.Interface;

/// <summary>Caller identity snapshot, passed from the controller (denormalized at write time).</summary>
public record Author(Guid Id, string Name, string? Avatar, string Role);

public interface IForumService
{
    Task<List<ForumCategoryDto>> GetCategoriesAsync();
    Task<PaginatedResponse<ForumThreadListDto>> GetThreadsAsync(string? categorySlug, string? search, int page, int pageSize, Guid? authorId = null);
    Task<ForumThreadDto?> GetThreadAsync(Guid threadId, Guid? currentUserId = null);
    // QOLF-3b: resolve (creating if needed) the comment thread for a wiki article.
    Task<ApiResponse<ForumThreadDto>> GetOrCreateWikiThreadAsync(Guid articleId, string articleTitle);
    Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId);
    // A user's forum replies for their public profile (excludes thread-opening posts).
    Task<PaginatedResponse<UserReplyDto>> GetUserRepliesAsync(Guid userId, int page, int pageSize);

    Task<ApiResponse<Guid>> CreateThreadAsync(CreateThreadRequest request, Author author);
    Task<ApiResponse<ForumPostDto>> CreatePostAsync(Guid threadId, CreatePostRequest request, Author author);
    Task<ApiResponse> UpdatePostAsync(Guid postId, UpdatePostRequest request, Guid userId);
    Task<ApiResponse> DeletePostAsync(Guid postId, Guid userId, bool isAdmin);
    Task<ApiResponse> ToggleReactionAsync(Guid postId, Guid userId, ReactRequest request);
    Task<ApiResponse> SavePostAttachmentsAsync(Guid postId, List<string> urls, Guid userId);
    Task<ApiResponse> ToggleThreadSubscriptionAsync(Guid threadId, Guid userId);
    Task<ApiResponse> ReportPostAsync(Guid postId, string reason, Author reporter);

    // Moderation
    Task<ApiResponse> TogglePinAsync(Guid threadId);
    Task<ApiResponse> ToggleLockAsync(Guid threadId);
    Task<ApiResponse> DeleteThreadAsync(Guid threadId);
    Task<ApiResponse> RemovePostAsync(Guid postId, Author moderator, string reason);
    Task<ApiResponse> RestorePostAsync(Guid postId);
    Task<PaginatedResponse<AdminForumThreadDto>> ListThreadsForModerationAsync(int page, int pageSize);
    Task<PaginatedResponse<AdminForumPostDto>> ListPostsForModerationAsync(bool removedOnly, string? search, int page, int pageSize);
    Task<PaginatedResponse<AdminPostReportDto>> ListReportsAsync(string status, int page, int pageSize);
    Task<ApiResponse> DismissReportAsync(Guid reportId);
    Task<ApiResponse> ResolveReportAsync(Guid reportId);

    // Category management (admin)
    Task<ApiResponse<int>> CreateCategoryAsync(ForumCategoryRequest request);
    Task<ApiResponse> UpdateCategoryAsync(int id, ForumCategoryRequest request);
    Task<ApiResponse> DeleteCategoryAsync(int id);

    // Aggregator support
    Task<List<ForumPostSearchDto>> SearchAsync(string query, int limit);
    Task<(int Threads, int Posts, int RemovedPosts)> GetStatsAsync();
}
