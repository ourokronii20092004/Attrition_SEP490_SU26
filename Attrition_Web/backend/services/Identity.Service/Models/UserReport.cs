namespace Identity.Service.Models;

/// <summary>
/// QOLF-9: a user-against-user report that admins review before acting. Mirrors the forum's
/// PostReport (Pending → Resolved/Dismissed), but the subject is a user, not a post.
/// </summary>
public class UserReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportedUserId { get; set; }
    public string? ReportedUserName { get; set; }
    public Guid ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending | Resolved | Dismissed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
