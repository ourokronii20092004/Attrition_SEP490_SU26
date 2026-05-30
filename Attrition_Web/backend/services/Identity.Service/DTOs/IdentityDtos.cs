namespace Identity.Service.DTOs;

// ─── Auth requests/responses ───
public record RegisterRequest(string Username, string Password, string? Email);
public record GoogleAuthRequest(string Code, string RedirectUri);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record VerifyEmailRequest(string Token);

// ─── Account requests ───
public record UpdateProfileRequest(string? Bio, string? Email, bool? NotifyOnReply, bool? NotifyOnMention, string? DisplayName);
public record UpdateThemeRequest(string ThemeMode, string ThemeAccent);
public record SetPasswordRequest(string NewPassword);
public record UpdateEmailRequest(string NewEmail, string CurrentPassword);

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

public record UserListItem(Guid Id, string Username, string Role, bool IsBanned, DateTime JoinedAt);

// ─── Admin actions ───
public record ChangeRoleRequest(string Role);
public record AdminResetPasswordRequest(string NewPassword);

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
