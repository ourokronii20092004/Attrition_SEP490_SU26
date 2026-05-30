namespace Wiki.Service.Models;

public class WikiCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
}

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

    public string Status { get; set; } = "Published";     // "Draft" | "Published"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class WikiRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? EditedById { get; set; }
    public string? EditedByName { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string? ChangeNote { get; set; }
}

public class WikiContribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public Guid ContributorId { get; set; }
    public string? ContributorName { get; set; }
    public string SuggestedContent { get; set; } = string.Empty;
    public string? ChangeNote { get; set; }
    public string Status { get; set; } = "Pending";       // Pending | Approved | Rejected
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
}
