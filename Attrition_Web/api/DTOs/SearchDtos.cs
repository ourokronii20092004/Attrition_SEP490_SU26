namespace Attrition.API.DTOs;

public record GlobalSearchResponse(
    IEnumerable<SearchWikiResultDto> Wiki,
    IEnumerable<SearchUserResultDto> Users,
    IEnumerable<SearchPostResultDto> Posts
);

public record SearchWikiResultDto(Guid Id, string Title, string Slug);
public record SearchUserResultDto(Guid Id, string Username, string? DisplayName, string? AvatarPath, string? GoogleAvatarUrl, string Role);
public record SearchPostResultDto(Guid Id, Guid ThreadId, string Content, string Title, DateTime CreatedAt);
