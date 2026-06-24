namespace Forum.Service.Models;

public class ForumThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;

    // Author refs: plain Guid + denormalized snapshot (no cross-schema FK).
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorAvatar { get; set; }

    // QOLF-3b: when set, this thread is the comment thread for a wiki article (not a normal forum
    // thread). Such threads are hidden from the forum listing/search and reached via the article.
    public Guid? WikiArticleId { get; set; }

    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReplyAt { get; set; } = DateTime.UtcNow;
    public int ReplyCount { get; set; }
}
