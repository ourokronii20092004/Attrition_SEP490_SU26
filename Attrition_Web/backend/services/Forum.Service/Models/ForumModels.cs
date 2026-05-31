namespace Forum.Service.Models;

public static class ReportStatus
{
    public const string Pending = "Pending";
    public const string Resolved = "Resolved";
    public const string Dismissed = "Dismissed";
}

public static class ReactionType
{
    public const string Like = "like";
    public const string Dislike = "dislike";
}

public class ForumCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class ForumThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;

    // Author refs: plain Guid + denormalized snapshot (no cross-schema FK).
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorAvatar { get; set; }

    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReplyAt { get; set; } = DateTime.UtcNow;
    public int ReplyCount { get; set; }
}

public class ForumPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }

    // Reddit-style nesting: a reply points at its parent post (null = top-level reply to the
    // thread). Depth is denormalized for cheap indent rendering and to cap nesting.
    public Guid? ParentPostId { get; set; }
    public int Depth { get; set; }

    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorAvatar { get; set; }
    public string AuthorRole { get; set; } = "User";

    public string Content { get; set; } = string.Empty;     // Markdown
    public string? Attachments { get; set; }                  // JSON array
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Soft-remove moderation
    public bool IsRemoved { get; set; }
    public string? RemovedReason { get; set; }
    public Guid? RemovedByUserId { get; set; }
    public string? RemovedByName { get; set; }
    public DateTime? RemovedAt { get; set; }
}

public class ForumReaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public string ReactionType { get; set; } = Models.ReactionType.Like;        // like | dislike
}

public class ThreadSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
}

public class PostReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = ReportStatus.Pending;           // Pending | Resolved | Dismissed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
