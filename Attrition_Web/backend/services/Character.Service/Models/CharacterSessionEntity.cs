namespace Character.Service.Models;

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

    // Vital stats (owned value object — stored as columns on this table).
    public VitalStats Vitals { get; set; } = new();

    // Combat stats (owned value object).
    public CombatStats Combat { get; set; } = new();

    // Current position in the scene (owned value object).
    public Position Position { get; set; } = new();

    // Inventory + equipped gear as JSON blobs (client is source of truth).
    public string? InventoryJson { get; set; }
    public string? EquipmentJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
