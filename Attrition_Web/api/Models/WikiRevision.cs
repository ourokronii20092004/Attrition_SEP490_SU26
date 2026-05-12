namespace Attrition.API.Models;

public class WikiRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? EditedById { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string? ChangeNote { get; set; }
}