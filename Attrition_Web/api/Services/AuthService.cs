using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Attrition.API.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            return new ApiResponse<AuthResponse>(false, Error: "Username is already taken.");

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        
        var (accessToken, refreshToken) = GenerateTokens(user);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));

        await _db.SaveChangesAsync();

        return new ApiResponse<AuthResponse>(true, new AuthResponse(accessToken, refreshToken, MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new ApiResponse<AuthResponse>(false, Error: "Invalid username or password.");

        if (user.IsBanned)
            return new ApiResponse<AuthResponse>(false, Error: "Account is suspended.");

        var (accessToken, refreshToken) = GenerateTokens(user);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));
        
        await _db.SaveChangesAsync();

        return new ApiResponse<AuthResponse>(true, new AuthResponse(accessToken, refreshToken, MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> RefreshAsync(RefreshRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

        if (user == null || user.RefreshTokenExpiresAt <= DateTime.UtcNow || user.IsBanned)
            return new ApiResponse<AuthResponse>(false, Error: "Invalid or expired refresh token.");

        var (accessToken, refreshToken) = GenerateTokens(user);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));

        await _db.SaveChangesAsync();

        return new ApiResponse<AuthResponse>(true, new AuthResponse(accessToken, refreshToken, MapToDto(user)));
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return new ApiResponse<UserDto>(false, Error: "User not found.");
        return new ApiResponse<UserDto>(true, MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> GetProfileByUsernameAsync(string username)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null) return new ApiResponse<UserDto>(false, Error: "User not found.");
        return new ApiResponse<UserDto>(true, MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return new ApiResponse<UserDto>(false, Error: "User not found.");

        user.Bio = request.Bio;
        await _db.SaveChangesAsync();

        return new ApiResponse<UserDto>(true, MapToDto(user));
    }

    public async Task<ApiResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return new ApiResponse(false, Error: "User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return new ApiResponse(false, Error: "Incorrect current password.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        await _db.SaveChangesAsync();

        return new ApiResponse(true);
    }

    public async Task<PaginatedResponse<UserListItem>> ListUsersAsync(int page, int pageSize)
    {
        var query = _db.Users.OrderByDescending(u => u.JoinedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserListItem(u.Id, u.Username, u.Role, u.IsBanned, u.JoinedAt))
            .ToListAsync();

        return new PaginatedResponse<UserListItem>(items, total, page, pageSize);
    }

    public async Task<ApiResponse> ChangeRoleAsync(Guid userId, string role)
    {
        if (role != "User" && role != "Admin") return new ApiResponse(false, "Invalid role.");
        
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.Role = role;
        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ToggleBanAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.IsBanned = !user.IsBanned;
        if (user.IsBanned)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
        }
        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> AdminResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = true;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _db.SaveChangesAsync();
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

    private UserDto MapToDto(User u) => new UserDto(
        u.Id, u.Username, u.Role, u.AvatarPath, u.Bio, u.JoinedAt, u.PostCount, u.ContributionCount, u.MustChangePassword
    );
}