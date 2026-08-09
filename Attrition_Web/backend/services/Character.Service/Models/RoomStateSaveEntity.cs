namespace Character.Service.Models;

/// <summary>
/// The room's shared progress at a point in time — bosses defeated, quest states, discovered rest
/// points and fog — captured alongside the character saves written in the same transaction.
///
/// World state is keyed <c>(SessionId, EventId)</c>: it belongs to the *room*, not to a character,
/// so every player in the room shares it. <see cref="CharacterSaveEntity"/> therefore cannot carry
/// it, and without this table there is nothing for a "roll the world back" to restore from.
///
/// Restoring one of these rewrites progress for **everyone** in the room, which is why it is
/// offered only to the room owner and only as an explicit opt-in.
///
/// Stored as JSON rather than a child table: it is written once, read whole, and never queried by
/// individual event — the shape the game already sends is the shape the web replays.
/// </summary>
public class RoomStateSaveEntity
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    /// <summary>
    /// Matches the <c>CapturedAt</c> of the character saves from the same bulk save, so a character
    /// save and the room state around it can be paired without another key.
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>What triggered it — mirrors the bulk-save eventType ("rest" | "quit").</summary>
    public string EventType { get; set; } = "rest";

    /// <summary>Scene the party was on, for display without joining the room.</summary>
    public string? CurrentScene { get; set; }

    /// <summary>
    /// JSON array of <c>{ eventId, stateValue, progress }</c> — every world_state row for the room
    /// at capture time. Restoring replaces the live rows with exactly this set.
    /// </summary>
    public string? WorldStatesJson { get; set; }

    /// <summary>Copy of the room's fog at capture time ("scene:cellX:cellY" keys).</summary>
    public string? FogJson { get; set; }

    /// <summary>Total playtime for the room, for the timeline.</summary>
    public int PlayTimeSeconds { get; set; }
}