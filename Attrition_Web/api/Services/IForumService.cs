using Attrition.API.DTOs;

namespace Attrition.API.Services;

public interface IForumService
{
    Task<List<ForumCategoryDto>> GetCategoriesAsync();
    Task<PaginatedResponse<ForumThreadListDto>> GetThreadsAsync(string? categorySlug, string? search, int page, int pageSize);
    Task<ForumThreadDto?> GetThreadAsync(Guid threadId);
    Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId);
    Task<ApiResponse<Guid>> CreateThreadAsync(CreateThreadRequest request, Guid userId);
    Task<ApiResponse> CreatePostAsync(Guid threadId, CreatePostRequest request, Guid userId);
    Task<ApiResponse> UpdatePostAsync(Guid postId, UpdatePostRequest request, Guid userId);
    Task<ApiResponse> DeletePostAsync(Guid postId, Guid userId, string role);
    Task<ApiResponse> ToggleReactionAsync(Guid postId, Guid userId, ReactRequest request);
    Task<ApiResponse> TogglePinAsync(Guid threadId);
    Task<ApiResponse> ToggleLockAsync(Guid threadId);
    Task<ApiResponse> DeleteThreadAsync(Guid threadId);
    Task<ApiResponse> SavePostAttachmentsAsync(Guid postId, List<string> urls, Guid userId);
    Task<ApiResponse> ToggleThreadSubscriptionAsync(Guid threadId, Guid userId);
    Task<ApiResponse> ReportPostAsync(Guid postId, string reason, Guid userId);
}
