using Attrition.API.DTOs;

namespace Attrition.API.Services;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> RefreshAsync(RefreshRequest request);
    Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId);
    Task<ApiResponse<UserDto>> GetProfileByUsernameAsync(string username);
    Task<ApiResponse<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<ApiResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<PaginatedResponse<UserListItem>> ListUsersAsync(int page, int pageSize);
    Task<ApiResponse> ChangeRoleAsync(Guid userId, string role);
    Task<ApiResponse> ToggleBanAsync(Guid userId);
    Task<ApiResponse> AdminResetPasswordAsync(Guid userId, string newPassword);
    Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleAuthRequest request);
    Task<ApiResponse> LinkGoogleAsync(Guid userId, GoogleAuthRequest request);
    Task<ApiResponse> UnlinkGoogleAsync(Guid userId);
    Task<ApiResponse> LogoutAsync(Guid userId);
    Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request);
    Task<ApiResponse> SendVerificationEmailAsync(Guid userId);
}
