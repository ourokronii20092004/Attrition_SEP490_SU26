namespace Forum.Service.DTOs;

public record ForumCategoryDto(int Id, string Name, string Slug, string Description, int ThreadCount, DateTime? LatestActivity);

public record ForumThreadListDto(Guid Id, string Title, Guid AuthorId, string AuthorName, string? AuthorAvatar,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt, DateTime LastReplyAt);

public record ForumThreadDto(Guid Id, string Title, string CategorySlug, Guid AuthorId, string AuthorName,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt);

public record ForumPostDto(Guid Id, Guid ThreadId, Guid AuthorId, string AuthorName, string? AuthorAvatar,
    string AuthorRole, string Content, DateTime CreatedAt, DateTime? UpdatedAt,
    int LikeCount, int DislikeCount, string? CurrentUserReaction);

public record CreateThreadRequest(int CategoryId, string Title, string Content);
public record CreatePostRequest(string Content);
public record UpdatePostRequest(string Content);
public record ReactRequest(string ReactionType);       // like | dislike
public record ReportPostReq(string Reason);

// Category management (admin)
public record ForumCategoryRequest(string Name, string? Description);

// Moderation views
public record AdminForumThreadDto(Guid Id, string Title, bool IsPinned, bool IsLocked, int ReplyCount,
    DateTime CreatedAt, DateTime LastReplyAt, string? AuthorName);

public record AdminForumPostDto(Guid Id, Guid ThreadId, string Content, DateTime CreatedAt, DateTime? UpdatedAt,
    bool IsRemoved, string? RemovedReason, DateTime? RemovedAt, string? AuthorName, string? RemovedByName);

public record AdminPostReportDto(Guid Id, Guid PostId, string PostContent, string AuthorName, string ReporterName,
    string Reason, string Status, DateTime CreatedAt);

public record RemovePostRequest(string Reason);

// Search aggregator projection
public record ForumPostSearchDto(Guid Id, Guid ThreadId, string ThreadTitle, string Snippet);
