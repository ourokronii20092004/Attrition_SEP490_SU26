using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Repositories;
using Microsoft.AspNetCore.Http;

namespace Identity.Service.Services;

public class AccountService : IAccountService
{
    private readonly IUserRepository _userRepo;
    private readonly IFileService _files;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<AccountService> _logger;

    public AccountService(IUserRepository userRepo, IFileService files, IEmailService email,
        IConfiguration config, ILogger<AccountService> logger)
    {
        _userRepo = userRepo;
        _files = files;
        _email = email;
        _config = config;
        _logger = logger;
    }

    public async Task<ApiResponse<PublicProfileDto>> GetProfileByUsernameAsync(string username)
    {
        var user = await _userRepo.GetByUsernameAsync(username);
        return user == null
            ? ApiResponse<PublicProfileDto>.Fail("User not found.")
            : ApiResponse<PublicProfileDto>.Ok(TokenService.MapToPublicProfile(user));
    }

    public async Task<ApiResponse<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<UserDto>.Fail("User not found.");

        user.Bio = request.Bio;
        if (request.NotifyOnReply.HasValue) user.NotifyOnReply = request.NotifyOnReply.Value;
        if (request.NotifyOnMention.HasValue) user.NotifyOnMention = request.NotifyOnMention.Value;
        await _userRepo.UpdateAsync(user);

        return ApiResponse<UserDto>.Ok(TokenService.MapToDto(user));
    }

    public async Task<ApiResponse> UpdateThemeAsync(Guid userId, string themeMode, string themeAccent)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        user.ThemeMode = themeMode;
        user.ThemeAccent = themeAccent;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse<string>> UpdateAvatarAsync(Guid userId, IFormFile file)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<string>.Fail("User not found.");

        var (success, path, error) = await _files.UploadAvatarAsync(userId, file);
        if (!success) return ApiResponse<string>.Fail(error ?? "Upload failed.");

        user.AvatarPath = path;
        await _userRepo.UpdateAsync(user);
        return ApiResponse<string>.Ok(path!);
    }

    public async Task<ApiResponse> DeleteAvatarAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.AvatarPath = null;
            await _userRepo.UpdateAsync(user);
        }
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse<string>> UpdateBackgroundAsync(Guid userId, IFormFile file)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<string>.Fail("User not found.");

        var (success, path, error) = await _files.UploadBackgroundAsync(userId, file);
        if (!success) return ApiResponse<string>.Fail(error ?? "Upload failed.");

        user.BackgroundUrl = path;
        await _userRepo.UpdateAsync(user);
        return ApiResponse<string>.Ok(path!);
    }

    public async Task<ApiResponse> DeleteBackgroundAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.BackgroundUrl = null;
            await _userRepo.UpdateAsync(user);
        }
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> SetPasswordAsync(Guid userId, SetPasswordRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        if (!string.IsNullOrEmpty(user.PasswordHash))
            return ApiResponse.Fail("Password is already set. Use the change password flow instead.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> UpdateEmailAsync(Guid userId, UpdateEmailRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        if (string.IsNullOrEmpty(user.PasswordHash))
            return ApiResponse.Fail("Please set a password on your account first before changing your email.");
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return ApiResponse.Fail("Incorrect current password.");

        var existing = await _userRepo.GetByEmailAsync(request.NewEmail);
        if (existing != null && existing.Id != userId)
            return ApiResponse.Fail("Email address is already in use by another account.");

        var verifyToken = TokenService.NewRawToken();
        user.PendingEmail = request.NewEmail;
        user.EmailVerificationToken = TokenService.HashToken(verifyToken);
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        await _userRepo.UpdateAsync(user);

        try
        {
            var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
            var verifyUrl = $"{clientUrl}/verify-email?token={Uri.EscapeDataString(verifyToken)}";
            await _email.SendAsync(request.NewEmail, "Verify Your New Email Address",
                $"Hi {user.Username},\n\nPlease verify your new email address: {verifyUrl}");
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send email-change verification"); }

        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeleteAccountAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");

        user.IsBanned = true;
        user.Username = $"deleted_user_{user.Id.ToString()[..8]}";
        user.DisplayName = "Deleted User";
        user.Email = null;
        user.PasswordHash = null;
        user.Bio = null;
        user.AvatarPath = null;
        user.GoogleId = null;
        user.GoogleAvatarUrl = null;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }
}
