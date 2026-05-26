namespace Attrition.API.Models;

public class ForumPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;   // Markdown
    public string? Attachments { get; set; } // string, nullable, JSON array
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Reddit-style soft-remove moderation
    public bool IsRemoved { get; set; } = false;
    public string? RemovedReason { get; set; }
    public Guid? RemovedByUserId { get; set; }
    public DateTime? RemovedAt { get; set; }
}