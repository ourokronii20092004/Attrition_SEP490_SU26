namespace Forum.Service.DTOs;

public record ForumCategoryDto(int Id, string Name, string Slug, string Description, int ThreadCount, DateTime? LatestActivity);

public record ForumThreadListDto(Guid Id, string Title, Guid AuthorId, string AuthorName, string? AuthorAvatar,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt, DateTime LastReplyAt);

public record ForumThreadDto(Guid Id, string Title, string CategorySlug, Guid AuthorId, string AuthorName,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt, string Content,
    IReadOnlyList<string> Attachments, string? AuthorAvatar, string AuthorRole, DateTime? UpdatedAt,
    int LikeCount, int DislikeCount, string? CurrentUserReaction,
    // Whether the signed-in viewer has muted this thread. Always false for anonymous viewers,
    // who receive no notifications and so have nothing to mute.
    bool IsMuted = false);

public record ForumPostDto(Guid Id, Guid ThreadId, Guid? ParentPostId, int Depth, Guid AuthorId, string AuthorName, string? AuthorAvatar,
    string AuthorRole, string Content, IReadOnlyList<string> Attachments, DateTime CreatedAt, DateTime? UpdatedAt,
    int LikeCount, int DislikeCount, string? CurrentUserReaction);

// A user's forum reply for their public profile (Twitter-style "Replies" tab): one of their posts
// that isn't a thread's opening post, carrying just enough thread context to link + preview it.
public record UserReplyDto(Guid PostId, Guid ThreadId, string ThreadTitle, string Content, DateTime CreatedAt,
    int LikeCount, int DislikeCount);

public record CreateThreadRequest(int CategoryId, string Title, string Content);
public record CreatePostRequest(string Content, Guid? ParentPostId = null, List<string>? Attachments = null);
public record UpdatePostRequest(string Content);
public record ReactRequest(string ReactionType);       // like | dislike
public record ReportPostReq(string Reason);
// Mute (or un-mute) a thread. Explicit state rather than a toggle, so a retried or double-clicked
// request can't leave the user on the opposite setting from the one they chose.
public record MuteThreadReq(bool Muted);

// Category management (admin)
public record ForumCategoryRequest(string Name, string? Description);

// Moderation views
public record AdminForumThreadDto(Guid Id, string Title, bool IsPinned, bool IsLocked, int ReplyCount,
    DateTime CreatedAt, DateTime LastReplyAt, string? AuthorName, bool IsRemoved);

public record AdminForumPostDto(Guid Id, Guid ThreadId, string Content, DateTime CreatedAt, DateTime? UpdatedAt,
    bool IsRemoved, string? RemovedReason, DateTime? RemovedAt, string? AuthorName, string? RemovedByName);

public record AdminPostReportDto(Guid Id, Guid PostId, string PostContent, string AuthorName, string ReporterName,
    string Reason, string Status, DateTime CreatedAt);

public record RemovePostRequest(string Reason);

// Search aggregator projection
public record ForumPostSearchDto(Guid Id, Guid ThreadId, string ThreadTitle, string Snippet);