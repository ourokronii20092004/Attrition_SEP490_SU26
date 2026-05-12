namespace Attrition.API.Models;

public class ForumThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public bool IsPinned { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReplyAt { get; set; } = DateTime.UtcNow;
    public int ReplyCount { get; set; } = 0;
}