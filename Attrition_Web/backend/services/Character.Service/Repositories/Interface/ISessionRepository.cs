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

    Task<SessionEntity> AddAsync(SessionEntity session);
    Task UpdateAsync(SessionEntity session);

    // True if no room currently uses this code (codes are fixed & unique per room).
    Task<bool> RoomCodeIsFreeAsync(string roomCode);

    Task<CharacterSessionEntity?> GetCharacterSessionAsync(Guid characterId, Guid sessionId);
    Task UpsertCharacterSessionAsync(CharacterSessionEntity entity);

    Task<WorldStateEntity?> GetWorldStateAsync(Guid sessionId, string eventId);
    Task UpsertWorldStateAsync(WorldStateEntity entity);

    // Delete a room entirely (session + all child rows).
    Task<bool> DeleteSessionAsync(Guid sessionId);
}
