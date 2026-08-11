namespace Search.Service.DTOs;

public record SearchWikiResultDto(Guid Id, string Title, string Slug, string CategorySlug);
public record SearchUserResultDto(Guid Id, string Username, string? DisplayName, string? AvatarUrl);
public record SearchPostResultDto(Guid Id, Guid ThreadId, string ThreadTitle, string Snippet);
public record SearchEnemyResultDto(string EnemyId, string Name, string Tier);
public record SearchItemResultDto(string ItemId, string Name, string Category, string Rarity, string? ImageUrl);
public record SearchSkillResultDto(string SkillId, string Name, string Element, string Rarity, string? ImageUrl);

public record GlobalSearchResponse(
    IReadOnlyList<SearchWikiResultDto> Wiki,
    IReadOnlyList<SearchUserResultDto> Users,
    IReadOnlyList<SearchPostResultDto> Posts,
    IReadOnlyList<SearchEnemyResultDto> Enemies,
    IReadOnlyList<SearchItemResultDto> Items,
    IReadOnlyList<SearchSkillResultDto> Skills,
    IReadOnlyList<string> DegradedSources   // names of services that failed this query
)
{
    /// <summary>
    /// A response with no hits. Callers use this instead of listing every bucket by hand, so adding
    /// a result kind can't leave a caller silently constructing the wrong arity.
    /// </summary>
    public static GlobalSearchResponse Empty() => new(
        Array.Empty<SearchWikiResultDto>(), Array.Empty<SearchUserResultDto>(),
        Array.Empty<SearchPostResultDto>(), Array.Empty<SearchEnemyResultDto>(),
        Array.Empty<SearchItemResultDto>(), Array.Empty<SearchSkillResultDto>(),
        Array.Empty<string>());
}

/// <summary>Lightweight autocomplete suggestion: a label, its kind, and where selecting it goes.</summary>
public record SearchSuggestionDto(string Label, string Type, string Url);