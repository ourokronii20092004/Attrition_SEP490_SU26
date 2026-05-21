namespace Attrition.API.Models;

public class User
{
    // ─── Identity ───
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }                    // NEW: from Google or manual
    public bool EmailVerified { get; set; } = false;      // NEW: true if from OAuth
    public string? DisplayName { get; set; }              // NEW: shown in UI
    public string? PasswordHash { get; set; }             // CHANGED: nullable (OAuth users may not have password)

    // ─── OAuth ───
    public string? GoogleId { get; set; }                 // NEW: Google 'sub' claim
    public string? GoogleAvatarUrl { get; set; }          // NEW: profile picture from Google
    public string AuthProvider { get; set; } = "local";   // NEW: "local" | "google" | "linked"

    // ─── Profile ───
    public string Role { get; set; } = "User";
    public string? AvatarPath { get; set; }               // uploaded avatar (overrides Google)
    public string? BackgroundUrl { get; set; }            // NEW: profile background
    public string? Bio { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // ─── UI Config ───
    public string ThemeMode { get; set; } = "system";     // NEW: "light" | "dark" | "system"
    public string ThemeAccent { get; set; } = "ember";    // NEW: accent color key

    // ─── Stats ───
    public int PostCount { get; set; } = 0;
    public int ContributionCount { get; set; } = 0;

    // ─── Account Status ───
    public bool IsBanned { get; set; } = false;
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }            // NEW
    public string? LastLoginIp { get; set; }              // NEW

    // ─── Tokens ───
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    // ─── Timestamps ───
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;  // NEW
}
