using Character.Service.Models;

namespace Character.Service.Repositories;

public interface ISessionRepository
{
    // Rooms owned by a host (menu list). Ordered newest-played first.
    Task<List<SessionEntity>> GetByOwnerAsync(Guid ownerId);

    // Full room load: session + character progress + world state.
    Task<SessionEntity?> GetDetailAsync(Guid sessionId);

    Task<SessionEntity?> GetByRoomCodeAsync(string roomCode);

    Task<SessionEntity> AddAsync(SessionEntity session);
    Task UpdateAsync(SessionEntity session);

    // True if no room currently uses this code (codes are fixed & unique per room).
    Task<bool> RoomCodeIsFreeAsync(string roomCode);

    // ─── character_session upsert/read ───
    Task<CharacterSessionEntity?> GetCharacterSessionAsync(Guid characterId, Guid sessionId);
    Task UpsertCharacterSessionAsync(CharacterSessionEntity entity);

    // ─── world_state upsert ───
    Task<WorldStateEntity?> GetWorldStateAsync(Guid sessionId, string eventId);
    Task UpsertWorldStateAsync(WorldStateEntity entity);
}
