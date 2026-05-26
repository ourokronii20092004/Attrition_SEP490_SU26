using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;

namespace Attrition.API.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public AuthService(IUserRepository userRepo, IConfiguration config, IEmailService emailService)
    {
        _userRepo = userRepo;
        _config = config;
        _emailService = emailService;
    }

    private string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        if (!await _userRepo.IsUsernameAvailableAsync(request.Username))
            return new ApiResponse<AuthResponse>(false, Error: "Username is already taken.");

        if (!string.IsNullOrEmpty(request.Email) && await _userRepo.GetByEmailAsync(request.Email) != null)
            return new ApiResponse<AuthResponse>(false, Error: "Email is already taken.");

        var verifyToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailVerified = false,
            EmailVerificationToken = verifyToken
        };

        var (accessToken, refreshToken) = GenerateTokens(user);
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));

        await _userRepo.AddAsync(user);

        if (!string.IsNullOrEmpty(user.Email))
        {
            try
            {
                var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
                var verifyUrl = $"{clientUrl}/verify-email?token={Uri.EscapeDataString(verifyToken)}";
                await _emailService.SendAsync(user.Email, "Verify Your Attrition Account", 
                    $"Hi {user.Username},\n\nPlease verify your email by clicking: {verifyUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send verification email: {ex.Message}");
            }
        }

        return new ApiResponse<AuthResponse>(true, new AuthResponse(accessToken, refreshToken, MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _userRepo.GetByUsernameAsync(request.Username);
        if (user == null)
            return new ApiResponse<AuthResponse>(false, Error: "Invalid username or password.");

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            return new ApiResponse<AuthResponse>(false, Error: $"Account locked. Try again after {user.LockoutEnd.Value}.");

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            }
            await _userRepo.UpdateAsync(user);
            return new ApiResponse<AuthResponse>(false, Error: "Invalid username or password.");
        }

        if (user.IsBanned)
            return new ApiResponse<AuthResponse>(false, Error: "Account is suspended.");

        var (accessToken, refreshToken) = GenerateTokens(user);
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        
        await _userRepo.UpdateAsync(user);

        return new ApiResponse<AuthResponse>(true, new AuthResponse(accessToken, refreshToken, MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> RefreshAsync(RefreshRequest request)
    {
        var hashedToken = HashToken(request.RefreshToken);
        var (users, total) = await _userRepo.GetPagedAsync(1, 1, u => u.RefreshToken == hashedToken);
        var user = users.FirstOrDefault();

        if (user == null || user.RefreshTokenExpiresAt <= DateTime.UtcNow || user.IsBanned)
            return new ApiResponse<AuthResponse>(false, Error: "Invalid or expired refresh token.");

        var (accessToken, refreshToken) = GenerateTokens(user);
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));

        await _userRepo.UpdateAsync(user);

        return new ApiResponse<AuthResponse>(true, new AuthResponse(accessToken, refreshToken, MapToDto(user)));
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse<UserDto>(false, Error: "User not found.");
        return new ApiResponse<UserDto>(true, MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> GetProfileByUsernameAsync(string username)
    {
        var user = await _userRepo.GetByUsernameAsync(username);
        if (user == null) return new ApiResponse<UserDto>(false, Error: "User not found.");
        return new ApiResponse<UserDto>(true, MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse<UserDto>(false, Error: "User not found.");

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            var existingWithEmail = await _userRepo.GetByEmailAsync(request.Email);
            if (existingWithEmail != null && existingWithEmail.Id != userId)
                return new ApiResponse<UserDto>(false, Error: "Email is already in use by another account.");
            
            user.Email = request.Email;
            user.IsEmailVerified = false; // reset verification if email changes
        }

        user.Bio = request.Bio;
        if (request.NotifyOnReply.HasValue)
            user.NotifyOnReply = request.NotifyOnReply.Value;
        if (request.NotifyOnMention.HasValue)
            user.NotifyOnMention = request.NotifyOnMention.Value;

        await _userRepo.UpdateAsync(user);

        return new ApiResponse<UserDto>(true, MapToDto(user));
    }

    public async Task<ApiResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, Error: "User not found.");

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return new ApiResponse(false, Error: "Incorrect current password.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        await _userRepo.UpdateAsync(user);

        return new ApiResponse(true);
    }

    public async Task<PaginatedResponse<UserListItem>> ListUsersAsync(int page, int pageSize)
    {
        var (items, total) = await _userRepo.GetPagedAsync(
            page, pageSize,
            orderBy: q => q.OrderByDescending(u => u.JoinedAt)
        );

        var dtos = items.Select(u => new UserListItem(u.Id, u.Username, u.Role, u.IsBanned, u.JoinedAt)).ToList();
        return new PaginatedResponse<UserListItem>(dtos, total, page, pageSize);
    }

    public async Task<ApiResponse> ChangeRoleAsync(Guid userId, string role)
    {
        if (role != "User" && role != "Admin") return new ApiResponse(false, "Invalid role.");
        
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.Role = role;
        await _userRepo.UpdateAsync(user);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ToggleBanAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.IsBanned = !user.IsBanned;
        if (user.IsBanned)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
        }
        await _userRepo.UpdateAsync(user);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> AdminResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = true;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _userRepo.UpdateAsync(user);
        return new ApiResponse(true);
    }

    private (string AccessToken, string RefreshToken) GenerateTokens(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMins = double.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMins),
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        return (accessToken, refreshToken);
    }

    public async Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleAuthRequest request)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Code, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Authentication:Google:ClientId"] }
            });

            if (payload == null)
                return new ApiResponse<AuthResponse>(false, Error: "Invalid Google token.");

            var user = await _userRepo.GetByGoogleIdAsync(payload.Subject);

            if (user == null)
            {
                user = await _userRepo.GetByEmailAsync(payload.Email);
                if (user != null)
                {
                    user.GoogleId = payload.Subject;
                    user.GoogleAvatarUrl = payload.Picture;
                    user.AuthProvider = "linked";
                    if (!user.IsEmailVerified && payload.EmailVerified)
                        user.IsEmailVerified = true;
                    await _userRepo.UpdateAsync(user);
                }
                else
                {
                    var baseUsername = payload.Email.Split('@')[0];
                    var username = baseUsername;
                    int counter = 1;
                    while (!await _userRepo.IsUsernameAvailableAsync(username))
                    {
                        username = $"{baseUsername}{counter++}";
                    }

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
                return new ApiResponse<AuthResponse>(false, Error: "Account is suspended.");

            user.LastLoginAt = DateTime.UtcNow;

            var (accessToken, refreshToken) = GenerateTokens(user);
            user.RefreshToken = HashToken(refreshToken);
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));
            
            await _userRepo.UpdateAsync(user);

            return new ApiResponse<AuthResponse>(true, new AuthResponse(accessToken, refreshToken, MapToDto(user)));
        }
        catch (InvalidJwtException)
        {
            return new ApiResponse<AuthResponse>(false, Error: "Invalid Google token.");
        }
    }

    public async Task<ApiResponse> LinkGoogleAsync(Guid userId, GoogleAuthRequest request)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Code, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Authentication:Google:ClientId"] }
            });

            if (payload == null)
                return new ApiResponse(false, Error: "Invalid Google token.");

            var existingWithGoogle = await _userRepo.GetByGoogleIdAsync(payload.Subject);
            if (existingWithGoogle != null && existingWithGoogle.Id != userId)
                return new ApiResponse(false, Error: "Google account is already linked to another user.");

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return new ApiResponse(false, Error: "User not found.");

            user.GoogleId = payload.Subject;
            user.GoogleAvatarUrl = payload.Picture;
            user.AuthProvider = "linked";
            if (string.IsNullOrEmpty(user.Email))
            {
                user.Email = payload.Email;
                user.IsEmailVerified = payload.EmailVerified;
            }

            await _userRepo.UpdateAsync(user);
            return new ApiResponse(true);
        }
        catch (InvalidJwtException)
        {
            return new ApiResponse(false, Error: "Invalid Google token.");
        }
    }

    public async Task<ApiResponse> UnlinkGoogleAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, Error: "User not found.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            return new ApiResponse(false, Error: "Cannot unlink Google account without setting a password first.");

        user.GoogleId = null;
        user.GoogleAvatarUrl = null;
        user.AuthProvider = "local";

        await _userRepo.UpdateAsync(user);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> LogoutAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _userRepo.UpdateAsync(user);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
            return new ApiResponse(false, "Email is required.");

        var user = await _userRepo.GetByEmailAsync(request.Email);
        if (user == null)
            return new ApiResponse(false, "User not found.");

        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken = HashToken(resetToken);
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        await _userRepo.UpdateAsync(user);

        try
        {
            var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
            var resetUrl = $"{clientUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
            await _emailService.SendAsync(user.Email!, "Reset Your Attrition Password",
                $"Hi {user.Username},\n\nYou requested a password reset. Reset your password by clicking: {resetUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send password reset email: {ex.Message}");
        }

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Token))
            return new ApiResponse(false, "Reset token is required.");

        var hashedToken = HashToken(request.Token);
        var (users, total) = await _userRepo.GetPagedAsync(1, 1, u => u.PasswordResetToken == hashedToken);
        var user = users.FirstOrDefault();

        if (user == null || user.PasswordResetTokenExpiry <= DateTime.UtcNow)
            return new ApiResponse(false, "Invalid or expired password reset token.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.MustChangePassword = false;

        await _userRepo.UpdateAsync(user);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        if (string.IsNullOrEmpty(request.Token))
            return new ApiResponse(false, "Verification token is required.");

        var (users, total) = await _userRepo.GetPagedAsync(1, 1, u => u.EmailVerificationToken == request.Token);
        var user = users.FirstOrDefault();

        if (user == null)
            return new ApiResponse(false, "Invalid verification token.");

        if (!string.IsNullOrEmpty(user.PendingEmail))
        {
            user.Email = user.PendingEmail;
            user.PendingEmail = null;
        }

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;

        await _userRepo.UpdateAsync(user);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> SendVerificationEmailAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        if (string.IsNullOrEmpty(user.Email))
            return new ApiResponse(false, "No email address registered for this account.");

        if (user.IsEmailVerified)
            return new ApiResponse(false, "Email is already verified.");

        var verifyToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.EmailVerificationToken = verifyToken;

        await _userRepo.UpdateAsync(user);

        try
        {
            var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
            var verifyUrl = $"{clientUrl}/verify-email?token={Uri.EscapeDataString(verifyToken)}";
            await _emailService.SendAsync(user.Email, "Verify Your Attrition Account",
                $"Hi {user.Username},\n\nPlease verify your email by clicking: {verifyUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send verification email: {ex.Message}");
            return new ApiResponse(false, "Failed to send verification email. Please try again later.");
        }

        return new ApiResponse(true);
    }

    private UserDto MapToDto(User u) => new UserDto(
        u.Id,
        u.Username,
        u.Email,
        u.DisplayName,
        u.Role,
        u.AvatarPath ?? u.GoogleAvatarUrl,
        u.BackgroundUrl,
        u.ThemeMode,
        u.ThemeAccent,
        u.Bio,
        u.AuthProvider,
        u.JoinedAt,
        u.PostCount,
        u.ContributionCount,
        u.MustChangePassword,
        u.IsEmailVerified,
        u.PendingEmail,
        u.NotifyOnReply,
        u.NotifyOnMention
    );
}