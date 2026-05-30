using BuildingBlocks.Contracts;
using Character.Service.DTOs;

namespace Character.Service.Services;

public interface ICharacterService
{
    Task<List<CharacterSummaryDto>> GetByOwnerAsync(Guid ownerId);
    Task<CharacterDetailDto?> GetDetailAsync(Guid id);
    Task<List<AdminCharacterDto>> GetAllAsync();
    Task<ApiResponse<CharacterDetailDto>> IngestSnapshotAsync(SnapshotIngestRequest request);
    Task<int> CountAsync();
}
