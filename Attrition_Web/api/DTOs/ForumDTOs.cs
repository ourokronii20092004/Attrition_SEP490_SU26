namespace Attrition.API.DTOs;

public record ForumCategoryDto(int Id, string Name, string Slug, string Description, int ThreadCount, DateTime? LatestActivity);
public record ForumThreadListDto(Guid Id, string Title, string AuthorName, string? AuthorAvatar,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt, DateTime LastReplyAt);
public record ForumThreadDto(Guid Id, string Title, string CategorySlug, string AuthorName,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt);
public record CreateThreadRequest(int CategoryId, string Title, string Content); // Content = first post
public record ForumPostDto(Guid Id, Guid ThreadId, string AuthorName, string? AuthorAvatar,
    string AuthorRole, string Content, DateTime CreatedAt, DateTime? UpdatedAt,
    int LikeCount, int DislikeCount, string? CurrentUserReaction);
public record CreatePostRequest(string Content);
public record UpdatePostRequest(string Content);
public record ReactRequest(string ReactionType); // "like" or "dislike"
public record PaginatedResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize);
public record ReportPostReq(string Reason);