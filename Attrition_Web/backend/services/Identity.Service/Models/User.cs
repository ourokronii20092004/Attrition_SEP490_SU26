namespace Identity.Service.Models;

public class User
{
    // Identity
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PasswordHash { get; set; }

    // OAuth
    public string? GoogleId { get; set; }
    public string? GoogleAvatarUrl { get; set; }
    public string AuthProvider { get; set; } = "local";

    // Profile
    public string Role { get; set; } = "User";
    public string? AvatarPath { get; set; }
    public string? BackgroundUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // UI Config
    public string ThemeMode { get; set; } = "system";
    public string ThemeAccent { get; set; } = "ember";

    // Stats (denormalized counters maintained by Forum/Wiki via Admin or events)
    public int PostCount { get; set; } = 0;
    public int ContributionCount { get; set; } = 0;

    // Account Status
    public bool IsBanned { get; set; } = false;
    // Deletion is distinct from ban: a deleted account is anonymized/tombstoned, not punished.
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    // Pending email-confirmed deletion (PROB-4): set when the user requests deletion; cleared on
    // confirm (→ IsDeleted) or if they change their mind. The soft-deleted account keeps its PII
    // for a 90-day recovery window, after which a purge job tombstones it.
    public string? DeletionConfirmToken { get; set; }
    public DateTime? DeletionConfirmTokenExpiry { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }

    // Verification & Recovery
    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }
    public string? PendingEmail { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    // Preferences
    public bool NotifyOnReply { get; set; } = true;
    public bool NotifyOnMention { get; set; } = true;

    // Tokens
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    // Timestamps
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
