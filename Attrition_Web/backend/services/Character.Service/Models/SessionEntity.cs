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

    // Fog-of-war cells the party has revealed, as a JSON array of "scene:cellX:cellY" keys.
    // Room-level because co-op players explore one shared map. Null = nothing revealed yet.
    public string? FogJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    public List<CharacterSessionEntity> Characters { get; set; } = new();
    public List<WorldStateEntity> WorldStates { get; set; } = new();
}