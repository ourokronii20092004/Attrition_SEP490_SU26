namespace Wiki.Service.Models;

public class WikiArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Content { get; set; } = string.Empty;   // Markdown

    // Author refs: plain Guid + denormalized snapshot (no cross-schema FK to identity).
    public Guid? CreatedById { get; set; }

    public string? CreatedByName { get; set; }
    public Guid? LastEditedById { get; set; }
    public string? LastEditedByName { get; set; }

    public string Status { get; set; } = ArticleStatus.Published;     // "Draft" | "Published"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}