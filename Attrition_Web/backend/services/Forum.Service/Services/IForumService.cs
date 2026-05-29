using BuildingBlocks.Contracts;
using Forum.Service.DTOs;

namespace Forum.Service.Services;

/// <summary>Caller identity snapshot, passed from the controller (denormalized at write time).</summary>
public record Author(Guid Id, string Name, string? Avatar, string Role);

public interface IForumService
{
    Task<List<ForumCategoryDto>> GetCategoriesAsync();
    Task<PaginatedResponse<ForumThreadListDto>> GetThreadsAsync(string? categorySlug, string? search, int page, int pageSize);
    Task<ForumThreadDto?> GetThreadAsync(Guid threadId);
    Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId);

    Task<ApiResponse<Guid>> CreateThreadAsync(CreateThreadRequest request, Author author);
    Task<ApiResponse> CreatePostAsync(Guid threadId, CreatePostRequest request, Author author);
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
    Task<List<AdminForumThreadDto>> ListThreadsForModerationAsync();
    Task<List<AdminForumPostDto>> ListPostsForModerationAsync(bool removedOnly, string? search);
    Task<List<AdminPostReportDto>> ListReportsAsync(string status);
    Task<ApiResponse> DismissReportAsync(Guid reportId);

    // Category management (admin)
    Task<ApiResponse<int>> CreateCategoryAsync(ForumCategoryRequest request);
    Task<ApiResponse> UpdateCategoryAsync(int id, ForumCategoryRequest request);

    // Aggregator support
    Task<List<ForumPostSearchDto>> SearchAsync(string query, int limit);
    Task<(int Threads, int Posts, int RemovedPosts)> GetStatsAsync();
}
