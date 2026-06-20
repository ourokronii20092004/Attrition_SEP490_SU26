using BuildingBlocks.Contracts;
using Character.Service.DTOs;

namespace Character.Service.Services;

public interface ISessionService
{
    // Player-facing reads (JWT). A host sees only rooms they own.
    Task<List<SessionSummaryDto>> GetByOwnerAsync(Guid ownerId);
    Task<SessionDetailDto?> GetDetailAsync(Guid sessionId);
    Task<SessionDetailDto?> GetByRoomCodeAsync(string roomCode);

    // Game-client ingestion (internal). Create-or-reopen a room with a fixed unique code.
    Task<ApiResponse<SessionDetailDto>> CreateOrReopenAsync(CreateSessionRequest request);
    Task<ApiResponse<SessionDetailDto>> UpdateMetaAsync(UpdateSessionRequest request);

    // Upsert one character's progress / world state for a room.
    Task<ApiResponse<CharacterSessionDto>> SaveCharacterSessionAsync(SaveCharacterSessionRequest request);
    Task<ApiResponse<WorldStateDto>> SaveWorldStateAsync(SaveWorldStateRequest request);

    // Delete a room entirely (session + all child data). Irreversible.
    Task<ApiResponse> DeleteSessionAsync(Guid sessionId);
}
