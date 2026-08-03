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

    /// <summary>
    /// Public profile lookup, honouring the owner's privacy setting.
    ///
    /// With ShowBio off the profile is withheld from everyone but the owner and admins; the
    /// controller turns that into a 403 so the client can show a "hidden profile" page rather
    /// than a generic not-found.
    /// </summary>
    public async Task<ApiResponse<PublicProfileDto>> GetProfileByUsernameAsync(
        string username, Guid? viewerId = null, bool viewerIsAdmin = false)
    {
        var user = await _userRepo.GetByUsernameAsync(username);
        if (user == null) return ApiResponse<PublicProfileDto>.Fail("User not found.");

        var isOwner = viewerId is { } id && id == user.Id;
        if (!user.ShowBio && !isOwner && !viewerIsAdmin)
            return ApiResponse<PublicProfileDto>.Fail(ProfileErrors.Hidden);

        return ApiResponse<PublicProfileDto>.Ok(TokenService.MapToPublicProfile(user));
    }

    public async Task<ApiResponse<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<UserDto>.Fail("User not found.");

        // Every field here is patch-style: absent means "leave alone", so a caller that only
        // updates one setting can't clear the others. Bio used to be assigned unconditionally,
        // which meant saving a toggle or renaming yourself silently wiped your bio. An explicit
        // empty string still clears it — only a missing field is ignored.
        if (request.Bio != null) user.Bio = request.Bio;
        if (request.DisplayName != null)
        {
            var trimmed = request.DisplayName.Trim();
            user.DisplayName = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
        if (request.NotifyOnReply.HasValue) user.NotifyOnReply = request.NotifyOnReply.Value;
        if (request.NotifyOnMention.HasValue) user.NotifyOnMention = request.NotifyOnMention.Value;
        if (request.ShowBio.HasValue) user.ShowBio = request.ShowBio.Value;
        if (request.ShowActivity.HasValue) user.ShowActivity = request.ShowActivity.Value;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);

        return ApiResponse<UserDto>.Ok(TokenService.MapToDto(user));
    }

    public async Task<ApiResponse> UpdateThemeAsync(Guid userId, string themeMode, string themeAccent)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        user.ThemeMode = themeMode;
        user.ThemeAccent = themeAccent;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse<string>> UpdateAvatarAsync(Guid userId, IFormFile file)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<string>.Fail("User not found.");

        var (success, path, error) = await _files.UploadAvatarAsync(userId, file);
        if (!success) return ApiResponse<string>.Fail(error ?? "Upload failed.");

        var previous = user.AvatarPath;
        user.AvatarPath = path;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);
        await _files.DeleteAsync(previous);   // remove the replaced file so it can't be served stale
        return ApiResponse<string>.Ok(path!);
    }

    public async Task<ApiResponse> DeleteAvatarAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            var previous = user.AvatarPath;
            user.AvatarPath = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user);
            await _files.DeleteAsync(previous);
        }
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse<string>> UpdateBackgroundAsync(Guid userId, IFormFile file)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<string>.Fail("User not found.");

        var (success, path, error) = await _files.UploadBackgroundAsync(userId, file);
        if (!success) return ApiResponse<string>.Fail(error ?? "Upload failed.");

        var previous = user.BackgroundUrl;
        user.BackgroundUrl = path;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);
        await _files.DeleteAsync(previous);
        return ApiResponse<string>.Ok(path!);
    }

    public async Task<ApiResponse> DeleteBackgroundAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            var previous = user.BackgroundUrl;
            user.BackgroundUrl = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user);
            await _files.DeleteAsync(previous);
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
        user.EmailVerification = new() { Token = TokenService.HashToken(verifyToken), ExpiresAt = DateTime.UtcNow.AddHours(24) };
        try
        {
            await _userRepo.UpdateAsync(user);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Lost the race to a concurrent claim on the same address.
            return ApiResponse.Fail("Email address is already in use by another account.");
        }

        try
        {
            var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
            var verifyUrl = $"{clientUrl}/verify-email?token={Uri.EscapeDataString(verifyToken)}";
            await _email.SendAsync(request.NewEmail, "Verify Your New Email Address",
                $"Hi {user.Username},\n\nPlease verify your new email address: {verifyUrl}",
                EmailTemplate.Wrap("Verify your new email address",
                    "Confirm this address to finish moving your Attrition account to it.",
                    EmailTemplate.Text($"Hi {user.Username},"),
                    EmailTemplate.Text("You asked to change the email address on your Attrition account to this one. Confirm it here:"),
                    EmailTemplate.Button("Verify this address", verifyUrl),
                    EmailTemplate.Muted("Until you confirm, your account keeps using its previous address."),
                    EmailTemplate.Muted("If you didn't request this, you can ignore this email.")));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send email-change verification"); }

        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> RequestDeletionAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        if (user.IsDeleted) return ApiResponse.Fail("This account is already scheduled for deletion.");
        if (string.IsNullOrEmpty(user.Email))
            return ApiResponse.Fail("Add and verify an email address before deleting your account, so deletion can be confirmed.");

        // Email-confirmed deletion: store a hashed token + expiry and mail the confirm link.
        var rawToken = TokenService.NewRawToken();
        user.DeletionConfirm = new() { Token = TokenService.HashToken(rawToken), ExpiresAt = DateTime.UtcNow.AddHours(24) };
        await _userRepo.UpdateAsync(user);

        try
        {
            var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
            var confirmUrl = $"{clientUrl}/settings/confirm-deletion?token={Uri.EscapeDataString(rawToken)}";
            await _email.SendAsync(user.Email,
                "Confirm Your Account Deletion",
                $"Hi {user.Username},\n\nWe received a request to delete your Attrition account. " +
                $"If this was you, confirm here (link valid 24 hours):\n\n{confirmUrl}\n\n" +
                "After confirming, your account is deactivated and permanently deleted 90 days later. " +
                "Sign back in any time within those 90 days to cancel and restore your account.\n\n" +
                "If you didn't request this, you can ignore this email — nothing will change.",
                EmailTemplate.Wrap("Confirm account deletion",
                    "Confirm you want to delete your Attrition account.",
                    EmailTemplate.Text($"Hi {user.Username},"),
                    EmailTemplate.Text("We received a request to delete your Attrition account. Confirm below if that was you:"),
                    EmailTemplate.Button("Confirm deletion", confirmUrl, danger: true),
                    EmailTemplate.Muted("This link is valid for 24 hours."),
                    EmailTemplate.Text("After confirming, your account is deactivated immediately and permanently deleted 90 days later. Sign back in any time within those 90 days to cancel and restore it."),
                    EmailTemplate.Muted("If you didn't request this, you can ignore this email — nothing will change.")));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send deletion-confirmation email"); }

        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ConfirmDeletionAsync(Guid userId, string token)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        if (string.IsNullOrEmpty(token) || user.DeletionConfirm.Token == null
            || user.DeletionConfirm.ExpiresAt == null || user.DeletionConfirm.ExpiresAt < DateTime.UtcNow
            || user.DeletionConfirm.Token != TokenService.HashToken(token))
            return ApiResponse.Fail("This confirmation link is invalid or has expired.");

        // Soft-delete: mark deleted and revoke sessions, but KEEP PII so the user can recover within
        // 90 days by signing back in. A purge job tombstones (anonymizes) accounts past 90 days.
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletionConfirm = new() { Token = null, ExpiresAt = null };
        user.Refresh = new() { Token = null, ExpiresAt = null };
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }
}
