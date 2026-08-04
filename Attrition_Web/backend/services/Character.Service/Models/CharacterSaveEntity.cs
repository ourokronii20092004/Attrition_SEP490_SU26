namespace Character.Service.Models;

/// <summary>
/// One complete, immutable save file for a character — the full state at a point in time.
///
/// This exists because the two older tables each hold half of what a save file needs:
/// <c>character_snapshots</c> is append-only but thin (level/hp/gold), while
/// <c>character_session</c> is rich but keyed <c>(CharacterId, SessionId)</c>, so each save
/// overwrites the last one for that room. Neither can answer "show me my stats and inventory as
/// they were three saves ago".
///
/// Deliberately a new table rather than a widening of <c>character_snapshots</c>: shipped game
/// builds still POST to <c>/api/characters/snapshot</c>, and that path must keep working untouched.
/// <c>character_session</c> also keeps its job — it is what the game hydrates from on spawn, so it
/// stays an upsert of "current state per room". This table is the history the web displays.
///
/// Rows are capped per character (see <see cref="SaveRetention.MaxPerCharacter"/>); the oldest is
/// pruned on insert so history cannot grow without bound.
/// </summary>
public class CharacterSaveEntity
{
    public long Id { get; set; }

    /// <summary>Logical ref to characters.Id (no FK across the owning aggregate boundary).</summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Room this save was taken in. Null for a save with no room — character creation posts a
    /// snapshot before any session exists — which is why deleting such a save has no live state to
    /// roll back to.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>Join code at capture time, kept even if the room is later deleted.</summary>
    public string? RoomCode { get; set; }

    /// <summary>Scene the party was on, for "where was I?" without joining the room.</summary>
    public string? CurrentScene { get; set; }

    /// <summary>What triggered it: "rest" | "quit" | "create". Mirrors the bulk-save eventType.</summary>
    public string EventType { get; set; } = "rest";

    /// <summary>0 = host, 1 = joining client. Admin-facing; users don't need it.</summary>
    public short PlayerRole { get; set; }

    // ── Progression ───────────────────────────────────────────────────────────
    public int CurrentLevel { get; set; } = 1;
    public int CurrentExp { get; set; }
    public int DeathCount { get; set; }
    public int PlaytimeSeconds { get; set; }
    public bool IsAlive { get; set; } = true;

    /// <summary>JSON int[7] — [MaxHP, MaxMana, MaxStamina, AD, AP, DEF, RES].</summary>
    public string? AllocatedPointsJson { get; set; }

    // ── Stats, reusing the same owned value objects as character_session so the
    //    two tables can be compared field-for-field without a mapping layer. ──
    public VitalStats Vitals { get; set; } = new();
    public CombatStats Combat { get; set; } = new();
    public Position Position { get; set; } = new();

    /// <summary>Inventory blob in the shape PlayerInventory.ExportJson() produces.</summary>
    public string? InventoryJson { get; set; }

    /// <summary>Always null from Unity — equipped gear rides inside InventoryJson.</summary>
    public string? EquipmentJson { get; set; }

    /// <summary>When the save was taken. The newest row is the character's current progress.</summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Retention policy for <see cref="CharacterSaveEntity"/>.</summary>
public static class SaveRetention
{
    /// <summary>
    /// Saves kept per character; the oldest is pruned when a new one arrives. Bounded because the
    /// game saves at every rest point, so an unbounded history would grow for the lifetime of a
    /// character with no upper limit.
    /// </summary>
    public const int MaxPerCharacter = 30;
}
