using BuildingBlocks.Contracts;
using Google.Apis.Auth;
using Identity.Service.DTOs;
using Identity.Service.Models;
using Identity.Service.Repositories;

namespace Identity.Service.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository userRepo, IConfiguration config, IEmailService email,
        TokenService tokens, ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _config = config;
        _email = email;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        if (!await _userRepo.IsUsernameAvailableAsync(request.Username))
            return ApiResponse<AuthResponse>.Fail("Username is already taken.");

        if (!string.IsNullOrEmpty(request.Email) && await _userRepo.GetByEmailAsync(request.Email) != null)
            return ApiResponse<AuthResponse>.Fail("Email is already taken.");

        var verifyToken = TokenService.NewRawToken();
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailVerified = false,
            EmailVerificationToken = verifyToken
        };

        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.RefreshToken = TokenService.HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays);

        await _userRepo.AddAsync(user);
        await SendVerifyEmail(user, verifyToken);

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, string? ip)
    {
        var user = await _userRepo.GetByUsernameAsync(request.Username);
        if (user == null)
            return ApiResponse<AuthResponse>.Fail("Invalid username or password.");

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            return ApiResponse<AuthResponse>.Fail($"Account locked. Try again after {user.LockoutEnd.Value}.");

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            await _userRepo.UpdateAsync(user);
            return ApiResponse<AuthResponse>.Fail("Invalid username or password.");
        }

        if (user.IsBanned)
            return ApiResponse<AuthResponse>.Fail("Account is suspended.");

        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.RefreshToken = TokenService.HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays);
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ip;
        await _userRepo.UpdateAsync(user);

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> RefreshAsync(RefreshRequest request)
    {
        var hashed = TokenService.HashToken(request.RefreshToken);
        var user = await _userRepo.GetByRefreshTokenAsync(hashed);

        if (user == null || user.RefreshTokenExpiresAt <= DateTime.UtcNow || user.IsBanned)
            return ApiResponse<AuthResponse>.Fail("Invalid or expired refresh token.");

        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.RefreshToken = TokenService.HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays);
        await _userRepo.UpdateAsync(user);

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleAuthRequest request)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Code,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _config["Authentication:Google:ClientId"] } });

            if (payload == null)
                return ApiResponse<AuthResponse>.Fail("Invalid Google token.");

            var user = await _userRepo.GetByGoogleIdAsync(payload.Subject);
            if (user == null)
            {
                user = await _userRepo.GetByEmailAsync(payload.Email);
                if (user != null)
                {
                    user.GoogleId = payload.Subject;
                    user.GoogleAvatarUrl = payload.Picture;
                    user.AuthProvider = "linked";
                    if (!user.IsEmailVerified && payload.EmailVerified) user.IsEmailVerified = true;
                    await _userRepo.UpdateAsync(user);
                }
                else
                {
                    var baseUsername = payload.Email.Split('@')[0];
                    var username = baseUsername;
                    int counter = 1;
                    while (!await _userRepo.IsUsernameAvailableAsync(username))
                        username = $"{baseUsername}{counter++}";

                    user = new User
                    {
                        Username = username,
                        Email = payload.Email,
                        IsEmailVerified = payload.EmailVerified,
                        DisplayName = payload.Name,
                        GoogleId = payload.Subject,
                        GoogleAvatarUrl = payload.Picture,
                        AuthProvider = "google",
                        PasswordHash = null
                    };
                    await _userRepo.AddAsync(user);
                }
            }

            if (user.IsBanned)
                return ApiResponse<AuthResponse>.Fail("Account is suspended.");

            user.LastLoginAt = DateTime.UtcNow;
            var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
            user.RefreshToken = TokenService.HashToken(refreshToken);
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays);
            await _userRepo.UpdateAsync(user);

            return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
        }
        catch (InvalidJwtException)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid Google token.");
        }
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        return user == null
            ? ApiResponse<UserDto>.Fail("User not found.")
            : ApiResponse<UserDto>.Ok(TokenService.MapToDto(user));
    }

    public async Task<ApiResponse<SessionStatusDto>> CheckSessionAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<SessionStatusDto>.Fail("User not found.");
        return ApiResponse<SessionStatusDto>.Ok(
            new SessionStatusDto(user.Id, user.Username, user.Role, user.IsBanned));
    }

    public async Task<ApiResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return ApiResponse.Fail("Incorrect current password.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> LogoutAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");

        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Email)) return ApiResponse.Fail("Email is required.");

        var user = await _userRepo.GetByEmailAsync(request.Email);
        if (user == null) return ApiResponse.Fail("User not found.");

        var resetToken = TokenService.NewRawToken();
        user.PasswordResetToken = TokenService.HashToken(resetToken);
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _userRepo.UpdateAsync(user);

        var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
        var resetUrl = $"{clientUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        await TrySend(user.Email!, "Reset Your Attrition Password",
            $"Hi {user.Username},\n\nYou requested a password reset. Reset it here: {resetUrl}");
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Token)) return ApiResponse.Fail("Reset token is required.");

        var hashed = TokenService.HashToken(request.Token);
        var user = await _userRepo.GetByPasswordResetTokenAsync(hashed);
        if (user == null || user.PasswordResetTokenExpiry <= DateTime.UtcNow)
            return ApiResponse.Fail("Invalid or expired password reset token.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.MustChangePassword = false;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        if (string.IsNullOrEmpty(request.Token)) return ApiResponse.Fail("Verification token is required.");

        var user = await _userRepo.GetByEmailVerificationTokenAsync(request.Token);
        if (user == null) return ApiResponse.Fail("Invalid verification token.");

        if (!string.IsNullOrEmpty(user.PendingEmail))
        {
            user.Email = user.PendingEmail;
            user.PendingEmail = null;
        }
        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> SendVerificationEmailAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        if (string.IsNullOrEmpty(user.Email)) return ApiResponse.Fail("No email address registered for this account.");
        if (user.IsEmailVerified) return ApiResponse.Fail("Email is already verified.");

        var verifyToken = TokenService.NewRawToken();
        user.EmailVerificationToken = verifyToken;
        await _userRepo.UpdateAsync(user);

        await SendVerifyEmail(user, verifyToken);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> LinkGoogleAsync(Guid userId, GoogleAuthRequest request)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Code,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _config["Authentication:Google:ClientId"] } });
            if (payload == null) return ApiResponse.Fail("Invalid Google token.");

            var existing = await _userRepo.GetByGoogleIdAsync(payload.Subject);
            if (existing != null && existing.Id != userId)
                return ApiResponse.Fail("Google account is already linked to another user.");

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return ApiResponse.Fail("User not found.");

            user.GoogleId = payload.Subject;
            user.GoogleAvatarUrl = payload.Picture;
            user.AuthProvider = "linked";
            if (string.IsNullOrEmpty(user.Email))
            {
                user.Email = payload.Email;
                user.IsEmailVerified = payload.EmailVerified;
            }
            await _userRepo.UpdateAsync(user);
            return ApiResponse.Ok();
        }
        catch (InvalidJwtException)
        {
            return ApiResponse.Fail("Invalid Google token.");
        }
    }

    public async Task<ApiResponse> UnlinkGoogleAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            return ApiResponse.Fail("Cannot unlink Google account without setting a password first.");

        user.GoogleId = null;
        user.GoogleAvatarUrl = null;
        user.AuthProvider = "local";
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    private Task SendVerifyEmail(User user, string verifyToken)
    {
        if (string.IsNullOrEmpty(user.Email)) return Task.CompletedTask;
        var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
        var verifyUrl = $"{clientUrl}/verify-email?token={Uri.EscapeDataString(verifyToken)}";
        return TrySend(user.Email, "Verify Your Attrition Account",
            $"Hi {user.Username},\n\nPlease verify your email: {verifyUrl}");
    }

    private async Task TrySend(string to, string subject, string body)
    {
        try { await _email.SendAsync(to, subject, body); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send email to {To}", to); }
    }
}
