namespace Forum.Service.Models;

/// <summary>
/// Soft-delete moderation state for a forum post.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class ModerationInfo
{
    public bool IsRemoved { get; set; }
    public string? Reason { get; set; }
    public Guid? ByUserId { get; set; }
    public string? ByName { get; set; }
    public DateTime? At { get; set; }
}