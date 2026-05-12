namespace Attrition.API.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";           // "User" or "Admin"
    public string? AvatarPath { get; set; }
    public string? Bio { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public int PostCount { get; set; } = 0;
    public int ContributionCount { get; set; } = 0;
    public bool IsBanned { get; set; } = false;
    public bool MustChangePassword { get; set; } = false;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
}