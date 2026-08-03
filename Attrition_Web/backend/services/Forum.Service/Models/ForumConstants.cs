namespace Forum.Service.Models;

public static class ReportStatus
{
    public const string Pending = "Pending";
    public const string Resolved = "Resolved";
    public const string Dismissed = "Dismissed";
}

public static class ReactionType
{
    public const string Like = "like";
    public const string Dislike = "dislike";
}

/// <summary>
/// Notification kinds this service produces. Identity owns the notifications table and has its
/// own copy of these strings; the set is small and deliberately duplicated rather than shared,
/// so Forum needs no project reference on Identity. Keep the values in step with
/// Identity.Service.Models.NotificationType.
/// </summary>
public static class NotifyType
{
    public const string Reply = "reply";
    public const string Mention = "mention";
}
