using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Services;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, string? ip);
    Task<ApiResponse<AuthResponse>> RefreshAsync(RefreshRequest request);
    Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleAuthRequest request, string? ip = null);
    Task<ApiResponse<AuthResponse>> GoogleCodeExchangeAsync(string code, string redirectUri, string? ip = null);
    Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId);
    Task<ApiResponse<SessionStatusDto>> CheckSessionAsync(Guid userId);
    // Returns a freshly-minted session for the caller so the device that changed the password stays
    // signed in while every other device is invalidated (Security.TokensValidAfter is bumped).
    Task<ApiResponse<AuthResponse>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<ApiResponse> LogoutAsync(Guid userId);
    Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request);
    Task<ApiResponse> SendVerificationEmailAsync(Guid userId);
    Task<ApiResponse> LinkGoogleAsync(Guid userId, GoogleAuthRequest request);
    Task<ApiResponse> LinkGoogleByCodeAsync(Guid userId, string code, string redirectUri);
    Task<ApiResponse> UnlinkGoogleAsync(Guid userId);
}

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
    // Account deletion (PROB-4): request sends an email confirmation; confirm soft-deletes with a
    // 90-day recovery window; a purge job tombstones accounts past the window.
    Task<ApiResponse> RequestDeletionAsync(Guid userId);
    Task<ApiResponse> ConfirmDeletionAsync(Guid userId, string token);
}

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
