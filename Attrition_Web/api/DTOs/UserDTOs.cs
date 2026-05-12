namespace Attrition.API.DTOs;

public record UserDto(Guid Id, string Username, string Role, string? AvatarUrl,
    string? Bio, DateTime JoinedAt, int PostCount, int ContributionCount, bool MustChangePassword);
public record UpdateProfileRequest(string? Bio);
public record UserListItem(Guid Id, string Username, string Role, bool IsBanned, DateTime JoinedAt);