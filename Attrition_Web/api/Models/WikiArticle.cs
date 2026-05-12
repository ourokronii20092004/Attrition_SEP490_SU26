namespace Attrition.API.Models;

public class WikiArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Content { get; set; } = string.Empty;   // Markdown
    public Guid? CreatedById { get; set; }
    public Guid? LastEditedById { get; set; }
    public string Status { get; set; } = "Published";     // "Draft" or "Published"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}