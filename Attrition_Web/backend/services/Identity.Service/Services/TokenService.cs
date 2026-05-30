using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Service.DTOs;
using Identity.Service.Models;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Service.Services;

/// <summary>Token issuance, hashing, and User→DTO mapping shared by Auth/Account/Admin services.</summary>
public sealed class TokenService
{
    private readonly IConfiguration _config;
    public TokenService(IConfiguration config) => _config = config;

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public static string NewRawToken(int bytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    public (string AccessToken, string RefreshToken) GenerateTokens(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("username", user.Username),
            new(ClaimTypes.Role, user.Role),
            new("email_verified", user.IsEmailVerified ? "true" : "false")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMins = double.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMins),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (accessToken, refreshToken);
    }

    public double RefreshExpiryDays => double.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

    public static UserDto MapToDto(User u) => new(
        u.Id, u.Username, u.Email, u.DisplayName, u.Role,
        u.AvatarPath ?? u.GoogleAvatarUrl, u.BackgroundUrl,
        u.ThemeMode, u.ThemeAccent, u.Bio, u.AuthProvider, u.JoinedAt,
        u.PostCount, u.ContributionCount, u.MustChangePassword,
        u.IsEmailVerified, u.PendingEmail, u.NotifyOnReply, u.NotifyOnMention);

    public static PublicProfileDto MapToPublicProfile(User u) => new(
        u.Id, u.Username, u.DisplayName, u.Role,
        u.AvatarPath ?? u.GoogleAvatarUrl, u.BackgroundUrl,
        u.Bio, u.JoinedAt, u.PostCount, u.ContributionCount);
}
