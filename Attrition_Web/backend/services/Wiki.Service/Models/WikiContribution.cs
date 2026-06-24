namespace Wiki.Service.Models;

public class WikiContribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public Guid ContributorId { get; set; }
    public string? ContributorName { get; set; }
    public string SuggestedContent { get; set; } = string.Empty;
    public string? ChangeNote { get; set; }
    public string Status { get; set; } = ContributionStatus.Pending;       // Pending | Approved | Rejected
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
}
