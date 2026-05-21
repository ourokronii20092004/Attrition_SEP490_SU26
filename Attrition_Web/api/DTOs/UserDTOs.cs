namespace Attrition.API.DTOs;

public record UserDto(
    Guid Id,
    string Username,
    string? Email,
    string? DisplayName,
    string Role,
    string? AvatarUrl,       // resolved: AvatarPath ?? GoogleAvatarUrl ?? null
    string? BackgroundUrl,
    string ThemeMode,
    string ThemeAccent,
    string? Bio,
    string AuthProvider,
    DateTime JoinedAt,
    int PostCount,
    int ContributionCount,
    bool MustChangePassword
);
public record UpdateProfileRequest(string? Bio, string? Email);
public record UpdateThemeRequest(string ThemeMode, string ThemeAccent);
public record UserListItem(Guid Id, string Username, string Role, bool IsBanned, DateTime JoinedAt);