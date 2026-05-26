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
    bool MustChangePassword,
    bool IsEmailVerified,
    string? PendingEmail,
    bool NotifyOnReply,
    bool NotifyOnMention
);
public record UpdateProfileRequest(string? Bio, string? Email, bool? NotifyOnReply, bool? NotifyOnMention);
public record UpdateThemeRequest(string ThemeMode, string ThemeAccent);
public record UserListItem(Guid Id, string Username, string Role, bool IsBanned, DateTime JoinedAt);

public record UserPostDto(Guid Id, Guid ThreadId, string Content, DateTime CreatedAt, string ThreadTitle);
public record UserContributionDto(Guid Id, Guid ArticleId, string? Notes, DateTime CreatedAt, string Status, string ArticleTitle, string ArticleSlug);
public record SetPasswordRequest(string NewPassword);
public record UpdateEmailRequest(string NewEmail, string CurrentPassword);
public record GameBulkData(
    System.Collections.Generic.List<Attrition.API.Models.Item> Items,
    System.Collections.Generic.List<Attrition.API.Models.Enemy> Enemies,
    System.Collections.Generic.List<Attrition.API.Models.Skill> Skills,
    System.Collections.Generic.List<Attrition.API.Models.Level> Levels,
    System.Collections.Generic.List<Attrition.API.Models.SpawnPoint> SpawnPoints,
    System.Collections.Generic.List<Attrition.API.Models.GameConfig> GameConfigs
);