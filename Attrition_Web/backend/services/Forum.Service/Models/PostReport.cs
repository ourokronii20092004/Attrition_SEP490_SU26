namespace Forum.Service.Models;

public class PostReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = ReportStatus.Pending;           // Pending | Resolved | Dismissed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
