namespace Identity.Service.Models;

/// <summary>
/// A user-facing notification (forum reply or @mention). Written by other services via the
/// internal API; read/marked-read by the owner via the account API. Polled by the frontend bell.
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who receives this notification.</summary>
    public Guid UserId { get; set; }

    /// <summary>"reply" | "mention" (kept as a string; the set is small and Unity/other producers own it).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Human-readable line, e.g. "Alice replied to your post".</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Where clicking the notification should go (e.g. /forum/{threadId}).</summary>
    public string? Link { get; set; }

    /// <summary>Who triggered it (denormalized snapshot, no cross-schema FK).</summary>
    public string? ActorName { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class NotificationType
{
    public const string Reply = "reply";
    public const string Mention = "mention";
}
