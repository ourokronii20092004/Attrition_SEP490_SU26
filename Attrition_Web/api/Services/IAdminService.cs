using Attrition.API.DTOs;
using Attrition.API.Models;

namespace Attrition.API.Services;

public interface IAdminService
{
    Task<PaginatedResponse<AdminUserDto>> ListUsersAsync(int page, int pageSize, string? search, string sort);
    Task<ApiResponse> DeleteUserAsync(Guid id, Guid adminId);
    Task<PaginatedResponse<AdminWikiArticleDto>> ListWikiArticlesAsync(int page, int pageSize, string? search);
    Task<bool> DeleteWikiArticleAsync(Guid id);
    Task<PaginatedResponse<AdminWikiCategoryDto>> ListWikiCategoriesAsync(int page, int pageSize, string? search);
    Task<WikiCategory> CreateWikiCategoryAsync(WikiCategoryRequest req);
    Task<WikiCategory?> UpdateWikiCategoryAsync(int id, WikiCategoryRequest req);
    Task<(bool Found, bool HasArticles)> DeleteWikiCategoryAsync(int id);
    Task<PaginatedResponse<AdminForumThreadDto>> ListForumThreadsAsync(int page, int pageSize, string? search);
    Task<AdminTogglePinResponse?> TogglePinAsync(Guid id);
    Task<AdminToggleLockResponse?> ToggleLockAsync(Guid id);
    Task<bool> DeleteThreadAsync(Guid id);
    Task<PaginatedResponse<AdminForumPostDto>> ListForumPostsAsync(int page, int pageSize, bool? removedOnly, string? search);
    Task<bool> RemovePostAsync(Guid id, Guid adminId, string reason);
    Task<bool> RestorePostAsync(Guid id);
    Task<AdminStatsDto> GetStatsAsync();
    Task<PaginatedResponse<AdminPostReportDto>> ListForumReportsAsync(int page, int pageSize, string? status);
    Task<bool> DismissReportAsync(Guid reportId);

}
