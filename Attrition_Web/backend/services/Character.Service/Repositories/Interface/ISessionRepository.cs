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

    // ── Save files (character_saves) ─────────────────────────────────────────────────────────
    // Append a save to the bulk graph so it commits in the same transaction as the live-state
    // upsert: a save is one atomic thing, never half-written.
    void AddCharacterSave(CharacterSaveEntity entity);

    // Room-level snapshot (bosses/quests/fog) captured with the same bulk save.
    void AddRoomStateSave(RoomStateSaveEntity entity);
    Task<List<long>> GetRoomStateIdsBeyondCapAsync(Guid sessionId, int keep);
    void RemoveRoomStateSaves(List<long> ids);
    // The room snapshot taken at (or just before) a given moment — what a world rollback restores.
    Task<RoomStateSaveEntity?> GetRoomStateAtOrBeforeAsync(Guid sessionId, DateTime capturedAt);

    // Replace the room's live world state + fog with a snapshot's, in one transaction. Rows added
    // after the snapshot are removed, not merged: a boss defeated later must not survive a rollback.
    Task RestoreRoomStateAsync(Guid sessionId, RoomStateSaveEntity snapshot);

    // ── Admin: who played with whom ──────────────────────────────────────────────────────────
    // Paged rooms with their party, so an admin can see co-op pairings without opening each room.
    Task<(List<SessionEntity> Items, int Total)> GetRoomsPagedAsync(int page, int pageSize);

    /// <summary>Total rooms and how many of them are co-op, for the admin dashboard counters.</summary>
    Task<(int Rooms, int Multiplayer)> GetRoomStatsAsync();
    Task<List<RoomStateSaveEntity>> GetRoomStateHistoryAsync(Guid sessionId, int limit);

    // Oldest-first ids beyond the retention cap, so the caller can prune them in the same commit.
    Task<List<long>> GetSaveIdsBeyondCapAsync(Guid characterId, int keep);
    void RemoveCharacterSaves(List<long> ids);

    // Paged history, newest first. Returns (page, totalCount) so the UI can show real page counts.
    Task<(List<CharacterSaveEntity> Items, int Total)> GetSavesAsync(Guid characterId, int page, int pageSize);

    Task<CharacterSaveEntity?> GetSaveAsync(long saveId);

    // How many saves this character has — used to refuse deleting the last one.
    Task<int> CountSavesAsync(Guid characterId);

    // The newest save excluding one id: what live state rolls back to when the newest is deleted.
    Task<CharacterSaveEntity?> GetNewestSaveExcludingAsync(Guid characterId, long excludeSaveId);

    // Delete one save and, when it was the newest, rewrite live state from the previous one — in a
    // single transaction, so a character can never be left between two saves.
    Task<bool> DeleteSaveAndRollBackAsync(CharacterSaveEntity save, CharacterSaveEntity? rollBackTo);
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
