namespace Search.Service.DTOs;

public record SearchWikiResultDto(Guid Id, string Title, string Slug, string CategorySlug);
public record SearchUserResultDto(Guid Id, string Username, string? DisplayName, string? AvatarUrl);
public record SearchPostResultDto(Guid Id, Guid ThreadId, string ThreadTitle, string Snippet);
public record SearchEnemyResultDto(string EnemyId, string Name, string Tier);

public record GlobalSearchResponse(
    IReadOnlyList<SearchWikiResultDto> Wiki,
    IReadOnlyList<SearchUserResultDto> Users,
    IReadOnlyList<SearchPostResultDto> Posts,
    IReadOnlyList<SearchEnemyResultDto> Enemies,
    IReadOnlyList<string> DegradedSources   // names of services that failed this query
);
