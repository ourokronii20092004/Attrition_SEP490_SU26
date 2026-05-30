namespace Character.Service.Models;

/// <summary>
/// A player-owned game character. OwnerId is the Identity user GUID (no FK across services).
/// The web shows these read-only; the Unity client is the source of truth and pushes snapshots.
/// </summary>
public class CharacterEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Archetype { get; set; } = string.Empty;  // class/build, e.g. "Vanguard", "Rogue"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Snapshot history — appended on every save/quit the client reports.
    public List<CharacterSnapshot> Snapshots { get; set; } = new();
}

/// <summary>
/// One point-in-time status of a character, appended on save/quit. The most recent row
/// (max CapturedAt) is the character's current state; older rows form a progression timeline.
/// </summary>
public class CharacterSnapshot
{
    public int Level { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Gold { get; set; }
    public bool IsAlive { get; set; } = true;

    // Co-op session / room the character was in when this snapshot was taken. Null = not in a room.
    public string? RoomCode { get; set; }

    // What triggered this snapshot: "save" | "quit" | "death" | "checkpoint".
    public string EventType { get; set; } = "save";

    // Total playtime for this character in seconds, as reported by the client.
    public int PlaytimeSeconds { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
