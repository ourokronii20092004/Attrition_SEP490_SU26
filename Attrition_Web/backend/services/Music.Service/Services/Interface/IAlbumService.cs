using Music.Service.DTOs;

namespace Music.Service.Services.Interface;

public interface IAlbumService
{
    Task<IEnumerable<MusicAlbumDto>> GetAlbumsAsync();
    Task<BuildingBlocks.Contracts.PaginatedResponse<MusicAlbumDto>> GetAlbumsPagedAsync(int page, int pageSize);
    Task<AlbumDetailDto?> GetAlbumAsync(int id);
    Task<MusicAlbumDto> CreateAlbumAsync(CreateAlbumRequest req);
    Task<MusicAlbumDto?> UpdateAlbumAsync(int id, CreateAlbumRequest req);
    Task<bool> DeleteAlbumAsync(int id);
    Task<(bool success, string? error, string? coverPath)> UploadAlbumCoverAsync(int id, Microsoft.AspNetCore.Http.IFormFile file);
    Task<int> CountAsync();
}
