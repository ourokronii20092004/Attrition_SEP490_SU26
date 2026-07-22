using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Services.Interface;

public interface IAccountService
{
    Task<ApiResponse<PublicProfileDto>> GetProfileByUsernameAsync(string username);
    Task<ApiResponse<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<ApiResponse> UpdateThemeAsync(Guid userId, string themeMode, string themeAccent);
    Task<ApiResponse<string>> UpdateAvatarAsync(Guid userId, Microsoft.AspNetCore.Http.IFormFile file);
    Task<ApiResponse> DeleteAvatarAsync(Guid userId);
    Task<ApiResponse<string>> UpdateBackgroundAsync(Guid userId, Microsoft.AspNetCore.Http.IFormFile file);
    Task<ApiResponse> DeleteBackgroundAsync(Guid userId);
    Task<ApiResponse> SetPasswordAsync(Guid userId, SetPasswordRequest request);
    Task<ApiResponse> UpdateEmailAsync(Guid userId, UpdateEmailRequest request);
    Task<ApiResponse> RequestDeletionAsync(Guid userId);
    Task<ApiResponse> ConfirmDeletionAsync(Guid userId, string token);
}
