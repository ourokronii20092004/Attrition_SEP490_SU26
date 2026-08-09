namespace Forum.Service.Models;

public class ForumPost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A root post owns the discussion. Replies point to it and may also point to another reply.
    public Guid? RootPostId { get; set; }

    public Guid? ParentPostId { get; set; }
    public int Depth { get; set; }

    // Root-only discussion metadata; null on replies. Wiki roots intentionally have no category.
    public int? CategoryId { get; set; }

    public string? Title { get; set; }
    public Guid? WikiArticleId { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public DateTime LastReplyAt { get; set; } = DateTime.UtcNow;
    public int ReplyCount { get; set; }

    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorAvatar { get; set; }
    public string AuthorRole { get; set; } = "User";
    public string Content { get; set; } = string.Empty;
    public string? Attachments { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ModerationInfo Moderation { get; set; } = new();
}