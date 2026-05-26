using System;

namespace Attrition.API.Models;

public class PostReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid PostId { get; set; }
    public ForumPost Post { get; set; } = null!;
    
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Resolved, Dismissed
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
