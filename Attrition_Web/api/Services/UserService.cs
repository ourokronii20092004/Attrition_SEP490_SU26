using Attrition.API.DTOs;
using Attrition.API.Repositories;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Attrition.API.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IForumRepository _forumRepo;
    private readonly IWikiRepository _wikiRepo;
    private readonly IFileService _files;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public UserService(
        IUserRepository userRepo,
        IForumRepository forumRepo,
        IWikiRepository wikiRepo,
        IFileService files,
        IConfiguration config,
        IEmailService emailService)
    {
        _userRepo = userRepo;
        _forumRepo = forumRepo;
        _wikiRepo = wikiRepo;
        _files = files;
        _config = config;
        _emailService = emailService;
    }

    public async Task<PaginatedResponse<UserPostDto>?> GetUserPostsAsync(string username, int page, int pageSize)
    {
        var user = await _userRepo.GetByUsernameAsync(username);
        if (user == null) return null;

        return await _forumRepo.GetUserPostsPagedAsync(user.Id, page, pageSize);
    }

    public async Task<PaginatedResponse<UserContributionDto>?> GetUserContributionsAsync(string username, int page, int pageSize)
    {
        var user = await _userRepo.GetByUsernameAsync(username);
        if (user == null) return null;

        return await _wikiRepo.GetUserContributionsPagedAsync(user.Id, page, pageSize);
    }

    public async Task<ApiResponse> UpdateThemeAsync(Guid userId, string themeMode, string themeAccent)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.ThemeMode = themeMode;
        user.ThemeAccent = themeAccent;
        await _userRepo.UpdateAsync(user);

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> UpdateAvatarAsync(Guid userId, string? avatarPath)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.AvatarPath = avatarPath;
            await _userRepo.UpdateAsync(user);
        }
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteAvatarAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.AvatarPath = null;
            await _userRepo.UpdateAsync(user);
        }
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> UpdateBackgroundAsync(Guid userId, string? backgroundUrl)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.BackgroundUrl = backgroundUrl;
            await _userRepo.UpdateAsync(user);
        }
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteBackgroundAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.BackgroundUrl = null;
            await _userRepo.UpdateAsync(user);
        }
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> SetPasswordAsync(Guid userId, SetPasswordRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        if (!string.IsNullOrEmpty(user.PasswordHash))
            return new ApiResponse(false, "Password is already set. Use the change password flow instead.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepo.UpdateAsync(user);

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> UpdateEmailAsync(Guid userId, UpdateEmailRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            return new ApiResponse(false, "Please set a password on your account first before changing your email.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return new ApiResponse(false, "Incorrect current password.");

        var existing = await _userRepo.GetByEmailAsync(request.NewEmail);
        if (existing != null && existing.Id != userId)
            return new ApiResponse(false, "Email address is already in use by another account.");

        var verifyToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.PendingEmail = request.NewEmail;
        user.EmailVerificationToken = verifyToken;

        await _userRepo.UpdateAsync(user);

        try
        {
            var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
            var verifyUrl = $"{clientUrl}/verify-email?token={Uri.EscapeDataString(verifyToken)}";
            await _emailService.SendAsync(request.NewEmail, "Verify Your New Email Address",
                $"Hi {user.Username},\n\nPlease verify your new email address by clicking: {verifyUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email verification for email change: {ex.Message}");
        }

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteAccountAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.IsBanned = true;
        user.Username = $"deleted_user_{user.Id.ToString().Substring(0, 8)}";
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
        return new ApiResponse(true);
    }
}
