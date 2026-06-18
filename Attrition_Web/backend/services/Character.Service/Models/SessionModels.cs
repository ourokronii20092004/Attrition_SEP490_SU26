namespace Character.Service.Models;

/// <summary>
/// A persistent co-op room (a saved "journey"). The host creates one; it survives across
/// play sessions and keeps a FIXED RoomCode so the host can re-open the same room and invite
/// the same friend back. Solo play is NOT stored here — it stays in a local JSON save on the
/// client. Therefore IsMultiplayer is always true for rows that exist.
/// </summary>
public class SessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Identity user GUID of the host who owns this room. Logical ref only (no cross-service FK).
    public Guid OwnerId { get; set; }

    // Short code clients type to join. Fixed for the life of the room (unique).
    public string RoomCode { get; set; } = string.Empty;

    // Display name the host gave the journey.
    public string Name { get; set; } = string.Empty;

    public bool IsMultiplayer { get; set; } = true;

    // Total seconds played across the whole journey (sum the host reports).
    public int PlayTimeSeconds { get; set; }

    // Scene/map the party was last in, so a re-open loads the right level.
    public string? CurrentScene { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    public List<CharacterSessionEntity> Characters { get; set; } = new();
    public List<WorldStateEntity> WorldStates { get; set; } = new();
}

/// <summary>
/// One character's progress WITHIN one room. Stats/level live here (not on the character)
/// because the same character can have different progress per journey. Key is the composite
/// (CharacterId, SessionId). Inventory/equipment are stored as JSON blobs (decision: keep the
/// blob shape the client already sends, rather than normalizing into per-slot rows).
/// </summary>
public class CharacterSessionEntity
{
    // Logical ref to characters.Id. Both host and client characters live in the same table.
    public Guid CharacterId { get; set; }

    public Guid SessionId { get; set; }
    public SessionEntity? Session { get; set; }

    // 0 = host, 1 = client.
    public short PlayerRole { get; set; }

    public int CurrentLevel { get; set; } = 1;
    public int CurrentExp { get; set; }

    // 7 self-allocated points [hp,mana,sta,ad,ap,def,res] as a JSON array.
    public string? AllocatedPointsJson { get; set; }

    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int MaxMana { get; set; }
    public int CurrentMana { get; set; }
    public int MaxStamina { get; set; }

    // Healing-flask cap the player has unlocked.
    public int PotionMaxFlasks { get; set; }

    public float AttackSpeed { get; set; }

    // Current position in the scene.
    public float PosX { get; set; }
    public float PosY { get; set; }

    // Checkpoint last rested at.
    public string? LastRestPointId { get; set; }

    // Inventory + equipped gear as JSON blobs (client is source of truth).
    public string? InventoryJson { get; set; }
    public string? EquipmentJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

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
