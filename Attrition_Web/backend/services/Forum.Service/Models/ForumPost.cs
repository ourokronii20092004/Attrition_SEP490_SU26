namespace Forum.Service.Models;

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

    // Soft-remove moderation (owned value object — stored as columns on this table).
    public ModerationInfo Moderation { get; set; } = new();
}
