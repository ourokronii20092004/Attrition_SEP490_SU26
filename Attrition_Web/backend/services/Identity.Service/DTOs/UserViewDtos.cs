namespace Identity.Service.DTOs;

// ─── User views ───
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
    bool HasPassword,
    bool IsGoogleLinked
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

// ─── Public profile (anonymous, no PII) ───
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
    int ContributionCount
);

// ─── Internal lookup (used by Search/Admin aggregators) ───
public record UserSummaryDto(Guid Id, string Username, string? DisplayName, string? AvatarUrl, string Role);

// ─── Session check (polled by the game client to enforce bans mid-session) ───
// TokensValidAfter lets callers detect a revoked session: a token issued before this instant
// (compared against its "sat" claim) has been invalidated by a password change / forced logout.
public record SessionStatusDto(Guid UserId, string Username, string Role, bool IsBanned, DateTime? TokensValidAfter = null);

// ─── Admin actions ───
public record ChangeRoleRequest(string Role);
public record AdminResetPasswordRequest(string NewPassword);

// ─── User reports (QOLF-9) ───
public record ReportUserRequest(string Reason);
public record AdminUserReportDto(
    Guid Id, Guid ReportedUserId, string ReportedUserName, string ReporterName,
    string Reason, string Status, DateTime CreatedAt,
    string? ActionTaken, string? ModeratorNote, string? ResolvedByName, DateTime? ResolvedAt);
// Resolve a report, optionally banning the reported user in the same step.
public record ResolveReportRequest(bool BanUser, string? Note);
