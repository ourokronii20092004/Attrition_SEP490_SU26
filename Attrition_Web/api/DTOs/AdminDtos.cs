namespace Attrition.API.DTOs;

public record WikiCategoryRequest(string Name, string? Description, string? IconUrl);
public record RemovePostRequest(string Reason);

public record EnemyCreateRequest(
    string EnemyId,
    string Name,
    string Tier,
    string? SpawnBiome,
    int Hp,
    int Ad,
    int Ap,
    int Def,
    int Res,
    float AttackSpeed,
    bool IsRanged,
    int ExpReward,
    int GoldReward
);

public record EnemyUpdateRequest(
    string Name,
    string Tier,
    string? SpawnBiome,
    int Hp,
    int Ad,
    int Ap,
    int Def,
    int Res,
    float AttackSpeed,
    bool IsRanged,
    int ExpReward,
    int GoldReward
);

public record AdminUserDto(
    Guid Id,
    string Username,
    string? Email,
    string? DisplayName,
    string Role,
    string? AvatarPath,
    string? GoogleAvatarUrl,
    string AuthProvider,
    bool IsBanned,
    DateTime JoinedAt,
    DateTime? LastLoginAt,
    int PostCount,
    int ContributionCount
);

public record AdminWikiArticleDto(
    Guid Id,
    string Title,
    string Slug,
    string? CategoryName,
    string? AuthorName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AdminWikiCategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? IconUrl,
    int ArticleCount
);

public record AdminForumThreadDto(
    Guid Id,
    string Title,
    bool IsPinned,
    bool IsLocked,
    int ReplyCount,
    DateTime CreatedAt,
    DateTime LastReplyAt,
    string? CategoryName,
    string? AuthorName
);

public record AdminTogglePinResponse(bool IsPinned);
public record AdminToggleLockResponse(bool IsLocked);

public record AdminForumPostDto(
    Guid Id,
    Guid ThreadId,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsRemoved,
    string? RemovedReason,
    DateTime? RemovedAt,
    string? AuthorName,
    string? ThreadTitle,
    string? RemovedByName
);

public record AdminStatsDto(
    int TotalUsers,
    int TotalWikiArticles,
    int TotalForumThreads,
    int TotalForumPosts,
    int PendingContributions,
    int TotalMusicAlbums,
    int TotalMusicTracks,
    int RemovedPosts
);

public record AdminPostReportDto(
    Guid Id,
    Guid PostId,
    string PostContent,
    string AuthorName,
    string ReporterName,
    string Reason,
    string Status,
    DateTime CreatedAt
);

public record AssetDto(
    Guid Id,
    string FileName,
    string FilePath,
    string AssetType,
    string MimeType,
    long FileSize,
    string? Title,
    string? Description,
    string? Tags,
    string? UploadedBy,
    DateTime UploadedAt,
    DateTime? UpdatedAt
);

public record UpdateAssetReq(string? Title, string? Description, string? Tags, string? AssetType);


