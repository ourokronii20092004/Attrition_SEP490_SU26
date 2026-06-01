namespace Identity.Service.DTOs;

// ─── Auth requests/responses ───
public record RegisterRequest(string Username, string Password, string? Email);
public record GoogleAuthRequest(string Code, string RedirectUri);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
public record RefreshRequest(string? RefreshToken = null);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record VerifyEmailRequest(string Token);

// ─── Account requests ───
public record UpdateProfileRequest(string? Bio, string? Email, bool? NotifyOnReply, bool? NotifyOnMention, string? DisplayName);
public record UpdateThemeRequest(string ThemeMode, string ThemeAccent);
public record SetPasswordRequest(string NewPassword);
public record UpdateEmailRequest(string NewEmail, string CurrentPassword);
// Account deletion (PROB-4): a confirmed, 90-day-recoverable flow rather than an instant wipe.
public record ConfirmDeletionRequest(string Token);

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
    bool NotifyOnMention
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

// ─── Admin actions ───
public record ChangeRoleRequest(string Role);
public record AdminResetPasswordRequest(string NewPassword);

// ─── User reports (QOLF-9) ───
public record ReportUserRequest(string Reason);
public record AdminUserReportDto(
    Guid Id, Guid ReportedUserId, string ReportedUserName, string ReporterName,
    string Reason, string Status, DateTime CreatedAt);

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
public record SessionStatusDto(Guid UserId, string Username, string Role, bool IsBanned);

// ─── Notifications ───
public record NotificationDto(Guid Id, string Type, string Message, string? Link, string? ActorName,
    bool IsRead, DateTime CreatedAt);

/// <summary>Service-to-service create payload (Forum → Identity on reply/mention). Target the
/// recipient by UserId (replies — Forum knows the parent author's id) OR Username (@mentions —
/// Identity resolves it, since it owns users). Exactly one is required.</summary>
public record CreateNotificationRequest(string Type, string Message, string? Link, string? ActorName,
    Guid? UserId = null, string? Username = null);
