using BuildingBlocks.Contracts;
using Character.Service.DTOs;

namespace Character.Service.Services.Interface;

public interface ICharacterService
{
    Task<List<CharacterSummaryDto>> GetByOwnerAsync(Guid ownerId);
    Task<CharacterDetailDto?> GetDetailAsync(Guid id);
    Task<PaginatedResponse<AdminCharacterDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<CharacterDetailDto>> IngestSnapshotAsync(SnapshotIngestRequest request);
    Task<ApiResponse> DeleteAsync(Guid id, Guid ownerId, bool isAdmin);
    Task<int> CountAsync();

    // ── Save files ───────────────────────────────────────────────────────────────────────────
    // All of these take the caller's identity and verify it against the character's owner, rather
    // than trusting a caller-supplied owner id.

    Task<ApiResponse<SaveListDto>> GetSavesAsync(Guid characterId, Guid callerId, bool isAdmin, int page, int pageSize);
    Task<ApiResponse<SaveDetailDto>> GetSaveAsync(Guid characterId, long saveId, Guid callerId, bool isAdmin);

    /// <summary>
    /// Delete one save. When it is the newest, live game state is rolled back to the previous save,
    /// so the warning shown to the player ("you lose current progress") is true rather than
    /// decorative. Refuses to delete the only remaining save.
    /// </summary>
    Task<ApiResponse<DeleteSaveResultDto>> DeleteSaveAsync(
        Guid characterId, long saveId, Guid callerId, bool isAdmin, bool alsoRollBackWorldState);
}
