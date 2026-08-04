using BuildingBlocks.Contracts;
using Character.Service.DTOs;

namespace Character.Service.Services.Interface;

public interface ISessionService
{
    // Player-facing reads (JWT). A host sees only rooms they own.
    Task<List<SessionSummaryDto>> GetByOwnerAsync(Guid ownerId);
    Task<SessionDetailDto?> GetDetailAsync(Guid sessionId);
    Task<SessionDetailDto?> GetByRoomCodeAsync(string roomCode);

    // Ownership guard helper: the owner of a room, or null if the room doesn't exist.
    Task<Guid?> GetOwnerIdAsync(Guid sessionId);

    // Game-client ingestion (internal). Create-or-reopen a room with a fixed unique code.
    Task<ApiResponse<SessionDetailDto>> CreateOrReopenAsync(CreateSessionRequest request);
    Task<ApiResponse<SessionDetailDto>> UpdateMetaAsync(UpdateSessionRequest request);

    // Upsert one character's progress / world state for a room.
    Task<ApiResponse<CharacterSessionDto>> SaveCharacterSessionAsync(SaveCharacterSessionRequest request);
    Task<ApiResponse<WorldStateDto>> SaveWorldStateAsync(SaveWorldStateRequest request);

    // Consolidated save: the whole party (both players' progress, world flags, room meta and fog)
    // in ONE request, committed in ONE transaction. Replaces the per-player fan-out.
    Task<ApiResponse<BulkSaveResultDto>> BulkSaveAsync(BulkSaveRequest request);

    // Delete a room entirely (session + all child data). Irreversible.
    Task<ApiResponse> DeleteSessionAsync(Guid sessionId);

    // ── Admin: who played with whom, where, and with what world progress ─────────────────────
    Task<AdminRoomListDto> GetRoomsForAdminAsync(int page, int pageSize, CancellationToken ct = default);
    Task<AdminRoomDetailDto?> GetRoomDetailForAdminAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Room counts for the admin dashboard: total rooms, and how many are co-op.</summary>
    Task<(int Rooms, int Multiplayer)> GetRoomStatsAsync(CancellationToken ct = default);
}
