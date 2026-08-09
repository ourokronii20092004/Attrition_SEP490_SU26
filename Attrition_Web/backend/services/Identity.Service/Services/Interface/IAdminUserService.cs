using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Services.Interface;

public interface IAdminUserService
{
    Task<PaginatedResponse<UserListItem>> ListUsersAsync(int page, int pageSize, string? search, string? sort, string? status);

    Task<ApiResponse<AdminUserDetailDto>> GetUserDetailAsync(Guid userId);

    Task<ApiResponse> ChangeRoleAsync(Guid userId, string role);

    Task<ApiResponse> ToggleBanAsync(Guid userId);

    Task<ApiResponse> AdminResetPasswordAsync(Guid userId, string newPassword);

    Task<ApiResponse> DeleteUserAsync(Guid userId);

    Task<List<UserSummaryDto>> SearchAsync(string query, int limit);

    Task<List<UserSummaryDto>> GetByIdsAsync(IEnumerable<Guid> ids);

    Task<int> CountAsync();
}