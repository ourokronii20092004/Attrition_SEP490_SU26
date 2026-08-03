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
    /// <summary>
    /// Email of the linked Google account. Recorded separately from <see cref="Email"/> because the
    /// two can legitimately differ — linking google-b@ to an account registered as a@ is allowed —
    /// and without storing it the UI can only say "connected", never to what.
    /// </summary>
    public string? GoogleEmail { get; set; }
    public string AuthProvider { get; set; } = "local";

    // Profile
    public string Role { get; set; } = "User";
    public string? AvatarPath { get; set; }
    public string? BackgroundUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // UI Config
    // "light" | "dark". Was "system", which the web client has no handling for — its ThemeMode
    // is light|dark only, so a new account's stored value never matched and silently fell back.
    public string ThemeMode { get; set; } = "light";
    public string ThemeAccent { get; set; } = "ember";

    // Stats (denormalized counters maintained by Forum/Wiki via Admin or events)
    public int PostCount { get; set; } = 0;
    public int ContributionCount { get; set; } = 0;

    // Account Status
    public bool IsBanned { get; set; } = false;
    // Deletion is distinct from ban: a deleted account is anonymized/tombstoned, not punished.
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public bool MustChangePassword { get; set; } = false;

    // Preferences
    public bool NotifyOnReply { get; set; } = true;
    public bool NotifyOnMention { get; set; } = true;

    // Profile privacy. Default open, matching how every existing profile already behaves.
    /// <summary>When false, the profile page is withheld from everyone but the owner and admins.</summary>
    public bool ShowBio { get; set; } = true;
    /// <summary>When false, the activity feed is withheld but the rest of the profile still renders.</summary>
    public bool ShowActivity { get; set; } = true;

    // Verification & Recovery (owned value objects — stored as columns on this table)
    public string? PendingEmail { get; set; }
    public bool IsEmailVerified { get; set; } = false;
    public TokenPair Refresh { get; set; } = new();
    public TokenPair EmailVerification { get; set; } = new();
    public TokenPair PasswordReset { get; set; } = new();
    public TokenPair DeletionConfirm { get; set; } = new();

    // Login security (owned value object)
    public LoginSecurity Security { get; set; } = new();

    // Timestamps
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
