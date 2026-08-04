using Character.Service.Models;

namespace Character.Service.Repositories.Interface;

public interface ISessionRepository
{
    // Rooms owned by a host (menu list). Ordered newest-played first.
    Task<List<SessionEntity>> GetByOwnerAsync(Guid ownerId);

    // Full room load: session + character progress + world state.
    Task<SessionEntity?> GetDetailAsync(Guid sessionId);

    Task<SessionEntity?> GetByRoomCodeAsync(string roomCode);

    // Light owner lookup for the ownership guard — no Includes. Null if the room doesn't exist.
    Task<Guid?> GetOwnerIdAsync(Guid sessionId);

    // Display names for the given character ids (room detail shows names, not GUIDs). Missing ids
    // are simply absent from the result.
    Task<Dictionary<Guid, (string Name, string Archetype)>> GetCharacterNamesAsync(List<Guid> characterIds);

    Task<SessionEntity> AddAsync(SessionEntity session);
    Task UpdateAsync(SessionEntity session);

    // True if no room currently uses this code (codes are fixed & unique per room).
    Task<bool> RoomCodeIsFreeAsync(string roomCode);

    Task<CharacterSessionEntity?> GetCharacterSessionAsync(Guid characterId, Guid sessionId);
    Task UpsertCharacterSessionAsync(CharacterSessionEntity entity);

    Task<WorldStateEntity?> GetWorldStateAsync(Guid sessionId, string eventId);
    Task UpsertWorldStateAsync(WorldStateEntity entity);

    // ── Consolidated bulk save ───────────────────────────────────────────────────────────────
    // Load every row one push touches, TRACKED, in a handful of round-trips. The service mutates
    // the returned graph and calls SaveChangesAsync once, so the whole party commits atomically.
    Task<BulkSaveGraph> LoadForBulkAsync(Guid sessionId, List<Guid> characterIds, List<string> eventIds);
    void AddCharacterSession(CharacterSessionEntity entity);
    void AddWorldState(WorldStateEntity entity);
    Task SaveChangesAsync();

    // Delete a room entirely (session + all child rows).
    Task<bool> DeleteSessionAsync(Guid sessionId);
}

/// <summary>
/// Everything a single bulk save reads or writes, loaded tracked so the service can mutate in
/// place and commit with one SaveChangesAsync. Characters carries the global character rows
/// (for snapshot history + ownership verification), keyed separately from CharacterSessions
/// which is the per-room progress.
/// </summary>
public record BulkSaveGraph(
    SessionEntity? Session,
    List<CharacterSessionEntity> CharacterSessions,
    List<WorldStateEntity> WorldStates,
    List<CharacterEntity> Characters);
