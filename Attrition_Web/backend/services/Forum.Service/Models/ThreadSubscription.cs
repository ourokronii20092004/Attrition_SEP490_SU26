namespace Forum.Service.Models;

/// <summary>
/// One user's relationship to one thread. A row means "you have some stated interest here":
/// either following it, or explicitly muting it.
///
/// Muting has to be recorded rather than inferred from a missing row, because replying
/// auto-subscribes you — so "no row" would silently re-follow a thread the moment you posted
/// again, and the mute would never stick.
/// </summary>
public class ThreadSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When true the user has opted out of this thread and gets no reply notifications for it,
    /// including as the thread's author or as the author of a post being replied to.
    /// </summary>
    public bool IsMuted { get; set; }
}