namespace Identity.Service.DTOs;

public record UserDto(
    Guid Id,
    string Username,
    string? Email,
    string? DisplayName,
    string Role,
    string? AvatarUrl,
    string? BackgroundUrl,
    string ThemeMode,
    string ThemeAccent,
    string? Bio,
    string AuthProvider,
    DateTime JoinedAt,
    int PostCount,
    int ContributionCount,
    bool MustChangePassword,
    bool IsEmailVerified,
    string? PendingEmail,
    bool NotifyOnReply,
    bool NotifyOnMention,
    bool ShowBio,
    bool ShowActivity,
    bool HasPassword,
    bool IsGoogleLinked,
    // Which Google account is attached. Shown in settings so "Connected" names the address rather
    // than leaving the user guessing which of their Google accounts they linked.
    string? GoogleEmail = null
);

public record UserListItem(Guid Id, string Username, string Role, bool IsBanned, bool IsDeleted, DateTime JoinedAt);

// Rich per-user view for the admin dashboard (moderation context the sparse list omits).
public record AdminUserDetailDto(
    Guid Id,
    string Username,
    string? Email,
    string? DisplayName,
    string Role,
    string? AvatarUrl,
    string? BackgroundUrl,
    string? Bio,
    string AuthProvider,
    DateTime JoinedAt,
    int PostCount,
    int ContributionCount,
    bool IsBanned,
    bool IsDeleted,
    DateTime? DeletedAt,
    bool IsEmailVerified,
    string? PendingEmail,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    string? LastLoginIp,
    int FailedLoginAttempts,
    DateTime? LockoutEnd
);

public record PublicProfileDto(
    Guid Id,
    string Username,
    string? DisplayName,
    string Role,
    string? AvatarUrl,
    string? BackgroundUrl,
    string? Bio,
    DateTime JoinedAt,
    int PostCount,
    int ContributionCount,
    // Lets the profile page know to omit the activity feed. A profile with ShowBio off is never
    // returned to a stranger at all, so there is no equivalent flag for it here.
    bool ShowActivity = true
);

public record UserSummaryDto(Guid Id, string Username, string? DisplayName, string? AvatarUrl, string Role);

// TokensValidAfter lets callers detect a revoked session: a token issued before this instant
// (compared against its "sat" claim) has been invalidated by a password change / forced logout.
public record SessionStatusDto(Guid UserId, string Username, string Role, bool IsBanned, DateTime? TokensValidAfter = null);

public record ChangeRoleRequest(string Role);
public record AdminResetPasswordRequest(string NewPassword);

public record ReportUserRequest(string Reason);
public record AdminUserReportDto(
    Guid Id, Guid ReportedUserId, string ReportedUserName, string ReporterName,
    string Reason, string Status, DateTime CreatedAt,
    string? ActionTaken, string? ModeratorNote, string? ResolvedByName, DateTime? ResolvedAt);
// Resolve a report, optionally banning the reported user in the same step.
public record ResolveReportRequest(bool BanUser, string? Note);
