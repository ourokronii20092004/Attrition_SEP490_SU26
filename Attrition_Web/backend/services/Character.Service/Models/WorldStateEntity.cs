namespace Character.Service.Models;

/// <summary>
/// Quest / world-event progress for a ROOM (host-authoritative, not per character). Key is the
/// composite (SessionId, EventId) where EventId is the questId / objective key.
/// </summary>
public class WorldStateEntity
{
    public Guid SessionId { get; set; }
    public SessionEntity? Session { get; set; }

    // questId / objective key.
    public string EventId { get; set; } = string.Empty;

    // 0 = NotStarted, 1 = Active, 2 = Completed, 3 = Rewarded.
    public short StateValue { get; set; }

    // Objectives done so far (e.g. killed 3 of 5).
    public int Progress { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}