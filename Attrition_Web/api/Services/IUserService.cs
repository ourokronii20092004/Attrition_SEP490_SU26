using Attrition.API.DTOs;

namespace Attrition.API.Services;

public interface IUserService
{
    Task<PaginatedResponse<UserPostDto>?> GetUserPostsAsync(string username, int page, int pageSize);
    Task<PaginatedResponse<UserContributionDto>?> GetUserContributionsAsync(string username, int page, int pageSize);
    Task<ApiResponse> UpdateThemeAsync(Guid userId, string themeMode, string themeAccent);
    Task<ApiResponse> UpdateAvatarAsync(Guid userId, string? avatarPath);
    Task<ApiResponse> DeleteAvatarAsync(Guid userId);
    Task<ApiResponse> UpdateBackgroundAsync(Guid userId, string? backgroundUrl);
    Task<ApiResponse> DeleteBackgroundAsync(Guid userId);
    Task<ApiResponse> SetPasswordAsync(Guid userId, SetPasswordRequest request);
    Task<ApiResponse> UpdateEmailAsync(Guid userId, UpdateEmailRequest request);
    Task<ApiResponse> DeleteAccountAsync(Guid userId);
}
