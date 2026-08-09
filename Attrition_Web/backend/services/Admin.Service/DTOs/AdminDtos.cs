namespace Admin.Service.DTOs;

/// <summary>
/// Aggregated dashboard stats. Each numeric field is nullable: null means that
/// service was unavailable when the dashboard was assembled (see UnavailableSources).
/// </summary>
public record AdminStatsDto(
    int? TotalUsers,
    int? TotalWikiArticles,
    int? PendingContributions,
    int? TotalForumThreads,
    int? TotalForumPosts,
    int? RemovedPosts,
    int? TotalEnemies,
    int? TotalItems,
    int? TotalSkills,
    int? TotalAssets,
    int? TotalMusicAlbums,
    int? TotalMusicTracks,
    int? TotalCharacters,
    int? TotalRooms,
    int? TotalCoopRooms,
    IReadOnlyList<string> UnavailableSources
);