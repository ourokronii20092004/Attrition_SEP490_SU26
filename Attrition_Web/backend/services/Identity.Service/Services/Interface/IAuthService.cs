using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Services.Interface;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, string? ip);
    Task<ApiResponse<AuthResponse>> RefreshAsync(RefreshRequest request);
    Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleAuthRequest request, string? ip = null);
    Task<ApiResponse<AuthResponse>> GoogleCodeExchangeAsync(string code, string redirectUri, string? ip = null);
    Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId);
    Task<ApiResponse<SessionStatusDto>> CheckSessionAsync(Guid userId);
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
